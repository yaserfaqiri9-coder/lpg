namespace PTGOilSystem.Web.Services.Accounting;

/// <summary>
/// یک ماندهٔ پولیِ باز و نتیجهٔ تسعیرش. گروه‌بندی بر اساس همهٔ ابعادِ خواسته‌شده در پرامپ:
/// Company/Account/Currency/PartyType/PartyId/ContractId/ShipmentId/CashAccountId.
/// </summary>
public sealed record RevaluationLine(
    int AccountId,
    string AccountCode,
    string Currency,
    Models.Entities.AccountingPartyType? PartyType,
    int? PartyId,
    int? ContractId,
    int? ShipmentId,
    int? CashAccountId,
    decimal OpenSourceAmount,
    decimal CarryingUsd,
    decimal ClosingRate,
    decimal ClosingUsd,
    decimal DifferenceUsd);

/// <summary>سندِ تسعیرِ یک ارز — همهٔ گروه‌های همان ارز، با یک SourceEventId مستقلِ per-currency.</summary>
public sealed record RevaluationCurrencyGroup(
    string Currency,
    decimal ClosingRate,
    DateTime ClosingRateDate,
    IReadOnlyList<RevaluationLine> Lines)
{
    public decimal NetDifferenceUsd => Lines.Sum(l => l.DifferenceUsd);
}

public sealed record MissingClosingRate(string Currency, DateTime RequiredDate);

/// <summary>
/// Previewِ فقط‌خواندنیِ تسعیرِ پایان دوره. هیچ سندی نمی‌نویسد.
/// </summary>
public sealed record TrialClosePreview(
    int CompanyId,
    int FiscalYearId,
    DateTime EndDate,
    bool ChecklistBlocked,
    IReadOnlyList<string> ChecklistBlockers,
    IReadOnlyList<string> ChecklistWarnings,
    IReadOnlyList<string> UnclassifiedMonetaryAccounts,
    IReadOnlyList<RevaluationCurrencyGroup> Revaluations,
    IReadOnlyList<MissingClosingRate> MissingRates,
    bool NextYearOpenPeriodExists,
    DateTime? NextYearReversalDate,
    string? BlockingReason)
{
    public bool CanApply => BlockingReason is null;
}

public enum TrialCloseResultStatus
{
    Succeeded = 0,
    Blocked = 1,
    WarningsNotAcknowledged = 2
}

/// <summary>نتیجهٔ اجرای Trial Close یا ApplyRevaluation.</summary>
public sealed record TrialCloseRunResult(
    TrialCloseResultStatus Status,
    int? CloseRunId,
    string? FailureCode,
    string? FailureMessage,
    IReadOnlyList<int> RevaluationJournalIds,
    IReadOnlyList<string> Warnings)
{
    public static TrialCloseRunResult Fail(string code, string message, IReadOnlyList<string>? warnings = null)
        => new(TrialCloseResultStatus.Blocked, null, code, message, Array.Empty<int>(), warnings ?? Array.Empty<string>());

    public static TrialCloseRunResult WarningsPending(IReadOnlyList<string> warnings)
        => new(TrialCloseResultStatus.WarningsNotAcknowledged, null, "WARNINGS_NOT_ACKNOWLEDGED",
            "چک‌لیست Warning دارد؛ ادامه نیاز به تأیید صریح دارد.", Array.Empty<int>(), warnings);
}
