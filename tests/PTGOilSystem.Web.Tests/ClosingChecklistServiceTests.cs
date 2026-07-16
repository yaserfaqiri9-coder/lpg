using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PTGOilSystem.Web.Configuration;
using PTGOilSystem.Web.Controllers;
using PTGOilSystem.Web.Data;
using PTGOilSystem.Web.Models.Entities;
using PTGOilSystem.Web.Services.Accounting;
using Xunit;

namespace PTGOilSystem.Web.Tests;

/// <summary>
/// مرحله ۱۲ — چک‌لیستِ بستنِ سال. مهم‌ترین تضمین‌ها: کاملاً فقط‌خواندنی، مستقل per-company/year،
/// و اینکه هر وضعیتِ ناسالمِ اثبات‌پذیر واقعاً Blocked شود و چیزی جعل نشود.
/// </summary>
public class ClosingChecklistServiceTests
{
    private const int AccountBaseId = 100;

    [Fact]
    public async Task Healthy_Year_Has_No_Blocked_Checks()
    {
        await using var db = NewDb();
        SeedHealthy(db, companyId: 1);
        await db.SaveChangesAsync();

        var report = await Build(db, 1, 1);

        Assert.NotNull(report);
        Assert.DoesNotContain(report!.Checks, c => c.Status == ClosingCheckStatus.Blocked);
        Assert.Contains(report.Checks, c => c.Status == ClosingCheckStatus.Passed);
        Assert.Contains(report.Checks, c => c.Status == ClosingCheckStatus.NotApplicable);
        Assert.Contains(report.Checks, c => c.Code == "PERIOD_END_REVALUATION_PENDING" && c.Status == ClosingCheckStatus.Warning);
    }

    [Fact]
    public async Task Unknown_Year_For_Company_Returns_Null()
    {
        await using var db = NewDb();
        SeedHealthy(db, companyId: 1);
        SeedCompany(db, id: 2, code: "OTHER");
        db.FiscalYears.Add(new FiscalYear
        {
            Id = 2, CompanyId = 2, Name = "FY2 for C2",
            StartDate = new DateTime(2026, 1, 1), EndDate = new DateTime(2026, 12, 31),
            Status = FiscalYearStatus.Open, IsCurrent = true
        });
        await db.SaveChangesAsync();

        // سالِ شرکت ۲ با شناسهٔ شرکت ۱ خواسته می‌شود: باید null شود، نه گزارشِ شرکت اشتباه.
        Assert.Null(await Build(db, companyId: 1, fiscalYearId: 2));
    }

    [Fact]
    public async Task Company_Isolation_Unbalanced_Journal_Of_Other_Company_Does_Not_Leak()
    {
        await using var db = NewDb();
        SeedHealthy(db, companyId: 1);
        SeedCompany(db, id: 2, code: "OTHER");
        // سند نامتوازنِ شرکت ۲ نباید در چک‌لیستِ شرکت ۱ دیده شود.
        db.JournalEntries.Add(new JournalEntry
        {
            Id = 900, CompanyId = 2, FiscalYearId = 1, FiscalPeriodId = 1,
            JournalNumber = "C2-BAD", Status = JournalEntryStatus.Posted, PostedAt = DateTime.UtcNow,
            SourceModule = "Purchase",
            Lines = [ new() { AccountId = AccountBaseId, LineNumber = 1, Debit = 100m },
                      new() { AccountId = AccountBaseId + 1, LineNumber = 2, Credit = 90m } ]
        });
        await db.SaveChangesAsync();

        var report = await Build(db, 1, 1);
        Assert.Contains(report!.Checks, c => c.Code == "NO_UNBALANCED_JOURNAL" && c.Status == ClosingCheckStatus.Passed);
    }

    [Fact]
    public async Task Missing_Settings_Is_Blocked()
    {
        await using var db = NewDb();
        SeedCompany(db);
        SeedFullYear(db, companyId: 1);
        await db.SaveChangesAsync();

        var report = await Build(db, 1, 1);
        Assert.Equal(ClosingCheckStatus.Blocked, report!.OverallStatus);
        Assert.Contains(report.Checks, c => c.Code == "ACCOUNTING_SETTINGS_MISSING" && c.Status == ClosingCheckStatus.Blocked);
    }

    [Fact]
    public async Task Unbalanced_Journal_Is_Blocked()
    {
        await using var db = NewDb();
        SeedHealthy(db, companyId: 1);
        db.JournalEntries.Add(new JournalEntry
        {
            Id = 5, CompanyId = 1, FiscalYearId = 1, FiscalPeriodId = 1,
            JournalNumber = "JV-BAD", Status = JournalEntryStatus.Posted, PostedAt = DateTime.UtcNow,
            SourceModule = "Purchase",
            Lines = [ new() { AccountId = AccountBaseId, LineNumber = 1, Debit = 100m },
                      new() { AccountId = AccountBaseId + 1, LineNumber = 2, Credit = 90m } ]
        });
        await db.SaveChangesAsync();

        var report = await Build(db, 1, 1);
        Assert.Contains(report!.Checks, c => c.Code == "UNBALANCED_JOURNAL" && c.Status == ClosingCheckStatus.Blocked);
    }

    [Fact]
    public async Task Draft_Journal_Is_Blocked_As_Inconsistent_State()
    {
        await using var db = NewDb();
        SeedHealthy(db, companyId: 1);
        db.JournalEntries.Add(new JournalEntry
        {
            Id = 6, CompanyId = 1, FiscalYearId = 1, FiscalPeriodId = 1,
            JournalNumber = "JV-DRAFT", Status = JournalEntryStatus.Draft,
            SourceModule = "Purchase",
            Lines = [ new() { AccountId = AccountBaseId, LineNumber = 1, Debit = 10m },
                      new() { AccountId = AccountBaseId + 1, LineNumber = 2, Credit = 10m } ]
        });
        await db.SaveChangesAsync();

        var report = await Build(db, 1, 1);
        Assert.Contains(report!.Checks, c => c.Code == "INCONSISTENT_JOURNAL_STATE" && c.Status == ClosingCheckStatus.Blocked);
    }

    [Fact]
    public async Task Duplicate_Source_Event_Is_Blocked()
    {
        await using var db = NewDb();
        SeedHealthy(db, companyId: 1);
        db.JournalEntries.AddRange(
            Balanced(10, "Purchase:7:Created:0"),
            Balanced(11, "Purchase:7:Created:0"));
        await db.SaveChangesAsync();

        var report = await Build(db, 1, 1);
        Assert.Contains(report!.Checks, c => c.Code == "DUPLICATE_SOURCE_EVENT" && c.Status == ClosingCheckStatus.Blocked);
    }

    [Fact]
    public async Task Negative_Inventory_Is_Blocked()
    {
        await using var db = NewDb();
        SeedHealthy(db, companyId: 1);
        db.InventoryAverageCosts.Add(new InventoryAverageCost
        {
            Id = 1, CompanyId = 1, ProductId = 1, TerminalId = 1, QuantityMt = -5m, TotalValueUsd = 10m
        });
        await db.SaveChangesAsync();

        var report = await Build(db, 1, 1);
        Assert.Contains(report!.Checks, c => c.Code == "NEGATIVE_INVENTORY" && c.Status == ClosingCheckStatus.Blocked);
    }

    [Fact]
    public async Task Inventory_Quantity_Without_Value_Is_Blocked()
    {
        await using var db = NewDb();
        SeedHealthy(db, companyId: 1);
        db.InventoryAverageCosts.Add(new InventoryAverageCost
        {
            Id = 2, CompanyId = 1, ProductId = 1, TerminalId = 1, QuantityMt = 50m, TotalValueUsd = 0m
        });
        await db.SaveChangesAsync();

        var report = await Build(db, 1, 1);
        Assert.Contains(report!.Checks, c => c.Code == "INVENTORY_POOL_INCONSISTENT" && c.Status == ClosingCheckStatus.Blocked);
    }

    [Fact]
    public async Task Period_Gap_Is_Blocked()
    {
        await using var db = NewDb();
        SeedCompany(db);
        SeedAccountsAndSettings(db, companyId: 1);
        db.FiscalYears.Add(new FiscalYear
        {
            Id = 1, CompanyId = 1, Name = "1405",
            StartDate = new DateTime(2026, 1, 1), EndDate = new DateTime(2026, 12, 31),
            Status = FiscalYearStatus.Open, IsCurrent = true
        });
        // فاصله بین دو دوره: فوریه پوشش داده نمی‌شود.
        db.FiscalPeriods.Add(new FiscalPeriod
        {
            Id = 1, CompanyId = 1, FiscalYearId = 1, PeriodNumber = 1, Name = "Jan",
            StartDate = new DateTime(2026, 1, 1), EndDate = new DateTime(2026, 1, 31), Status = FiscalPeriodStatus.Open
        });
        db.FiscalPeriods.Add(new FiscalPeriod
        {
            Id = 2, CompanyId = 1, FiscalYearId = 1, PeriodNumber = 2, Name = "Mar",
            StartDate = new DateTime(2026, 3, 1), EndDate = new DateTime(2026, 12, 31), Status = FiscalPeriodStatus.Open
        });
        await db.SaveChangesAsync();

        var report = await Build(db, 1, 1);
        Assert.Contains(report!.Checks, c => c.Code == "PERIOD_COVERAGE_INCOMPLETE" && c.Status == ClosingCheckStatus.Blocked);
    }

    [Fact]
    public async Task Overlapping_Fiscal_Year_Is_Blocked()
    {
        await using var db = NewDb();
        SeedHealthy(db, companyId: 1);
        db.FiscalYears.Add(new FiscalYear
        {
            Id = 2, CompanyId = 1, Name = "overlap",
            StartDate = new DateTime(2026, 6, 1), EndDate = new DateTime(2027, 5, 31),
            Status = FiscalYearStatus.Draft
        });
        await db.SaveChangesAsync();

        var report = await Build(db, 1, 1);
        Assert.Contains(report!.Checks, c => c.Code == "FISCAL_YEAR_INVALID" && c.Status == ClosingCheckStatus.Blocked);
    }

    [Fact]
    public async Task Unexplained_1310_Is_Reported_As_Warning_Never_Faked_Passed()
    {
        await using var db = NewDb();
        SeedHealthy(db, companyId: 1);
        await db.SaveChangesAsync();

        var report = await Build(db, 1, 1);
        var check = Assert.Single(report!.Checks, c => c.Code == "INVENTORY_IN_TRANSIT_1310");
        Assert.Equal(ClosingCheckStatus.Warning, check.Status);
    }

    [Fact]
    public async Task Feature_Flags_Status_Is_Reported()
    {
        await using var db = NewDb();
        SeedHealthy(db, companyId: 1);
        await db.SaveChangesAsync();

        var report = await Build(db, 1, 1);
        var flags = Assert.Single(report!.Checks, c => c.Code == "FEATURE_FLAGS_STATUS");
        Assert.Contains(flags.SampleRecords, s => s == "Accounting.Enabled=OFF");
    }

    [Fact]
    public async Task Running_Twice_Is_Idempotent()
    {
        await using var db = NewDb();
        SeedHealthy(db, companyId: 1);
        await db.SaveChangesAsync();

        var first = await Build(db, 1, 1);
        var second = await Build(db, 1, 1);

        Assert.Equal(
            first!.Checks.Select(c => (c.Code, c.Status, c.RecordCount)),
            second!.Checks.Select(c => (c.Code, c.Status, c.RecordCount)));
    }

    [Fact]
    public async Task Report_Writes_Nothing()
    {
        await using var db = NewDb();
        SeedHealthy(db, companyId: 1);
        db.InventoryAverageCosts.Add(new InventoryAverageCost
        {
            Id = 3, CompanyId = 1, ProductId = 1, TerminalId = 1, QuantityMt = 10m, TotalValueUsd = 100m
        });
        await db.SaveChangesAsync();

        var before = await SnapshotAsync(db);
        await Build(db, 1, 1);
        var after = await SnapshotAsync(db);

        Assert.Equal(before, after);
        Assert.DoesNotContain(db.ChangeTracker.Entries(), e => e.State != EntityState.Unchanged);
    }

    [Fact]
    public async Task Csv_Export_Contains_A_Row_Per_Check()
    {
        await using var db = NewDb();
        SeedHealthy(db, companyId: 1);
        await db.SaveChangesAsync();

        var controller = new ClosingChecklistController(NewService(db), db);
        var result = Assert.IsType<Microsoft.AspNetCore.Mvc.FileContentResult>(
            await controller.Csv(1, 1, default));

        var text = System.Text.Encoding.UTF8.GetString(result.FileContents);
        var report = await Build(db, 1, 1);
        // یک سطر عنوان + یک سطر برای هر کنترل.
        var dataLines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length - 1;
        Assert.Equal(report!.Checks.Count, dataLines);
        Assert.Contains("Code,Status,Title", text);
    }

    [Fact]
    public async Task Json_Export_Returns_Report()
    {
        await using var db = NewDb();
        SeedHealthy(db, companyId: 1);
        await db.SaveChangesAsync();

        var controller = new ClosingChecklistController(NewService(db), db);
        var json = Assert.IsType<Microsoft.AspNetCore.Mvc.JsonResult>(await controller.Json(1, 1, default));
        Assert.IsType<ClosingChecklistReport>(json.Value);
    }

    // ---- helpers ----

    private static ApplicationDbContext NewDb()
        => new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static IClosingChecklistService NewService(ApplicationDbContext db)
    {
        var options = Options.Create(new AccountingOptions());
        return new ClosingChecklistService(db, options, new AccountingReadinessService(db, options));
    }

    private static Task<ClosingChecklistReport?> Build(ApplicationDbContext db, int companyId, int fiscalYearId)
        => NewService(db).BuildAsync(companyId, fiscalYearId);

    private static async Task<string> SnapshotAsync(ApplicationDbContext db)
        => string.Join("|",
            await db.JournalEntries.AsNoTracking().CountAsync(),
            await db.InventoryAverageCosts.AsNoTracking().CountAsync(),
            await db.FiscalYears.AsNoTracking().CountAsync(),
            await db.FiscalPeriods.AsNoTracking().CountAsync(),
            await db.AccountingSettings.AsNoTracking().CountAsync());

    private static JournalEntry Balanced(int id, string sourceEventId) => new()
    {
        Id = id, CompanyId = 1, FiscalYearId = 1, FiscalPeriodId = 1,
        JournalNumber = $"JV-{id}", Status = JournalEntryStatus.Posted, PostedAt = DateTime.UtcNow,
        SourceModule = "Purchase", SourceEventId = sourceEventId,
        Lines = [ new() { AccountId = AccountBaseId, LineNumber = 1, Debit = 100m },
                  new() { AccountId = AccountBaseId + 1, LineNumber = 2, Credit = 100m } ]
    };

    private static void SeedCompany(ApplicationDbContext db, int id = 1, string code = "PTG")
        => db.Companies.Add(new Company { Id = id, Code = code, Name = $"Company {code}", IsActive = true });

    private static void SeedFullYear(ApplicationDbContext db, int companyId)
    {
        db.FiscalYears.Add(new FiscalYear
        {
            Id = 1, CompanyId = companyId, Name = "1405",
            StartDate = new DateTime(2026, 1, 1), EndDate = new DateTime(2026, 12, 31),
            Status = FiscalYearStatus.Open, IsCurrent = true
        });
        db.FiscalPeriods.Add(new FiscalPeriod
        {
            Id = 1, CompanyId = companyId, FiscalYearId = 1, PeriodNumber = 1, Name = "Full",
            StartDate = new DateTime(2026, 1, 1), EndDate = new DateTime(2026, 12, 31), Status = FiscalPeriodStatus.Open
        });
    }

    private static void SeedHealthy(ApplicationDbContext db, int companyId)
    {
        SeedCompany(db, companyId);
        SeedAccountsAndSettings(db, companyId);
        SeedFullYear(db, companyId);
    }

    private static void SeedAccountsAndSettings(ApplicationDbContext db, int companyId)
    {
        for (var i = 0; i < 20; i++)
        {
            db.Accounts.Add(new Account
            {
                Id = AccountBaseId + i, CompanyId = companyId,
                Code = $"ACC-{i:00}", Name = $"Account {i}",
                AccountType = i == 12 ? AccountType.Revenue : i == 13 || i == 14 ? AccountType.Expense : AccountType.Asset,
                NormalBalance = NormalBalance.Debit, IsActive = true
            });
        }

        db.AccountingSettings.Add(new AccountingSettings
        {
            Id = companyId, CompanyId = companyId, FunctionalCurrencyCode = "USD",
            CashBankControlAccountId = AccountBaseId,
            AccountsReceivableAccountId = AccountBaseId + 1,
            AccountsPayableAccountId = AccountBaseId + 2,
            InventoryAccountId = AccountBaseId + 3,
            InventoryInTransitAccountId = AccountBaseId + 4,
            SupplierPrepaymentAccountId = AccountBaseId + 5,
            CustomerAdvanceAccountId = AccountBaseId + 6,
            FreightPayableAccountId = AccountBaseId + 7,
            CommissionPayableAccountId = AccountBaseId + 8,
            EmployeeAdvanceAccountId = AccountBaseId + 9,
            EmployeePayableAccountId = AccountBaseId + 10,
            AccruedExpenseAccountId = AccountBaseId + 11,
            SalesRevenueAccountId = AccountBaseId + 12,
            CostOfGoodsSoldAccountId = AccountBaseId + 13,
            GeneralExpenseAccountId = AccountBaseId + 14,
            ExchangeGainAccountId = AccountBaseId + 15,
            ExchangeLossAccountId = AccountBaseId + 16,
            InventoryLossAccountId = AccountBaseId + 17,
            CurrentYearProfitLossAccountId = AccountBaseId + 18,
            RetainedEarningsAccountId = AccountBaseId + 19
        });
    }
}
