using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PTGOilSystem.Web.Configuration;
using PTGOilSystem.Web.Data;
using PTGOilSystem.Web.Models.Entities;
using PTGOilSystem.Web.Services.Accounting;
using Xunit;

namespace PTGOilSystem.Web.Tests;

/// <summary>
/// مرحله ۱۳ — Trial Close و تسعیرِ پایان دوره. تسعیر فقط روی ماندهٔ پولیِ باز، فقط با نرخِ دقیقِ
/// EndDate، بدون تغییرِ وضعیتِ سال یا HardLockِ دوره.
/// </summary>
public class TrialCloseServiceTests
{
    private const int Base = 100;
    private const int ArId = Base + 1;
    private const int ApId = Base + 2;
    private const int RevenueId = Base + 12;
    private const int GainId = Base + 15;
    private const int LossId = Base + 16;
    private static readonly DateTime EndDate = new(2025, 12, 31);

    [Fact]
    public async Task Preview_Writes_Nothing()
    {
        await using var db = NewDb();
        Seed(db);
        SeedOpenReceivable(db, sourceRub: 8000m, carryingUsd: 100m);
        SeedClosingRate(db, "RUB", 100m);
        await db.SaveChangesAsync();

        var before = await CountsAsync(db);
        var preview = await Svc(db).PreviewAsync(1, 1);
        var after = await CountsAsync(db);

        Assert.NotNull(preview);
        Assert.Equal(before, after);
        Assert.DoesNotContain(db.ChangeTracker.Entries(), e => e.State != EntityState.Unchanged);
    }

    [Fact]
    public async Task Asset_Loss_Is_Computed_When_Currency_Strengthens_Against_Usd()
    {
        await using var db = NewDb();
        Seed(db);
        // AR 8000 RUB دفتری 100 USD (نرخ ثبت 80 RUB/USD). نرخ بستن 100 RUB/USD → 80 USD → زیان ۲۰.
        SeedOpenReceivable(db, sourceRub: 8000m, carryingUsd: 100m);
        SeedClosingRate(db, "RUB", 100m);
        await db.SaveChangesAsync();

        var preview = await Svc(db).PreviewAsync(1, 1);
        var line = Assert.Single(preview!.Revaluations.Single().Lines);
        Assert.Equal(80m, line.ClosingUsd);
        Assert.Equal(-20m, line.DifferenceUsd);
    }

    [Fact]
    public async Task Apply_Posts_Balanced_Revaluation_And_Auto_Reversal_In_Next_Year()
    {
        await using var db = NewDb();
        Seed(db);
        SeedOpenReceivable(db, sourceRub: 8000m, carryingUsd: 100m);
        SeedClosingRate(db, "RUB", 100m);
        await db.SaveChangesAsync();

        var result = await Svc(db).ApplyRevaluationAsync(1, 1, userId: 7);
        Assert.Equal(TrialCloseResultStatus.Succeeded, result.Status);

        var reval = db.JournalEntries.Include(j => j.Lines)
            .Single(j => j.SourceEventId == "FiscalYearRevaluation:1:RUB:0");
        Assert.Equal(EndDate, reval.AccountingDate);
        Assert.Equal(reval.Lines.Sum(l => l.Debit), reval.Lines.Sum(l => l.Credit));
        // زیان: Dr 5300 Loss 20، Cr 1200 AR 20.
        Assert.Contains(reval.Lines, l => l.AccountId == LossId && l.Debit == 20m);
        Assert.Contains(reval.Lines, l => l.AccountId == ArId && l.Credit == 20m);

        var autoRev = db.JournalEntries.Include(j => j.Lines)
            .Single(j => j.SourceEventId == "FiscalYearRevaluation:1:RUB:0:Reversal");
        Assert.Equal(new DateTime(2026, 1, 1), autoRev.AccountingDate);
        // برگشت: عکسِ سندِ تسعیر.
        Assert.Contains(autoRev.Lines, l => l.AccountId == LossId && l.Credit == 20m);
        Assert.Contains(autoRev.Lines, l => l.AccountId == ArId && l.Debit == 20m);
    }

    [Fact]
    public async Task Liability_Gain_Posts_To_Exchange_Gain()
    {
        await using var db = NewDb();
        Seed(db);
        // AP 8000 RUB دفتری 100 USD. نرخ بستن 100 RUB/USD → 80 USD بدهی → بدهی کمتر → سود ۲۰.
        SeedOpenPayable(db, sourceRub: 8000m, carryingUsd: 100m);
        SeedClosingRate(db, "RUB", 100m);
        await db.SaveChangesAsync();

        await Svc(db).ApplyRevaluationAsync(1, 1, userId: 1);

        var reval = db.JournalEntries.Include(j => j.Lines)
            .Single(j => j.SourceEventId == "FiscalYearRevaluation:1:RUB:0");
        // سود: Dr 2100 AP 20، Cr 4200 Gain 20.
        Assert.Contains(reval.Lines, l => l.AccountId == ApId && l.Debit == 20m);
        Assert.Contains(reval.Lines, l => l.AccountId == GainId && l.Credit == 20m);
    }

    [Fact]
    public async Task Party_And_Contract_Dimensions_Are_Preserved()
    {
        await using var db = NewDb();
        Seed(db);
        SeedOpenReceivable(db, sourceRub: 8000m, carryingUsd: 100m, partyId: 55, contractId: 66);
        SeedClosingRate(db, "RUB", 100m);
        await db.SaveChangesAsync();

        await Svc(db).ApplyRevaluationAsync(1, 1, userId: 1);

        var arLine = db.JournalEntries.Include(j => j.Lines)
            .Single(j => j.SourceEventId == "FiscalYearRevaluation:1:RUB:0")
            .Lines.Single(l => l.AccountId == ArId);
        Assert.Equal(AccountingPartyType.Customer, arLine.PartyType);
        Assert.Equal(55, arLine.PartyId);
        Assert.Equal(66, arLine.ContractId);
    }

    [Fact]
    public async Task Non_Monetary_And_Usd_Balances_Are_Not_Revalued()
    {
        await using var db = NewDb();
        Seed(db);
        // موجودی (غیرپولی) با ارز RUB نباید تسعیر شود.
        SeedPostedLine(db, accountId: Base + 3, debitUsd: 100m, currency: "RUB", sourceAmount: 8000m);
        // AR ولی به USD — نباید تسعیر شود.
        SeedPostedLine(db, accountId: ArId, debitUsd: 50m, currency: "USD", sourceAmount: 50m);
        SeedClosingRate(db, "RUB", 100m);
        await db.SaveChangesAsync();

        var preview = await Svc(db).PreviewAsync(1, 1);
        Assert.Empty(preview!.Revaluations);
    }

    [Fact]
    public async Task Missing_Exact_End_Date_Rate_Blocks_Apply()
    {
        await using var db = NewDb();
        Seed(db);
        SeedOpenReceivable(db, sourceRub: 8000m, carryingUsd: 100m);
        // نرخ فقط برای یک روز قبل موجود است — نباید fallback شود.
        db.DailyFxRates.Add(new DailyFxRate { BaseCurrency = "USD", QuoteCurrency = "RUB", RateDate = new DateTime(2025, 12, 30), Rate = 100m });
        await db.SaveChangesAsync();

        var preview = await Svc(db).PreviewAsync(1, 1);
        Assert.Equal("CLOSING_RATE_MISSING", preview!.BlockingReason);
        Assert.Contains(preview.MissingRates, m => m.Currency == "RUB");

        var result = await Svc(db).ApplyRevaluationAsync(1, 1, userId: 1);
        Assert.Equal(TrialCloseResultStatus.Blocked, result.Status);
        Assert.Equal("CLOSING_RATE_MISSING", result.FailureCode);
    }

    [Fact]
    public async Task Apply_Is_Idempotent_When_Nothing_Changed()
    {
        await using var db = NewDb();
        Seed(db);
        SeedOpenReceivable(db, sourceRub: 8000m, carryingUsd: 100m);
        SeedClosingRate(db, "RUB", 100m);
        await db.SaveChangesAsync();

        await Svc(db).ApplyRevaluationAsync(1, 1, userId: 1);
        var afterFirst = db.JournalEntries.Count();
        await Svc(db).ApplyRevaluationAsync(1, 1, userId: 1);
        var afterSecond = db.JournalEntries.Count();

        Assert.Equal(afterFirst, afterSecond); // Duplicate و بی‌اثر.
    }

    [Fact]
    public async Task Rate_Change_Supersedes_And_Posts_New_Revision()
    {
        await using var db = NewDb();
        Seed(db);
        SeedOpenReceivable(db, sourceRub: 8000m, carryingUsd: 100m);
        SeedClosingRate(db, "RUB", 100m);
        await db.SaveChangesAsync();

        await Svc(db).ApplyRevaluationAsync(1, 1, userId: 1);

        // نرخ عوض می‌شود → Revision جدید انتظار می‌رود.
        var rate = db.DailyFxRates.Single(r => r.RateDate == EndDate);
        rate.Rate = 80m; // حالا 8000/80 = 100 = دفتری → اختلاف صفر برای rev1؟ نه: نرخ 125 بگیریم.
        rate.Rate = 125m; // 8000/125 = 64 → اختلاف -36.
        await db.SaveChangesAsync();

        var result = await Svc(db).ApplyRevaluationAsync(1, 1, userId: 1);
        Assert.Equal(TrialCloseResultStatus.Succeeded, result.Status);
        Assert.True(db.JournalEntries.Any(j => j.SourceEventId == "FiscalYearRevaluation:1:RUB:1"));
        // نسخهٔ ۰ Supersede شده است.
        Assert.True(db.JournalEntries.Any(j => j.SourceEventId == "FiscalYearRevaluation:1:RUB:0:Superseded"));
    }

    [Fact]
    public async Task Missing_Next_Year_Open_Period_Blocks_Apply()
    {
        await using var db = NewDb();
        Seed(db, includeNextYear: false);
        SeedOpenReceivable(db, sourceRub: 8000m, carryingUsd: 100m);
        SeedClosingRate(db, "RUB", 100m);
        await db.SaveChangesAsync();

        var preview = await Svc(db).PreviewAsync(1, 1);
        Assert.Equal("NEXT_YEAR_OPEN_PERIOD_MISSING", preview!.BlockingReason);

        var result = await Svc(db).ApplyRevaluationAsync(1, 1, userId: 1);
        Assert.Equal(TrialCloseResultStatus.Blocked, result.Status);
    }

    [Fact]
    public async Task Trial_Close_Creates_Snapshot_Without_Changing_Year_Or_Locking_Periods()
    {
        await using var db = NewDb();
        Seed(db);
        SeedOpenReceivable(db, sourceRub: 8000m, carryingUsd: 100m);
        SeedClosingRate(db, "RUB", 100m);
        await db.SaveChangesAsync();

        var result = await Svc(db).RunTrialCloseAsync(1, 1, userId: 9, acknowledgeWarnings: true);
        Assert.Equal(TrialCloseResultStatus.Succeeded, result.Status);

        var run = db.FiscalYearCloseRuns.Single(r => r.Id == result.CloseRunId);
        Assert.Equal(FiscalYearCloseRunType.Trial, run.RunType);
        Assert.False(string.IsNullOrEmpty(run.SnapshotHash));
        Assert.False(string.IsNullOrEmpty(run.ChecklistSnapshotJson));

        var year = db.FiscalYears.Single(y => y.Id == 1);
        Assert.Equal(FiscalYearStatus.Open, year.Status);
        Assert.Null(year.ClosedAt);
        Assert.All(db.FiscalPeriods.Where(p => p.FiscalYearId == 1),
            p => Assert.Equal(FiscalPeriodStatus.Open, p.Status));
    }

    [Fact]
    public async Task Trial_Close_Requires_Warning_Acknowledgement()
    {
        await using var db = NewDb();
        Seed(db);
        SeedOpenReceivable(db, sourceRub: 8000m, carryingUsd: 100m);
        SeedClosingRate(db, "RUB", 100m);
        await db.SaveChangesAsync();

        // بدون تأیید هشدارها (چک‌لیست همیشه PERIOD_END_REVALUATION_PENDING دارد).
        var result = await Svc(db).RunTrialCloseAsync(1, 1, userId: 1, acknowledgeWarnings: false);
        Assert.Equal(TrialCloseResultStatus.WarningsNotAcknowledged, result.Status);
    }

    [Fact]
    public async Task Checklist_Blocker_Prevents_Trial_Close()
    {
        await using var db = NewDb();
        Seed(db);
        // سند نامتوازن → چک‌لیست Blocked.
        db.JournalEntries.Add(new JournalEntry
        {
            Id = 50, CompanyId = 1, FiscalYearId = 1, FiscalPeriodId = 1,
            JournalNumber = "BAD", Status = JournalEntryStatus.Posted, PostedAt = DateTime.UtcNow,
            AccountingDate = new DateTime(2025, 6, 1), SourceModule = "Purchase",
            Lines = [ new() { AccountId = Base, LineNumber = 1, Debit = 10m },
                      new() { AccountId = Base + 1, LineNumber = 2, Credit = 9m } ]
        });
        await db.SaveChangesAsync();

        var result = await Svc(db).RunTrialCloseAsync(1, 1, userId: 1, acknowledgeWarnings: true);
        Assert.Equal(TrialCloseResultStatus.Blocked, result.Status);
        Assert.Equal("CHECKLIST_BLOCKED", result.FailureCode);
    }

    [Fact]
    public async Task Company_Isolation_Unknown_Year_Returns_Null_Preview()
    {
        await using var db = NewDb();
        Seed(db);
        await db.SaveChangesAsync();
        Assert.Null(await Svc(db).PreviewAsync(companyId: 999, fiscalYearId: 1));
    }

    // ---- helpers ----

    private static ApplicationDbContext NewDb()
        => new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static ITrialCloseService Svc(ApplicationDbContext db)
    {
        var options = Options.Create(new AccountingOptions { Enabled = true });
        var calendar = new FiscalCalendarService(db);
        var guard = new PeriodGuard(db, calendar);
        var posting = new AccountingPostingService(db, guard, options);
        var checklist = new ClosingChecklistService(db, options, new AccountingReadinessService(db, options));
        return new TrialCloseService(db, checklist, posting);
    }

    private static async Task<string> CountsAsync(ApplicationDbContext db)
        => string.Join("|",
            await db.JournalEntries.CountAsync(),
            await db.JournalEntryLines.CountAsync(),
            await db.FiscalYearCloseRuns.CountAsync());

    private static void SeedClosingRate(ApplicationDbContext db, string currency, decimal rate)
        => db.DailyFxRates.Add(new DailyFxRate { BaseCurrency = "USD", QuoteCurrency = currency, RateDate = EndDate, Rate = rate });

    private static void Seed(ApplicationDbContext db, bool includeNextYear = true)
    {
        db.Companies.Add(new Company { Id = 1, Code = "PTG", Name = "PTG", IsActive = true });

        for (var i = 0; i < 20; i++)
        {
            db.Accounts.Add(new Account
            {
                Id = Base + i, CompanyId = 1, Code = $"ACC-{i:00}", Name = $"Account {i}",
                AccountType = i == 12 ? AccountType.Revenue : i is 13 or 14 or 16 ? AccountType.Expense : AccountType.Asset,
                NormalBalance = NormalBalance.Debit, IsActive = true,
                MonetaryTreatment = (Base + i) is ArId or ApId ? MonetaryTreatment.Monetary : MonetaryTreatment.NonMonetary
            });
        }

        db.AccountingSettings.Add(new AccountingSettings
        {
            Id = 1, CompanyId = 1, FunctionalCurrencyCode = "USD",
            CashBankControlAccountId = Base, AccountsReceivableAccountId = ArId, AccountsPayableAccountId = ApId,
            InventoryAccountId = Base + 3, InventoryInTransitAccountId = Base + 4,
            SupplierPrepaymentAccountId = Base + 5, CustomerAdvanceAccountId = Base + 6,
            FreightPayableAccountId = Base + 7, CommissionPayableAccountId = Base + 8,
            EmployeeAdvanceAccountId = Base + 9, EmployeePayableAccountId = Base + 10,
            AccruedExpenseAccountId = Base + 11, SalesRevenueAccountId = RevenueId,
            CostOfGoodsSoldAccountId = Base + 13, GeneralExpenseAccountId = Base + 14,
            ExchangeGainAccountId = GainId, ExchangeLossAccountId = LossId,
            InventoryLossAccountId = Base + 17, CurrentYearProfitLossAccountId = Base + 18,
            RetainedEarningsAccountId = Base + 19
        });

        db.FiscalYears.Add(new FiscalYear
        {
            Id = 1, CompanyId = 1, Name = "FY-2025", StartDate = new DateTime(2025, 1, 1), EndDate = EndDate,
            Status = FiscalYearStatus.Open, IsCurrent = false
        });
        db.FiscalPeriods.Add(new FiscalPeriod
        {
            Id = 1, CompanyId = 1, FiscalYearId = 1, PeriodNumber = 1, Name = "FY25",
            StartDate = new DateTime(2025, 1, 1), EndDate = EndDate, Status = FiscalPeriodStatus.Open
        });

        if (includeNextYear)
        {
            db.FiscalYears.Add(new FiscalYear
            {
                Id = 2, CompanyId = 1, Name = "FY-2026", StartDate = new DateTime(2026, 1, 1),
                EndDate = new DateTime(2026, 12, 31), Status = FiscalYearStatus.Open, IsCurrent = true
            });
            db.FiscalPeriods.Add(new FiscalPeriod
            {
                Id = 2, CompanyId = 1, FiscalYearId = 2, PeriodNumber = 1, Name = "FY26",
                StartDate = new DateTime(2026, 1, 1), EndDate = new DateTime(2026, 12, 31), Status = FiscalPeriodStatus.Open
            });
        }
    }

    private static int _lineSeq = 1000;

    private static void SeedOpenReceivable(ApplicationDbContext db, decimal sourceRub, decimal carryingUsd,
        int? partyId = 55, int? contractId = null)
    {
        db.JournalEntries.Add(new JournalEntry
        {
            Id = ++_lineSeq, CompanyId = 1, FiscalYearId = 1, FiscalPeriodId = 1,
            JournalNumber = $"SETUP-{_lineSeq}", Status = JournalEntryStatus.Posted, PostedAt = DateTime.UtcNow,
            AccountingDate = new DateTime(2025, 6, 1), SourceModule = "Sale",
            Lines =
            [
                new() { LineNumber = 1, AccountId = ArId, Debit = carryingUsd, TransactionCurrencyCode = "RUB",
                    TransactionAmount = sourceRub, ExchangeRate = decimal.Round(carryingUsd / sourceRub, 8),
                    PartyType = AccountingPartyType.Customer, PartyId = partyId, ContractId = contractId },
                new() { LineNumber = 2, AccountId = RevenueId, Credit = carryingUsd, TransactionCurrencyCode = "USD",
                    TransactionAmount = carryingUsd, ExchangeRate = 1m }
            ]
        });
    }

    private static void SeedOpenPayable(ApplicationDbContext db, decimal sourceRub, decimal carryingUsd)
    {
        db.JournalEntries.Add(new JournalEntry
        {
            Id = ++_lineSeq, CompanyId = 1, FiscalYearId = 1, FiscalPeriodId = 1,
            JournalNumber = $"SETUP-{_lineSeq}", Status = JournalEntryStatus.Posted, PostedAt = DateTime.UtcNow,
            AccountingDate = new DateTime(2025, 6, 1), SourceModule = "Purchase",
            Lines =
            [
                new() { LineNumber = 1, AccountId = ApId, Credit = carryingUsd, TransactionCurrencyCode = "RUB",
                    TransactionAmount = sourceRub, ExchangeRate = decimal.Round(carryingUsd / sourceRub, 8),
                    PartyType = AccountingPartyType.Supplier, PartyId = 1 },
                new() { LineNumber = 2, AccountId = Base + 3, Debit = carryingUsd, TransactionCurrencyCode = "USD",
                    TransactionAmount = carryingUsd, ExchangeRate = 1m }
            ]
        });
    }

    private static void SeedPostedLine(ApplicationDbContext db, int accountId, decimal debitUsd, string currency, decimal sourceAmount)
    {
        db.JournalEntries.Add(new JournalEntry
        {
            Id = ++_lineSeq, CompanyId = 1, FiscalYearId = 1, FiscalPeriodId = 1,
            JournalNumber = $"SETUP-{_lineSeq}", Status = JournalEntryStatus.Posted, PostedAt = DateTime.UtcNow,
            AccountingDate = new DateTime(2025, 6, 1), SourceModule = "Purchase",
            Lines =
            [
                new() { LineNumber = 1, AccountId = accountId, Debit = debitUsd, TransactionCurrencyCode = currency,
                    TransactionAmount = sourceAmount, ExchangeRate = decimal.Round(debitUsd / sourceAmount, 8) },
                new() { LineNumber = 2, AccountId = RevenueId, Credit = debitUsd, TransactionCurrencyCode = "USD",
                    TransactionAmount = debitUsd, ExchangeRate = 1m }
            ]
        });
    }
}
