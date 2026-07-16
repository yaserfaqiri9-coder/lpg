using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PTGOilSystem.Web.Configuration;
using PTGOilSystem.Web.Data;
using PTGOilSystem.Web.Models.Entities;

namespace PTGOilSystem.Web.Services.Accounting;

public interface IClosingChecklistService
{
    /// <summary>
    /// چک‌لیستِ بستنِ سال برای یک شرکت/سالِ مشخص. اگر سال متعلق به شرکت نباشد یا وجود نداشته باشد،
    /// null برمی‌گرداند. اجرای این متد هیچ چیزی را تغییر نمی‌دهد.
    /// </summary>
    Task<ClosingChecklistReport?> BuildAsync(
        int companyId,
        int fiscalYearId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// مرحله ۱۲ — چک‌لیستِ بستنِ سال. **کاملاً فقط‌خواندنی**: نه Journal، نه Entity، نه Flag، نه
/// Migration، نه Posting. هر کنترل یا از دیتابیس اثبات می‌شود یا صریحاً Warning/NotApplicable
/// می‌خورد؛ هیچ عددی جعل نمی‌شود.
///
/// این سرویس برای هر (Company, FiscalYear) مستقل اجرا می‌شود چون تنظیمات، حساب‌ها، دوره‌ها و
/// اسناد همه per-company و per-year هستند.
/// </summary>
public sealed class ClosingChecklistService(
    ApplicationDbContext db,
    IOptions<AccountingOptions> options,
    IAccountingReadinessService readiness) : IClosingChecklistService
{
    private readonly AccountingOptions _options = options.Value;

    public async Task<ClosingChecklistReport?> BuildAsync(
        int companyId,
        int fiscalYearId,
        CancellationToken cancellationToken = default)
    {
        var year = await db.FiscalYears.AsNoTracking()
            .Where(y => y.Id == fiscalYearId && y.CompanyId == companyId)
            .Select(y => new { y.Id, y.CompanyId, y.Name, y.StartDate, y.EndDate, y.Status })
            .SingleOrDefaultAsync(cancellationToken);

        if (year is null)
            return null;

        var companyName = await db.Companies.AsNoTracking()
            .Where(c => c.Id == companyId)
            .Select(c => c.Name)
            .SingleOrDefaultAsync(cancellationToken) ?? $"Company {companyId}";

        var checks = new List<ClosingCheckResult>();

        var settings = await db.AccountingSettings.AsNoTracking()
            .SingleOrDefaultAsync(s => s.CompanyId == companyId, cancellationToken);

        AddSettingsCheck(checks, companyId, fiscalYearId, settings);
        if (settings is not null)
            await AddRequiredAccountsCheckAsync(checks, companyId, fiscalYearId, settings, cancellationToken);

        await AddFiscalYearValidityCheckAsync(checks, companyId, year.Id, year.StartDate, year.EndDate, cancellationToken);
        await AddPeriodCoverageCheckAsync(checks, companyId, year.Id, year.StartDate, year.EndDate, cancellationToken);
        await AddUnbalancedJournalCheckAsync(checks, companyId, year.Id, cancellationToken);
        await AddInconsistentJournalStateCheckAsync(checks, companyId, year.Id, cancellationToken);
        await AddDuplicateSourceEventCheckAsync(checks, companyId, year.Id, cancellationToken);
        await AddPendingCostCheckAsync(checks, companyId, year.Id, cancellationToken);
        await AddNegativeInventoryCheckAsync(checks, companyId, year.Id, cancellationToken);
        await AddInventoryConsistencyCheckAsync(checks, companyId, year.Id, cancellationToken);
        await AddMonetaryTreatmentCheckAsync(checks, companyId, year.Id, cancellationToken);
        await AddTransferCompletenessCheckAsync(checks, companyId, year.Id, cancellationToken);
        AddInventoryInTransitCheck(checks, companyId, year.Id, settings);
        await AddExpenseTypeCheckAsync(checks, companyId, year.Id, cancellationToken);
        await AddCashAccountOwnershipCheckAsync(checks, companyId, year.Id, cancellationToken);
        await AddPaymentOwnershipCheckAsync(checks, companyId, year.Id, cancellationToken);
        await AddCustomerAdvanceCheckAsync(checks, companyId, year.Id, cancellationToken);
        AddFeatureFlagCheck(checks, companyId, year.Id);
        await AddPendingMigrationsCheckAsync(checks, companyId, year.Id, cancellationToken);
        await AddReadinessCheckAsync(checks, companyId, year.Id, cancellationToken);
        await AddOpenPeriodsCheckAsync(checks, companyId, year.Id, year.Status, cancellationToken);

        var summary = await AddYearBalanceChecksAsync(checks, companyId, year.Id, cancellationToken);

        AddRevaluationPendingCheck(checks, companyId, year.Id);
        AddExternalEvidenceChecks(checks, companyId, year.Id);

        var overall = Aggregate(checks);

        return new ClosingChecklistReport(
            DateTime.UtcNow,
            companyId,
            companyName,
            year.Id,
            year.Name,
            year.StartDate,
            year.EndDate,
            overall,
            summary,
            checks);
    }

    // بدترین وضعیت گزارش را تعیین می‌کند: یک Blocked کل چک‌لیست را Blocked می‌کند.
    private static ClosingCheckStatus Aggregate(IReadOnlyList<ClosingCheckResult> checks)
    {
        if (checks.Any(c => c.Status == ClosingCheckStatus.Blocked))
            return ClosingCheckStatus.Blocked;
        if (checks.Any(c => c.Status == ClosingCheckStatus.Warning))
            return ClosingCheckStatus.Warning;
        return ClosingCheckStatus.Passed;
    }

    private static ClosingCheckResult Result(
        string code,
        ClosingCheckStatus status,
        string title,
        string description,
        int companyId,
        int fiscalYearId,
        int recordCount = 0,
        IReadOnlyList<string>? samples = null,
        string requiredAction = "",
        string? featureFlag = null,
        string? link = null)
        => new(code, status, title, description, companyId, fiscalYearId, recordCount,
            samples ?? Array.Empty<string>(), requiredAction, featureFlag, link);

    private static IReadOnlyList<string> Sample(IEnumerable<string> values)
        => values.Take(ClosingCheckResult.MaxSamples).ToList();

    // ۱ — AccountingSettings کامل و معتبر.
    private static void AddSettingsCheck(
        List<ClosingCheckResult> checks, int companyId, int fiscalYearId, AccountingSettings? settings)
    {
        if (settings is null)
        {
            checks.Add(Result("ACCOUNTING_SETTINGS_MISSING", ClosingCheckStatus.Blocked,
                "AccountingSettings وجود ندارد",
                "بدون تنظیمات حسابداری هیچ Mapping حسابی وجود ندارد و سال قابل بستن نیست.",
                companyId, fiscalYearId, 1,
                requiredAction: "AccountingSettings این شرکت ساخته و حساب‌هایش تنظیم شود."));
            return;
        }

        if (!string.Equals(settings.FunctionalCurrencyCode?.Trim(), "USD", StringComparison.OrdinalIgnoreCase))
        {
            checks.Add(Result("UNSUPPORTED_FUNCTIONAL_CURRENCY", ClosingCheckStatus.Blocked,
                "ارز عملیاتی USD نیست",
                $"ارز عملیاتی «{settings.FunctionalCurrencyCode}» است؛ منطق بستن و تسعیر فقط USD را می‌شناسد.",
                companyId, fiscalYearId, 1,
                requiredAction: "ارز عملیاتی به USD اصلاح شود."));
            return;
        }

        checks.Add(Result("ACCOUNTING_SETTINGS_VALID", ClosingCheckStatus.Passed,
            "تنظیمات حسابداری معتبر است",
            "AccountingSettings موجود است و ارز عملیاتی USD است.",
            companyId, fiscalYearId));
    }

    private static IReadOnlyList<(string Name, int AccountId)> RequiredAccountIds(AccountingSettings s)
        => new (string, int)[]
        {
            (nameof(s.CashBankControlAccountId), s.CashBankControlAccountId),
            (nameof(s.AccountsReceivableAccountId), s.AccountsReceivableAccountId),
            (nameof(s.AccountsPayableAccountId), s.AccountsPayableAccountId),
            (nameof(s.InventoryAccountId), s.InventoryAccountId),
            (nameof(s.InventoryInTransitAccountId), s.InventoryInTransitAccountId),
            (nameof(s.SupplierPrepaymentAccountId), s.SupplierPrepaymentAccountId),
            (nameof(s.CustomerAdvanceAccountId), s.CustomerAdvanceAccountId),
            (nameof(s.FreightPayableAccountId), s.FreightPayableAccountId),
            (nameof(s.CommissionPayableAccountId), s.CommissionPayableAccountId),
            (nameof(s.EmployeeAdvanceAccountId), s.EmployeeAdvanceAccountId),
            (nameof(s.EmployeePayableAccountId), s.EmployeePayableAccountId),
            (nameof(s.AccruedExpenseAccountId), s.AccruedExpenseAccountId),
            (nameof(s.SalesRevenueAccountId), s.SalesRevenueAccountId),
            (nameof(s.CostOfGoodsSoldAccountId), s.CostOfGoodsSoldAccountId),
            (nameof(s.GeneralExpenseAccountId), s.GeneralExpenseAccountId),
            (nameof(s.ExchangeGainAccountId), s.ExchangeGainAccountId),
            (nameof(s.ExchangeLossAccountId), s.ExchangeLossAccountId),
            (nameof(s.InventoryLossAccountId), s.InventoryLossAccountId),
            (nameof(s.CurrentYearProfitLossAccountId), s.CurrentYearProfitLossAccountId),
            (nameof(s.RetainedEarningsAccountId), s.RetainedEarningsAccountId)
        };

    // ۲ — حساب‌های اجباری موجود، فعال و متعلق به همین شرکت.
    private async Task AddRequiredAccountsCheckAsync(
        List<ClosingCheckResult> checks, int companyId, int fiscalYearId,
        AccountingSettings settings, CancellationToken cancellationToken)
    {
        var required = RequiredAccountIds(settings);
        var requiredIds = required.Select(r => r.AccountId).Distinct().ToList();
        var accounts = await db.Accounts.AsNoTracking()
            .Where(a => requiredIds.Contains(a.Id))
            .Select(a => new { a.Id, a.Code, a.CompanyId, a.IsActive })
            .ToListAsync(cancellationToken);

        var broken = required
            .Where(r =>
            {
                var acc = accounts.FirstOrDefault(a => a.Id == r.AccountId);
                return acc is null || !acc.IsActive || acc.CompanyId != companyId;
            })
            .Select(r =>
            {
                var acc = accounts.FirstOrDefault(a => a.Id == r.AccountId);
                var reason = acc is null ? "MISSING" : !acc.IsActive ? "INACTIVE" : "WRONG_COMPANY";
                return $"{r.Name} → AccountId={r.AccountId}, {reason}";
            })
            .ToList();

        checks.Add(broken.Count == 0
            ? Result("REQUIRED_ACCOUNTS_VALID", ClosingCheckStatus.Passed,
                "همه حساب‌های اجباری معتبرند",
                "هر ۲۰ حساب لازم موجود، فعال و متعلق به همین شرکت‌اند.",
                companyId, fiscalYearId)
            : Result("REQUIRED_ACCOUNT_INVALID", ClosingCheckStatus.Blocked,
                "حساب اجباری ناقص است",
                "یک یا چند حساب لازم گم‌شده، غیرفعال یا متعلق به شرکت دیگری است.",
                companyId, fiscalYearId, broken.Count, Sample(broken),
                "ارجاع‌های تنظیمات و مالکیت/فعال‌بودن حساب‌ها اصلاح شوند."));
    }

    // ۳ — سال مالی معتبر و بدون هم‌پوشانی.
    private async Task AddFiscalYearValidityCheckAsync(
        List<ClosingCheckResult> checks, int companyId, int fiscalYearId,
        DateTime start, DateTime end, CancellationToken cancellationToken)
    {
        var problems = new List<string>();
        if (start.Date >= end.Date)
            problems.Add($"StartDate={start:yyyy-MM-dd} >= EndDate={end:yyyy-MM-dd}");

        var overlaps = await db.FiscalYears.AsNoTracking()
            .Where(y => y.CompanyId == companyId && y.Id != fiscalYearId
                && y.StartDate <= end && y.EndDate >= start)
            .Select(y => new { y.Id, y.Name, y.StartDate, y.EndDate })
            .ToListAsync(cancellationToken);
        problems.AddRange(overlaps.Select(o =>
            $"OverlapWith FiscalYearId={o.Id}, Name={o.Name}, {o.StartDate:yyyy-MM-dd}..{o.EndDate:yyyy-MM-dd}"));

        checks.Add(problems.Count == 0
            ? Result("FISCAL_YEAR_VALID", ClosingCheckStatus.Passed,
                "سال مالی معتبر و بدون هم‌پوشانی است",
                "بازهٔ سال معتبر است و با هیچ سال دیگری هم‌پوشانی ندارد.",
                companyId, fiscalYearId)
            : Result("FISCAL_YEAR_INVALID", ClosingCheckStatus.Blocked,
                "سال مالی نامعتبر یا هم‌پوشان",
                "بازهٔ سال نامعتبر است یا با سال دیگری هم‌پوشانی دارد.",
                companyId, fiscalYearId, problems.Count, Sample(problems),
                "بازهٔ سال یا سال‌های هم‌پوشان اصلاح شوند."));
    }

    // ۴ — دوره‌ها تمام سال را بدون فاصله و بدون هم‌پوشانی پوشش دهند.
    private async Task AddPeriodCoverageCheckAsync(
        List<ClosingCheckResult> checks, int companyId, int fiscalYearId,
        DateTime start, DateTime end, CancellationToken cancellationToken)
    {
        var periods = await db.FiscalPeriods.AsNoTracking()
            .Where(p => p.FiscalYearId == fiscalYearId)
            .OrderBy(p => p.StartDate).ThenBy(p => p.PeriodNumber)
            .Select(p => new { p.Id, p.PeriodNumber, p.Name, p.StartDate, p.EndDate, p.CompanyId })
            .ToListAsync(cancellationToken);

        var problems = new List<string>();
        if (periods.Count == 0)
        {
            problems.Add("NoPeriods");
        }
        else
        {
            if (periods[0].StartDate.Date != start.Date)
                problems.Add($"FirstPeriodStart={periods[0].StartDate:yyyy-MM-dd} != YearStart={start:yyyy-MM-dd}");
            if (periods[^1].EndDate.Date != end.Date)
                problems.Add($"LastPeriodEnd={periods[^1].EndDate:yyyy-MM-dd} != YearEnd={end:yyyy-MM-dd}");

            for (var i = 1; i < periods.Count; i++)
            {
                var expected = periods[i - 1].EndDate.Date.AddDays(1);
                if (periods[i].StartDate.Date != expected)
                {
                    problems.Add(periods[i].StartDate.Date > expected
                        ? $"Gap between Period {periods[i - 1].PeriodNumber} and {periods[i].PeriodNumber}"
                        : $"Overlap between Period {periods[i - 1].PeriodNumber} and {periods[i].PeriodNumber}");
                }
            }

            problems.AddRange(periods.Where(p => p.CompanyId != companyId)
                .Select(p => $"Period {p.PeriodNumber} belongs to CompanyId={p.CompanyId}"));
        }

        checks.Add(problems.Count == 0
            ? Result("PERIOD_COVERAGE_COMPLETE", ClosingCheckStatus.Passed,
                "دوره‌ها کل سال را پوشش می‌دهند",
                $"{periods.Count} دوره کل سال را بدون فاصله و هم‌پوشانی پوشش می‌دهند.",
                companyId, fiscalYearId, periods.Count)
            : Result("PERIOD_COVERAGE_INCOMPLETE", ClosingCheckStatus.Blocked,
                "پوشش دوره‌ها ناقص یا هم‌پوشان",
                "دوره‌ها کل سال را بدون فاصله و هم‌پوشانی پوشش نمی‌دهند.",
                companyId, fiscalYearId, problems.Count, Sample(problems),
                "دوره‌های سال بازبینی و اصلاح شوند."));
    }

    // ۵ — هیچ سند نامتوازن.
    private async Task AddUnbalancedJournalCheckAsync(
        List<ClosingCheckResult> checks, int companyId, int fiscalYearId, CancellationToken cancellationToken)
    {
        var unbalanced = await db.JournalEntries.AsNoTracking()
            .Where(j => j.CompanyId == companyId && j.FiscalYearId == fiscalYearId)
            .Select(j => new
            {
                j.Id,
                j.JournalNumber,
                Debit = j.Lines.Sum(l => (decimal?)l.Debit) ?? 0m,
                Credit = j.Lines.Sum(l => (decimal?)l.Credit) ?? 0m
            })
            .Where(j => j.Debit != j.Credit)
            .ToListAsync(cancellationToken);

        checks.Add(unbalanced.Count == 0
            ? Result("NO_UNBALANCED_JOURNAL", ClosingCheckStatus.Passed,
                "هیچ سند نامتوازنی وجود ندارد",
                "جمع بدهکار و بستانکار همهٔ اسناد این سال برابر است.",
                companyId, fiscalYearId)
            : Result("UNBALANCED_JOURNAL", ClosingCheckStatus.Blocked,
                "سند نامتوازن",
                "جمع بدهکار و بستانکار برابر نیست؛ هیچ سندی نباید در این وضعیت باشد.",
                companyId, fiscalYearId, unbalanced.Count,
                Sample(unbalanced.Select(j => $"JournalEntryId={j.Id}, Number={j.JournalNumber}, Debit={j.Debit}, Credit={j.Credit}")),
                "مسیر ساختِ این اسناد بررسی و با Reversal اصلاح شوند."));
    }

    // ۶ — وضعیت‌های ناسازگارِ سند: Draft، Posted بدون PostedAt/Line، Draft با PostedAt.
    private async Task AddInconsistentJournalStateCheckAsync(
        List<ClosingCheckResult> checks, int companyId, int fiscalYearId, CancellationToken cancellationToken)
    {
        var yearJournals = db.JournalEntries.AsNoTracking()
            .Where(j => j.CompanyId == companyId && j.FiscalYearId == fiscalYearId);

        var problems = new List<string>();

        var drafts = await yearJournals
            .Where(j => j.Status == JournalEntryStatus.Draft && j.PostedAt == null)
            .Select(j => new { j.Id, j.JournalNumber })
            .Take(ClosingCheckResult.MaxSamples).ToListAsync(cancellationToken);
        var draftCount = await yearJournals.CountAsync(
            j => j.Status == JournalEntryStatus.Draft && j.PostedAt == null, cancellationToken);
        problems.AddRange(drafts.Select(j => $"DRAFT JournalEntryId={j.Id}, Number={j.JournalNumber}"));

        var postedNoStamp = await yearJournals
            .Where(j => j.Status == JournalEntryStatus.Posted && j.PostedAt == null)
            .Select(j => $"POSTED_NO_POSTED_AT JournalEntryId={j.Id}")
            .Take(ClosingCheckResult.MaxSamples).ToListAsync(cancellationToken);
        problems.AddRange(postedNoStamp);

        var postedNoLines = await yearJournals
            .Where(j => j.Status == JournalEntryStatus.Posted && j.Lines.Count == 0)
            .Select(j => $"POSTED_NO_LINES JournalEntryId={j.Id}")
            .Take(ClosingCheckResult.MaxSamples).ToListAsync(cancellationToken);
        problems.AddRange(postedNoLines);

        var draftStamped = await yearJournals
            .Where(j => j.Status == JournalEntryStatus.Draft && j.PostedAt != null)
            .Select(j => $"DRAFT_WITH_POSTED_AT JournalEntryId={j.Id}")
            .Take(ClosingCheckResult.MaxSamples).ToListAsync(cancellationToken);
        problems.AddRange(draftStamped);

        var totalBad = draftCount + postedNoStamp.Count + postedNoLines.Count + draftStamped.Count;

        checks.Add(totalBad == 0
            ? Result("NO_INCONSISTENT_JOURNAL_STATE", ClosingCheckStatus.Passed,
                "هیچ سند با وضعیت ناسازگار وجود ندارد",
                "هیچ سندِ Draft، Posted بدون PostedAt/Line، یا Draft با PostedAt در این سال نمانده.",
                companyId, fiscalYearId)
            : Result("INCONSISTENT_JOURNAL_STATE", ClosingCheckStatus.Blocked,
                "سند با وضعیت ناسازگار",
                "سندِ Draft یا سندی با وضعیت و مهرِ زمانیِ ناسازگار مانده که باید پیش از بستن حل شود.",
                companyId, fiscalYearId, totalBad, Sample(problems),
                "این اسناد نهایی یا حذف/برگردانده شوند تا سال قابل بستن باشد."));
    }

    // ۷ — SourceEventId تکراری.
    private async Task AddDuplicateSourceEventCheckAsync(
        List<ClosingCheckResult> checks, int companyId, int fiscalYearId, CancellationToken cancellationToken)
    {
        var duplicates = await db.JournalEntries.AsNoTracking()
            .Where(j => j.CompanyId == companyId && j.FiscalYearId == fiscalYearId && j.SourceEventId != null)
            .GroupBy(j => j.SourceEventId)
            .Where(g => g.Count() > 1)
            .Select(g => new { SourceEventId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        checks.Add(duplicates.Count == 0
            ? Result("NO_DUPLICATE_SOURCE_EVENT", ClosingCheckStatus.Passed,
                "هیچ SourceEventId تکراری وجود ندارد",
                "هر رویداد دقیقاً یک سند دارد.",
                companyId, fiscalYearId)
            : Result("DUPLICATE_SOURCE_EVENT", ClosingCheckStatus.Blocked,
                "SourceEventId تکراری",
                "تکراری‌بودن یعنی Idempotency شکسته و یک رویداد دوبار ثبت شده.",
                companyId, fiscalYearId, duplicates.Count,
                Sample(duplicates.Select(d => $"SourceEventId={d.SourceEventId}, Count={d.Count}")),
                "اسناد تکراری با Reversal اصلاح شوند؛ رکورد Posted ویرایش نمی‌شود."));
    }

    // ۸ و ۹ — فروش با PendingCost/INVENTORY_NOT_VALUED یا فروش درآمددار بدون COGS.
    private async Task AddPendingCostCheckAsync(
        List<ClosingCheckResult> checks, int companyId, int fiscalYearId, CancellationToken cancellationToken)
    {
        var salesCount = await db.SalesTransactions.AsNoTracking()
            .CountAsync(s => s.CompanyId == companyId, cancellationToken);
        if (salesCount == 0)
        {
            checks.Add(Result("PENDING_COST_SALES", ClosingCheckStatus.NotApplicable,
                "فروشی برای این شرکت ثبت نشده",
                "هیچ فروشی وجود ندارد، پس کنترل بهای تمام‌شده موضوعیت ندارد.",
                companyId, fiscalYearId, featureFlag: "Accounting:Pilots:Cogs"));
            return;
        }

        if (!_options.Enabled || !_options.Pilots.Cogs)
        {
            checks.Add(Result("SALES_COST_NOT_EVALUATED", ClosingCheckStatus.Warning,
                "بهای تمام‌شدهٔ فروش هنوز ارزیابی نشده",
                $"{salesCount} فروش هست ولی Flag مربوط به Cogs خاموش است؛ وضعیت واقعی "
                    + "PendingCost/INVENTORY_NOT_VALUED بدون اجرای عملیاتی معلوم نیست.",
                companyId, fiscalYearId, salesCount,
                requiredAction: "روی Backup عملیاتی با Flagهای Sale/Cogs روشن اجرا و لاگ بررسی شود.",
                featureFlag: "Accounting:Pilots:Cogs"));
            return;
        }

        var costed = await db.JournalEntries.AsNoTracking()
            .Where(j => j.CompanyId == companyId
                && j.SourceModule == SalesAccountingAdapter.SourceModule
                && j.SourceEventId != null && j.SourceEventId.Contains("Cogs"))
            .Select(j => j.SourceEntityId)
            .Distinct().ToListAsync(cancellationToken);

        var uncosted = await db.SalesTransactions.AsNoTracking()
            .Where(s => s.CompanyId == companyId && !costed.Contains(s.Id))
            .OrderBy(s => s.Id)
            .Select(s => new { s.Id, s.ProductId, s.QuantityMt })
            .ToListAsync(cancellationToken);

        checks.Add(uncosted.Count == 0
            ? Result("NO_PENDING_COST_SALES", ClosingCheckStatus.Passed,
                "همهٔ فروش‌ها بهای تمام‌شده گرفته‌اند",
                "هیچ فروشِ درآمددارِ بدون سند COGS باقی نمانده.",
                companyId, fiscalYearId)
            : Result("SALE_WITHOUT_COGS_JOURNAL", ClosingCheckStatus.Blocked,
                "فروش بدون سند بهای تمام‌شده",
                "این فروش‌ها سند COGS نگرفته‌اند — Skip خورده‌اند (معمولاً INVENTORY_NOT_VALUED).",
                companyId, fiscalYearId, uncosted.Count,
                Sample(uncosted.Select(s => $"SalesTransactionId={s.Id}, ProductId={s.ProductId}, Qty={s.QuantityMt}")),
                "منشأ Skip بررسی و موجودی ارزش‌گذاری شود.", "Accounting:Pilots:Cogs"));
    }

    // ۱۰ — موجودی منفی.
    private async Task AddNegativeInventoryCheckAsync(
        List<ClosingCheckResult> checks, int companyId, int fiscalYearId, CancellationToken cancellationToken)
    {
        var negative = await db.InventoryAverageCosts.AsNoTracking()
            .Where(p => p.CompanyId == companyId && (p.QuantityMt < 0m || p.TotalValueUsd < 0m))
            .Select(p => new { p.Id, p.ProductId, p.TerminalId, p.QuantityMt, p.TotalValueUsd })
            .ToListAsync(cancellationToken);

        checks.Add(negative.Count == 0
            ? Result("NO_NEGATIVE_INVENTORY", ClosingCheckStatus.Passed,
                "هیچ موجودیِ منفی وجود ندارد",
                "هیچ Pool ارزش‌گذاری مقدار یا ارزشِ منفی ندارد.",
                companyId, fiscalYearId)
            : Result("NEGATIVE_INVENTORY", ClosingCheckStatus.Blocked,
                "موجودی منفی",
                "مقدار یا ارزشِ منفی یعنی برداشت بیش از موجودی ثبت شده.",
                companyId, fiscalYearId, negative.Count,
                Sample(negative.Select(p => $"PoolId={p.Id}, ProductId={p.ProductId}, TerminalId={p.TerminalId}, Qty={p.QuantityMt}, Value={p.TotalValueUsd}")),
                "ترتیب رویدادهای موجودی روی داده بررسی شود.", "Accounting:Pilots:Cogs"));
    }

    // ۱۱ و ۱۲ — Pool ارزش‌گذاری ناسازگار: مقدار بدون ارزش یا ارزش بدون مقدار.
    private async Task AddInventoryConsistencyCheckAsync(
        List<ClosingCheckResult> checks, int companyId, int fiscalYearId, CancellationToken cancellationToken)
    {
        var pools = await db.InventoryAverageCosts.AsNoTracking()
            .Where(p => p.CompanyId == companyId)
            .Select(p => new { p.Id, p.ProductId, p.TerminalId, p.QuantityMt, p.TotalValueUsd })
            .ToListAsync(cancellationToken);

        var inconsistent = pools
            .Where(p => (p.QuantityMt > 0m && p.TotalValueUsd <= 0m) || (p.TotalValueUsd > 0m && p.QuantityMt <= 0m))
            .ToList();

        checks.Add(inconsistent.Count == 0
            ? Result("INVENTORY_POOL_CONSISTENT", ClosingCheckStatus.Passed,
                "Poolهای ارزش‌گذاری سازگارند",
                "هیچ Poolی مقدار بدون ارزش یا ارزش بدون مقدار ندارد.",
                companyId, fiscalYearId)
            : Result("INVENTORY_POOL_INCONSISTENT", ClosingCheckStatus.Blocked,
                "Pool ارزش‌گذاری ناسازگار",
                "مقدار بدون ارزش یا ارزش بدون مقدار، میانگین متحرک را بی‌معنی می‌کند.",
                companyId, fiscalYearId, inconsistent.Count,
                Sample(inconsistent.Select(p => $"PoolId={p.Id}, ProductId={p.ProductId}, TerminalId={p.TerminalId}, Qty={p.QuantityMt}, Value={p.TotalValueUsd}")),
                "منشأ این Poolها بررسی شود.", "Accounting:Pilots:Cogs"));
    }

    // مرحله ۱۳ — حساب‌های دارای فعالیتِ Posted که طبقه‌بندیِ پولی/غیرپولی‌شان Unspecified است.
    // بدون طبقه‌بندیِ صریح، تسعیر نمی‌داند کدام حساب پولی است و نباید حدس بزند — پس Blocked.
    private async Task AddMonetaryTreatmentCheckAsync(
        List<ClosingCheckResult> checks, int companyId, int fiscalYearId, CancellationToken cancellationToken)
    {
        var unclassified = await db.Accounts.AsNoTracking()
            .Where(a => a.CompanyId == companyId && a.IsActive
                && a.MonetaryTreatment == MonetaryTreatment.Unspecified
                && db.JournalEntryLines.Any(l => l.AccountId == a.Id
                    && l.JournalEntry!.FiscalYearId == fiscalYearId
                    && l.JournalEntry.Status == JournalEntryStatus.Posted))
            .OrderBy(a => a.Code)
            .Select(a => new { a.Id, a.Code, a.Name })
            .ToListAsync(cancellationToken);

        checks.Add(unclassified.Count == 0
            ? Result("MONETARY_TREATMENT_CLASSIFIED", ClosingCheckStatus.Passed,
                "طبقه‌بندیِ پولی همه حساب‌های فعال مشخص است",
                "هیچ حسابِ دارای فعالیتِ Posted با طبقه‌بندیِ Unspecified نمانده.",
                companyId, fiscalYearId)
            : Result("MONETARY_TREATMENT_UNSPECIFIED", ClosingCheckStatus.Blocked,
                "حساب بدون طبقه‌بندیِ پولی/غیرپولی",
                "طبقه‌بندیِ Monetary/NonMonetary این حساب‌ها مشخص نیست؛ تسعیر پایان دوره بدون آن ممکن نیست "
                    + "و از شماره/نام حساب حدس زده نمی‌شود.",
                companyId, fiscalYearId, unclassified.Count,
                Sample(unclassified.Select(a => $"AccountId={a.Id}, Code={a.Code}, Name={a.Name}")),
                "MonetaryTreatment هر حساب (Monetary یا NonMonetary) با تصمیم صریح تعیین شود."));
    }

    // ۱۳ — انتقال بین ترمینال‌ها کامل باشد (بها منتقل شده باشد).
    private async Task AddTransferCompletenessCheckAsync(
        List<ClosingCheckResult> checks, int companyId, int fiscalYearId, CancellationToken cancellationToken)
    {
        var transferLegCount = await db.InventoryTransportLegs.AsNoTracking()
            .CountAsync(l => l.SourcePurchaseContract!.CompanyId == companyId && l.DestinationTerminalId != null,
                cancellationToken);
        if (transferLegCount == 0)
        {
            checks.Add(Result("TRANSFER_COMPLETE", ClosingCheckStatus.NotApplicable,
                "انتقال بین‌ترمینالی وجود ندارد",
                "هیچ حملِ بین‌ترمینالی برای این شرکت ثبت نشده.",
                companyId, fiscalYearId, featureFlag: "Accounting:Pilots:InventoryTransfer"));
            return;
        }

        if (!_options.Enabled || !_options.Pilots.InventoryTransfer)
        {
            checks.Add(Result("TRANSFER_COST_NOT_MOVED", ClosingCheckStatus.Warning,
                "انتقال بین‌ترمینالی بدون انتقال بها",
                $"{transferLegCount} حملِ بین‌ترمینالی هست و Flag مربوطه خاموش است؛ بهای هیچ‌کدام "
                    + "بین Poolها جابه‌جا نشده.",
                companyId, fiscalYearId, transferLegCount,
                requiredAction: "Flag InventoryTransfer روی Backup اعتبارسنجی و روشن شود.",
                featureFlag: "Accounting:Pilots:InventoryTransfer"));
            return;
        }

        var postedLegIds = await db.JournalEntries.AsNoTracking()
            .Where(j => j.CompanyId == companyId
                && j.SourceModule == InventoryTransferAccountingAdapter.SourceModule
                && j.SourceEntityId != null)
            .Select(j => j.SourceEntityId!.Value)
            .Distinct().ToListAsync(cancellationToken);

        var unmoved = await db.InventoryTransportLegs.AsNoTracking()
            .Where(l => l.SourcePurchaseContract!.CompanyId == companyId
                && l.DestinationTerminalId != null && !postedLegIds.Contains(l.Id))
            .OrderBy(l => l.Id)
            .Select(l => new { l.Id, l.SourceTerminalId, l.DestinationTerminalId })
            .ToListAsync(cancellationToken);

        checks.Add(unmoved.Count == 0
            ? Result("TRANSFER_COMPLETE", ClosingCheckStatus.Passed,
                "انتقال بین‌ترمینالی کامل است",
                "همهٔ حمل‌های بین‌ترمینالی سند انتقال بها گرفته‌اند.",
                companyId, fiscalYearId, transferLegCount)
            : Result("TRANSFER_LEG_WITHOUT_COST_JOURNAL", ClosingCheckStatus.Blocked,
                "حمل بین‌ترمینالی بدون سند انتقال بها",
                "این legها سند انتقال بها ندارند — Skip خورده‌اند.",
                companyId, fiscalYearId, unmoved.Count,
                Sample(unmoved.Select(l => $"LegId={l.Id}, Source={l.SourceTerminalId}, Dest={l.DestinationTerminalId}")),
                "لاگِ انتقال بررسی شود.", "Accounting:Pilots:InventoryTransfer"));
    }

    // ۱۴ و ۱۵ — مانده ۱۳۱۰ کالای در راه با Legها و رسیدهای باز قابل توضیح باشد.
    private void AddInventoryInTransitCheck(
        List<ClosingCheckResult> checks, int companyId, int fiscalYearId, AccountingSettings? settings)
    {
        // ماندهٔ ۱۳۱۰ عمداً از داده‌ی عملیاتی (Legهای باز، رسیدهای صفر، DirectSale) توضیح داده
        // می‌شود که در این repo قابل اثبات قطعی نیست؛ پس هرگز Passed جعل نمی‌شود.
        checks.Add(Result("INVENTORY_IN_TRANSIT_1310", ClosingCheckStatus.Warning,
            "ماندهٔ ۱۳۱۰ کالای در راه باید با Legها و رسیدهای باز توضیح داده شود",
            "مانده حساب ۱۳۱۰ باید با legهای هنوز در راه و رسیدهای باز خوانا باشد. DirectSale یا رسید صفر "
                + "می‌تواند ماندهٔ بی‌توجیه بسازد. این توضیح به داده‌ی عملیاتی نیاز دارد و اینجا جعل نمی‌شود.",
            companyId, fiscalYearId,
            requiredAction: "ماندهٔ ۱۳۱۰ روی Backup با legهای باز و رسیدهای صفر تطبیق داده شود.",
            featureFlag: "Accounting:Pilots:InventoryReceipt",
            link: settings is null ? null : $"InventoryInTransitAccountId={settings.InventoryInTransitAccountId}"));
    }

    // ۱۷ — ExpenseType بدون PayableAccountKind.
    private async Task AddExpenseTypeCheckAsync(
        List<ClosingCheckResult> checks, int companyId, int fiscalYearId, CancellationToken cancellationToken)
    {
        var count = await db.ExpenseTypes.AsNoTracking().CountAsync(e => e.PayableAccountKind == null, cancellationToken);
        if (count == 0)
        {
            checks.Add(Result("EXPENSE_TYPE_PAYABLE_KIND_OK", ClosingCheckStatus.Passed,
                "همه ExpenseTypeها PayableAccountKind دارند",
                "هیچ ExpenseType بدون نوعِ حسابِ بدهی نمانده.",
                companyId, fiscalYearId));
            return;
        }

        var samples = await db.ExpenseTypes.AsNoTracking()
            .Where(e => e.PayableAccountKind == null)
            .OrderBy(e => e.Code)
            .Select(e => new { e.Id, e.Code, e.Name })
            .Take(ClosingCheckResult.MaxSamples).ToListAsync(cancellationToken);

        checks.Add(Result("EXPENSE_TYPE_PAYABLE_KIND_MISSING", ClosingCheckStatus.Warning,
            "ExpenseType بدون PayableAccountKind",
            "بدون این فیلد هزینه با PAYABLE_KIND_UNKNOWN رد می‌شود (یافتهٔ سراسری).",
            companyId, fiscalYearId, count,
            Sample(samples.Select(e => $"ExpenseTypeId={e.Id}, Code={e.Code}, Name={e.Name}")),
            "نوع بدهی هر ExpenseType با تصمیم صریح تعیین شود.", "Accounting:Pilots:Expense"));
    }

    // ۱۸ — CashAccount بدون Company.
    private async Task AddCashAccountOwnershipCheckAsync(
        List<ClosingCheckResult> checks, int companyId, int fiscalYearId, CancellationToken cancellationToken)
    {
        var count = await db.CashAccounts.AsNoTracking().CountAsync(c => c.CompanyId == null, cancellationToken);
        checks.Add(count == 0
            ? Result("CASH_ACCOUNT_OWNERSHIP_OK", ClosingCheckStatus.Passed,
                "همه حساب‌های نقدی شرکت دارند",
                "هیچ CashAccount بدون شرکت نمانده.",
                companyId, fiscalYearId)
            : Result("CASH_ACCOUNT_WITHOUT_COMPANY", ClosingCheckStatus.Warning,
                "حساب نقدی بدون شرکت",
                "پرداخت‌های روی این حساب‌ها Skip می‌شوند (یافتهٔ سراسری).",
                companyId, fiscalYearId, count,
                requiredAction: "مالکیت شرکت هر حساب نقدی با شواهد تعیین شود. Backfill حدسی ممنوع."));
    }

    // ۱۹ — PaymentTransaction بدون Company.
    private async Task AddPaymentOwnershipCheckAsync(
        List<ClosingCheckResult> checks, int companyId, int fiscalYearId, CancellationToken cancellationToken)
    {
        var count = await db.PaymentTransactions.AsNoTracking().CountAsync(p => p.CompanyId == null, cancellationToken);
        checks.Add(count == 0
            ? Result("PAYMENT_OWNERSHIP_OK", ClosingCheckStatus.Passed,
                "همه پرداخت‌ها شرکت قابل‌اثبات دارند",
                "هیچ پرداختِ بدون شرکت نمانده.",
                companyId, fiscalYearId)
            : Result("PAYMENT_WITHOUT_COMPANY", ClosingCheckStatus.Warning,
                "پرداخت بدون شرکت",
                "پرداختِ بی‌شرکت در دفتر کل جدید نمی‌آید (یافتهٔ سراسری).",
                companyId, fiscalYearId, count,
                requiredAction: "مالکیت با شواهد تعیین شود. Backfill حدسی ممنوع."));
    }

    // ۲۰ — IsCustomerAdvance نامشخص.
    private async Task AddCustomerAdvanceCheckAsync(
        List<ClosingCheckResult> checks, int companyId, int fiscalYearId, CancellationToken cancellationToken)
    {
        var count = await db.PaymentTransactions.AsNoTracking()
            .CountAsync(p => p.PaymentKind == PaymentKind.CustomerReceipt && p.IsCustomerAdvance == null, cancellationToken);
        checks.Add(count == 0
            ? Result("CUSTOMER_ADVANCE_MARKER_OK", ClosingCheckStatus.Passed,
                "همه دریافت‌های مشتری تعیین‌تکلیف شده‌اند",
                "هیچ دریافتِ مشتری با IsCustomerAdvance نامشخص نمانده.",
                companyId, fiscalYearId)
            : Result("CUSTOMER_ADVANCE_MARKER_UNKNOWN", ClosingCheckStatus.Warning,
                "دریافت از مشتری بدون تعیین پیش‌پرداخت‌بودن",
                "معلوم نیست دریافت به «پیش‌دریافت» بنشیند یا طلب را کم کند (یافتهٔ سراسری).",
                companyId, fiscalYearId, count,
                requiredAction: "برای هر دریافت، پیش‌پرداخت‌بودن با تصمیم کاربر مشخص شود.",
                featureFlag: "Accounting:Pilots:CustomerReceipt + CustomerAdvance"));
    }

    // ۲۳ — وضعیت همهٔ Feature Flagهای Accounting.
    private void AddFeatureFlagCheck(List<ClosingCheckResult> checks, int companyId, int fiscalYearId)
    {
        var flags = new (string Name, bool Enabled)[]
        {
            ("Accounting.Enabled", _options.Enabled),
            ("Pilots.ContractBalanceTransfer", _options.Pilots.ContractBalanceTransfer),
            ("Pilots.SupplierPaymentAllocation", _options.Pilots.SupplierPaymentAllocation),
            ("Pilots.CustomerReceipt", _options.Pilots.CustomerReceipt),
            ("Pilots.CustomerAdvance", _options.Pilots.CustomerAdvance),
            ("Pilots.SupplierPayment", _options.Pilots.SupplierPayment),
            ("Pilots.SupplierPrepayment", _options.Pilots.SupplierPrepayment),
            ("Pilots.SarrafPayment", _options.Pilots.SarrafPayment),
            ("Pilots.Expense", _options.Pilots.Expense),
            ("Pilots.ExpensePayment", _options.Pilots.ExpensePayment),
            ("Pilots.CommissionPayment", _options.Pilots.CommissionPayment),
            ("Pilots.Purchase", _options.Pilots.Purchase),
            ("Pilots.InventoryReceipt", _options.Pilots.InventoryReceipt),
            ("Pilots.Sale", _options.Pilots.Sale),
            ("Pilots.Cogs", _options.Pilots.Cogs),
            ("Pilots.InventoryLoss", _options.Pilots.InventoryLoss),
            ("Pilots.ShortageCharge", _options.Pilots.ShortageCharge),
            ("Pilots.SarrafSettlement", _options.Pilots.SarrafSettlement),
            ("Pilots.ThreeWaySettlement", _options.Pilots.ThreeWaySettlement),
            ("Pilots.InventoryTransfer", _options.Pilots.InventoryTransfer)
        };

        var enabledCount = flags.Count(f => f.Enabled);
        checks.Add(Result("FEATURE_FLAGS_STATUS", ClosingCheckStatus.NotApplicable,
            "وضعیت Feature Flagهای حسابداری",
            "این کنترل فقط وضعیت Flagها را نمایش می‌دهد و مانع بستن نیست.",
            companyId, fiscalYearId, enabledCount,
            Sample(flags.Select(f => $"{f.Name}={(f.Enabled ? "ON" : "OFF")}")),
            requiredAction: "Flagها فقط با تصمیم صریح و در زمان Cutover روشن شوند."));
    }

    // ۲۴ — Migrationهای اجرا‌نشده.
    private async Task AddPendingMigrationsCheckAsync(
        List<ClosingCheckResult> checks, int companyId, int fiscalYearId, CancellationToken cancellationToken)
    {
        List<string> pending;
        try
        {
            pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
        }
        catch (Exception ex)
        {
            checks.Add(Result("MIGRATIONS_UNKNOWN", ClosingCheckStatus.NotApplicable,
                "وضعیت Migrationها قابل خواندن نیست",
                $"پرس‌وجوی Migrationهای اجرانشده ممکن نشد: {ex.GetType().Name} (مثلاً Provider درون‌حافظه).",
                companyId, fiscalYearId,
                requiredAction: "این چک‌لیست روی همان دیتابیسِ هدف (یا Backup آن) اجرا شود."));
            return;
        }

        checks.Add(pending.Count == 0
            ? Result("NO_PENDING_MIGRATIONS", ClosingCheckStatus.Passed,
                "هیچ Migration اجرانشده‌ای وجود ندارد",
                "طرحِ دیتابیس با کد هم‌خوان است.",
                companyId, fiscalYearId)
            : Result("MIGRATIONS_PENDING", ClosingCheckStatus.Blocked,
                "Migration اجرانشده وجود دارد",
                $"{pending.Count} Migration روی این دیتابیس اجرا نشده است.",
                companyId, fiscalYearId, pending.Count, Sample(pending),
                "با اجازهٔ صریح و روی Backup تأییدشده اجرا شوند. هرگز خودکار اجرا نشوند."));
    }

    // ۲۵ — AccountingReadiness شرکت Blocked نباشد.
    private async Task AddReadinessCheckAsync(
        List<ClosingCheckResult> checks, int companyId, int fiscalYearId, CancellationToken cancellationToken)
    {
        var report = await readiness.BuildAsync(cancellationToken);
        var company = report.Companies.FirstOrDefault(c => c.CompanyId == companyId);
        var blockers = company?.Findings
            .Where(f => f.Severity == AccountingReadinessSeverity.Blocker)
            .Select(f => f.Code).ToList() ?? new List<string>();

        checks.Add(blockers.Count == 0
            ? Result("READINESS_NOT_BLOCKED", ClosingCheckStatus.Passed,
                "آمادگی حسابداری این شرکت Blocked نیست",
                "AccountingReadiness هیچ Blocker برای این شرکت گزارش نکرده.",
                companyId, fiscalYearId, link: "/accounting/readiness")
            : Result("READINESS_BLOCKED", ClosingCheckStatus.Blocked,
                "آمادگی حسابداری این شرکت Blocked است",
                "تا وقتی Readiness این شرکت Blocker دارد، سال قابل بستن نیست.",
                companyId, fiscalYearId, blockers.Count, Sample(blockers),
                "یافته‌های Blocker در /accounting/readiness برطرف شوند.", link: "/accounting/readiness"));
    }

    // ۲۶ — دوره‌های باز غیرضروری (اطلاع‌رسانی).
    private async Task AddOpenPeriodsCheckAsync(
        List<ClosingCheckResult> checks, int companyId, int fiscalYearId,
        FiscalYearStatus yearStatus, CancellationToken cancellationToken)
    {
        var openPeriods = await db.FiscalPeriods.AsNoTracking()
            .Where(p => p.FiscalYearId == fiscalYearId && p.Status == FiscalPeriodStatus.Open)
            .Select(p => new { p.PeriodNumber, p.Name })
            .ToListAsync(cancellationToken);

        checks.Add(Result("OPEN_PERIODS", ClosingCheckStatus.NotApplicable,
            "دوره‌های باز",
            $"{openPeriods.Count} دورهٔ باز وجود دارد. Final Close همهٔ دوره‌ها را HardLock می‌کند؛ "
                + "دوره‌های باز غیرضروری فقط برای اطلاع گزارش می‌شوند.",
            companyId, fiscalYearId, openPeriods.Count,
            Sample(openPeriods.Select(p => $"Period {p.PeriodNumber} ({p.Name})"))));
    }

    // ۲۷ و ۲۸ — جمع بدهکار/بستانکار کل سال و مانده درآمد/هزینه برای Final Close.
    private async Task<ClosingRevenueExpenseSummary> AddYearBalanceChecksAsync(
        List<ClosingCheckResult> checks, int companyId, int fiscalYearId, CancellationToken cancellationToken)
    {
        var lines = await db.JournalEntryLines.AsNoTracking()
            .Where(l => l.JournalEntry!.CompanyId == companyId
                && l.JournalEntry.FiscalYearId == fiscalYearId
                && l.JournalEntry.Status == JournalEntryStatus.Posted)
            .Select(l => new { l.Debit, l.Credit, l.Account!.AccountType })
            .ToListAsync(cancellationToken);

        var totalDebit = lines.Sum(l => l.Debit);
        var totalCredit = lines.Sum(l => l.Credit);

        checks.Add(totalDebit == totalCredit
            ? Result("YEAR_DEBIT_CREDIT_EQUAL", ClosingCheckStatus.Passed,
                "جمع بدهکار و بستانکار کل سال برابر است",
                $"مجموع بدهکار {totalDebit} و بستانکار {totalCredit} اسنادِ Posted این سال برابر است.",
                companyId, fiscalYearId)
            : Result("YEAR_DEBIT_CREDIT_MISMATCH", ClosingCheckStatus.Blocked,
                "جمع بدهکار و بستانکار کل سال برابر نیست",
                $"مجموع بدهکار {totalDebit} با بستانکار {totalCredit} برابر نیست.",
                companyId, fiscalYearId,
                requiredAction: "اسناد نامتوازن یا ناقص بررسی شوند."));

        // درآمد normal-credit است (credit − debit)، هزینه normal-debit است (debit − credit).
        var revenue = lines.Where(l => l.AccountType == AccountType.Revenue).Sum(l => l.Credit - l.Debit);
        var expense = lines.Where(l => l.AccountType == AccountType.Expense).Sum(l => l.Debit - l.Credit);
        var net = revenue - expense;

        checks.Add(Result("REVENUE_EXPENSE_BALANCES", ClosingCheckStatus.NotApplicable,
            "مانده حساب‌های درآمد و هزینه برای Final Close",
            $"مانده درآمد={revenue} USD، مانده هزینه={expense} USD، سود/زیان خالص={net} USD. "
                + "این ارقام ورودیِ محاسبهٔ Final Close (مرحله ۱۴) هستند.",
            companyId, fiscalYearId,
            samples: new[] { $"Revenue={revenue}", $"Expense={expense}", $"NetProfit={net}" }));

        return new ClosingRevenueExpenseSummary(revenue, expense, net);
    }

    // ۲۹ و ۳۰ — نرخ‌های پایان دوره و تسعیر مرحله ۱۳ تا زمان اجرا Pending.
    private void AddRevaluationPendingCheck(List<ClosingCheckResult> checks, int companyId, int fiscalYearId)
    {
        checks.Add(Result("PERIOD_END_REVALUATION_PENDING", ClosingCheckStatus.Warning,
            "تسعیر پایان دوره هنوز اجرا نشده",
            "نرخ‌های دقیقِ پایان دوره برای ارزهای باز و تسعیر مرحله ۱۳ تا زمان اجرای Trial Close به‌صورت "
                + "Pending هستند. این کنترل تا اجرای مرحله ۱۳ Warning می‌ماند.",
            companyId, fiscalYearId,
            requiredAction: "Trial Close (مرحله ۱۳) اجرا و تسعیر پایان دوره تکمیل شود."));
    }

    // ۳۱ و ۳۲ — Full Suite و شمارش Skipها فقط شواهد بیرونی‌اند؛ Runtime جعل نمی‌شود.
    private static void AddExternalEvidenceChecks(List<ClosingCheckResult> checks, int companyId, int fiscalYearId)
    {
        checks.Add(Result("FULL_SUITE_EXTERNAL_EVIDENCE", ClosingCheckStatus.NotApplicable,
            "نتیجهٔ Full Suite شواهد بیرونی است",
            "نتیجهٔ تست‌ها حالتِ Runtime نیست و از دیتابیس خوانده نمی‌شود؛ به‌صورت دستی/خارجی ضمیمه می‌شود.",
            companyId, fiscalYearId,
            requiredAction: "Full Suite روی Worktree تمیز اجرا و نتیجه ضمیمه شود."));

        checks.Add(Result("SKIP_COUNTS_REQUIRE_LOG_HARVEST", ClosingCheckStatus.NotApplicable,
            "شمارش Skipهای Adapter از لاگ می‌آید",
            "Skipها در دیتابیس ذخیره نمی‌شوند؛ شمارش دقیق فقط از لاگِ یک اجرای واقعی به‌دست می‌آید و عدد تخمینی ساخته نمی‌شود.",
            companyId, fiscalYearId,
            requiredAction: "روی Backup با Flag روشن اجرا و SkipOrFailureReason از لاگ گروه‌بندی شود."));
    }
}
