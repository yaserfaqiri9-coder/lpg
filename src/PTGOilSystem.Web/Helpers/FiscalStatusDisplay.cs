using PTGOilSystem.Web.Models.Entities;

namespace PTGOilSystem.Web.Helpers;

/// <summary>
/// برچسب و کلاسِ نمایشیِ وضعیت‌های سال و دورهٔ مالی. فقط ترجمهٔ نمایشی است — هیچ تصمیم مالی‌ای
/// اینجا گرفته نمی‌شود؛ آن‌ها در <c>FiscalYearOverviewService</c> و <c>PeriodGuard</c> هستند.
/// کلاس‌ها همان کلاس‌های موجودِ <c>ak-status</c> هستند و CSS جدیدی ساخته نشده.
/// </summary>
public static class FiscalStatusDisplay
{
    public static string YearLabel(FiscalYearStatus status) => status switch
    {
        FiscalYearStatus.Draft => "پیش‌نویس",
        FiscalYearStatus.Open => "باز",
        FiscalYearStatus.Closing => "در حال بستن",
        FiscalYearStatus.Closed => "بسته",
        FiscalYearStatus.Reopened => "بازگشایی‌شده",
        _ => status.ToString()
    };

    public static string YearCss(FiscalYearStatus status) => status switch
    {
        FiscalYearStatus.Open or FiscalYearStatus.Reopened => "is-active",
        _ => "is-inactive"
    };

    public static string PeriodLabel(FiscalPeriodStatus status) => status switch
    {
        FiscalPeriodStatus.Open => "باز",
        FiscalPeriodStatus.Closed => "بسته",
        FiscalPeriodStatus.SoftLocked => "قفل نرم",
        FiscalPeriodStatus.HardLocked => "قفل سخت",
        _ => status.ToString()
    };

    public static string PeriodCss(FiscalPeriodStatus status) => status switch
    {
        FiscalPeriodStatus.Open => "is-active",
        _ => "is-inactive"
    };

    public static string CloseRunLabel(FiscalYearCloseRunStatus status) => status switch
    {
        FiscalYearCloseRunStatus.Pending => "در انتظار",
        FiscalYearCloseRunStatus.Running => "در حال اجرا",
        FiscalYearCloseRunStatus.Completed => "کامل",
        FiscalYearCloseRunStatus.Failed => "ناموفق",
        _ => status.ToString()
    };
}
