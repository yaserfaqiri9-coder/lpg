using PTGOilSystem.Web.Services.Accounting;

namespace PTGOilSystem.Web.Models.Accounting;

/// <summary>
/// مرحله ۱۳ — مدلِ صفحهٔ Trial Close. فقط برای نمایشِ Preview و دکمه‌های POST.
/// </summary>
public sealed record TrialClosePageViewModel(
    IReadOnlyList<ClosingChecklistCompanyOption> Companies,
    int? SelectedCompanyId,
    IReadOnlyList<ClosingChecklistYearOption> Years,
    int? SelectedFiscalYearId,
    TrialClosePreview? Preview,
    IReadOnlyList<TrialCloseRunRow> Runs);

public sealed record TrialCloseRunRow(
    int Id,
    string RunType,
    int Revision,
    string Status,
    DateTime StartedAt,
    string? StartedBy,
    int JournalCount,
    decimal DebitTotal,
    decimal CreditTotal,
    string? SnapshotHash);
