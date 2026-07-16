using PTGOilSystem.Web.Services.Accounting;

namespace PTGOilSystem.Web.Models.Accounting;

public sealed record ClosingChecklistCompanyOption(int CompanyId, string Name, bool IsSelected);

public sealed record ClosingChecklistYearOption(
    int FiscalYearId, string Name, bool IsSelected);

/// <summary>
/// مرحله ۱۲ — مدلِ صفحهٔ چک‌لیستِ بستنِ سال. فقط برای نمایش؛ هیچ چیزی نمی‌نویسد.
/// </summary>
public sealed record ClosingChecklistPageViewModel(
    IReadOnlyList<ClosingChecklistCompanyOption> Companies,
    int? SelectedCompanyId,
    IReadOnlyList<ClosingChecklistYearOption> Years,
    int? SelectedFiscalYearId,
    ClosingChecklistReport? Report);
