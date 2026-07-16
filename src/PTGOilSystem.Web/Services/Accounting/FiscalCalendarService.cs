using Microsoft.EntityFrameworkCore;
using PTGOilSystem.Web.Data;
using PTGOilSystem.Web.Models.Entities;

namespace PTGOilSystem.Web.Services.Accounting;

public sealed record FiscalCalendarSelection(FiscalYear FiscalYear, FiscalPeriod FiscalPeriod);

/// <summary>
/// نتیجه‌ی دقیقِ جست‌وجوی تقویم برای یک AccountingDate. عمداً به‌جای «پیدا شد / پیدا نشد»، دلیلِ
/// دقیق برمی‌گردد تا <see cref="PeriodGuard"/> بتواند Reason Code درست بدهد؛ «تاریخ خارج از سال
/// مالی» و «دوره قفل سخت است» دو مشکل کاملاً متفاوت‌اند و کاربر باید بداند کدام است.
/// </summary>
public enum FiscalCalendarResolution
{
    Open = 0,
    FiscalYearNotFound = 1,
    FiscalYearClosed = 2,
    FiscalYearNotOpen = 3,
    PeriodNotFound = 4,
    CompanyPeriodMismatch = 5,
    PeriodSoftLocked = 6,
    PeriodHardLocked = 7
}

public sealed record FiscalCalendarLookup(
    FiscalCalendarResolution Resolution,
    FiscalYear? FiscalYear,
    FiscalPeriod? FiscalPeriod);

public interface IFiscalCalendarService
{
    /// <summary>دورهٔ باز برای این تاریخ، یا null. برای هر تصمیمی که به دلیل نیاز دارد، <see cref="ResolveAsync"/>.</summary>
    Task<FiscalCalendarSelection?> FindOpenPeriodAsync(
        int companyId,
        DateTime accountingDate,
        CancellationToken cancellationToken = default);

    Task<FiscalCalendarLookup> ResolveAsync(
        int companyId,
        DateTime accountingDate,
        CancellationToken cancellationToken = default);
}

public sealed class FiscalCalendarService(ApplicationDbContext db) : IFiscalCalendarService
{
    /// <summary>سالی که سند می‌تواند در آن بنشیند. سالِ بازگشایی‌شده هم باز است — معنیِ بازگشایی همین است.</summary>
    private static bool IsPostableYear(FiscalYearStatus status)
        => status is FiscalYearStatus.Open or FiscalYearStatus.Reopened;

    public async Task<FiscalCalendarSelection?> FindOpenPeriodAsync(
        int companyId,
        DateTime accountingDate,
        CancellationToken cancellationToken = default)
    {
        var lookup = await ResolveAsync(companyId, accountingDate, cancellationToken);
        return lookup.Resolution == FiscalCalendarResolution.Open
            ? new FiscalCalendarSelection(lookup.FiscalYear!, lookup.FiscalPeriod!)
            : null;
    }

    public async Task<FiscalCalendarLookup> ResolveAsync(
        int companyId,
        DateTime accountingDate,
        CancellationToken cancellationToken = default)
    {
        var date = accountingDate.Date;

        var years = await db.FiscalYears
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.StartDate <= date && x.EndDate >= date)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);

        if (years.Count == 0)
            return new FiscalCalendarLookup(FiscalCalendarResolution.FiscalYearNotFound, null, null);

        // سالِ قابلِ ثبت مقدم است. اگر هیچ‌کدام قابلِ ثبت نبودند، دلیل از خودِ سال خوانده می‌شود
        // تا «بسته» از «پیش‌نویس/در حال بستن» جدا بماند.
        var fiscalYear = years.FirstOrDefault(x => IsPostableYear(x.Status));
        if (fiscalYear is null)
        {
            var blocking = years[0];
            return new FiscalCalendarLookup(
                blocking.Status == FiscalYearStatus.Closed
                    ? FiscalCalendarResolution.FiscalYearClosed
                    : FiscalCalendarResolution.FiscalYearNotOpen,
                blocking,
                null);
        }

        // دوره عمداً **بدون فیلترِ شرکت** خوانده می‌شود: اگر دوره‌ای از شرکت دیگری داخل سال این
        // شرکت باشد، باید دیده و رد شود، نه اینکه با فیلتر ناپدید شود و «دوره پیدا نشد» بگیرد.
        var period = await db.FiscalPeriods
            .AsNoTracking()
            .Where(x => x.FiscalYearId == fiscalYear.Id && x.StartDate <= date && x.EndDate >= date)
            .OrderBy(x => x.PeriodNumber)
            .FirstOrDefaultAsync(cancellationToken);

        if (period is null)
            return new FiscalCalendarLookup(FiscalCalendarResolution.PeriodNotFound, fiscalYear, null);

        if (period.CompanyId != companyId)
            return new FiscalCalendarLookup(FiscalCalendarResolution.CompanyPeriodMismatch, fiscalYear, period);

        return new FiscalCalendarLookup(
            period.Status switch
            {
                FiscalPeriodStatus.Open => FiscalCalendarResolution.Open,
                FiscalPeriodStatus.SoftLocked => FiscalCalendarResolution.PeriodSoftLocked,
                // دورهٔ Closed از نظر ثبت دقیقاً همان قفل سخت است — سخت‌گیرانه‌ترین معنیِ موجود.
                _ => FiscalCalendarResolution.PeriodHardLocked
            },
            fiscalYear,
            period);
    }
}
