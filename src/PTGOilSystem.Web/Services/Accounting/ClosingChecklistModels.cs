namespace PTGOilSystem.Web.Services.Accounting;

/// <summary>
/// مرحله ۱۲ — وضعیت هر کنترلِ چک‌لیستِ بستن سال.
///
/// Blocked یعنی چیزی که در همین repo اثبات‌پذیر خراب است و بستن سال را غیرممکن می‌کند.
/// Warning یعنی چیزی که باید دیده شود ولی مانع قطعی نیست (اغلب نیازمندِ داده‌ی عملیاتی).
/// NotApplicable یعنی این کنترل برای این شرکت/سال موضوعیت ندارد یا شواهدش از بیرون می‌آید.
/// Passed یعنی کنترل با شواهدِ دیتابیس سبز است.
/// </summary>
public enum ClosingCheckStatus
{
    Passed = 0,
    Warning = 1,
    NotApplicable = 2,
    Blocked = 3
}

/// <summary>
/// خروجی یک کنترلِ چک‌لیست. عمداً همان شکلِ یافتهٔ Readiness را دارد به‌علاوهٔ FiscalYearId و Link،
/// چون چک‌لیستِ بستنِ سال همیشه برای یک سالِ مشخص اجرا می‌شود. <see cref="SampleRecords"/> محدود
/// است — گزارش برای تصمیم است نه استخراج داده.
/// </summary>
public sealed record ClosingCheckResult(
    string Code,
    ClosingCheckStatus Status,
    string Title,
    string Description,
    int CompanyId,
    int FiscalYearId,
    int RecordCount,
    IReadOnlyList<string> SampleRecords,
    string RequiredAction,
    string? FeatureFlag,
    string? Link)
{
    public const int MaxSamples = 10;
}

/// <summary>
/// جمعِ سود و زیانِ سال — ورودیِ محاسبهٔ Final Close (مرحله ۱۴). فقط از سطرهای Posted تا
/// EndDate خوانده می‌شود. اینجا فقط گزارش می‌شود؛ هیچ سندی نوشته نمی‌شود.
/// </summary>
public sealed record ClosingRevenueExpenseSummary(
    decimal RevenueBalanceUsd,
    decimal ExpenseBalanceUsd,
    decimal NetProfitUsd);

/// <summary>
/// گزارشِ کاملِ چک‌لیستِ یک شرکت/سال. فقط‌خواندنی — اجرای آن هیچ Entity یا Journal را تغییر نمی‌دهد.
/// </summary>
public sealed record ClosingChecklistReport(
    DateTime GeneratedAtUtc,
    int CompanyId,
    string CompanyName,
    int FiscalYearId,
    string FiscalYearName,
    DateTime FiscalYearStartDate,
    DateTime FiscalYearEndDate,
    ClosingCheckStatus OverallStatus,
    ClosingRevenueExpenseSummary RevenueExpenseSummary,
    IReadOnlyList<ClosingCheckResult> Checks)
{
    public bool HasBlockers => Checks.Any(c => c.Status == ClosingCheckStatus.Blocked);

    public int BlockedCount => Checks.Count(c => c.Status == ClosingCheckStatus.Blocked);
    public int WarningCount => Checks.Count(c => c.Status == ClosingCheckStatus.Warning);
    public int PassedCount => Checks.Count(c => c.Status == ClosingCheckStatus.Passed);
}
