using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PTGOilSystem.Web.Data;
using PTGOilSystem.Web.Models.Entities;

namespace PTGOilSystem.Web.Services.Accounting;

public sealed record FiscalPeriodLockResult(bool Succeeded, string? ErrorCode);

public interface IFiscalPeriodLockService
{
    Task<FiscalPeriodLockResult> ChangeStatusAsync(
        int fiscalPeriodId,
        FiscalPeriodStatus newStatus,
        int? actorUserId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// مرحله ۱۱ — تغییرِ وضعیتِ قفلِ دوره. تنها مسیرِ نوشتنِ <see cref="FiscalPeriod.Status"/>.
///
/// دو قاعده‌ی این سرویس عمدی‌اند:
/// 1. **قفلِ سخت برگشت‌ناپذیر است.** اگر بشود بازش کرد، قفل سخت نیست و همهٔ تضمین‌های مرحله ۱۱
///    به یک کلیک تبدیل می‌شوند. بازگشایی کارِ مرحله ۱۵ است، با مسیر و تأییدِ خودش.
/// 2. **دورهٔ سالِ بسته اصلاً تغییر نمی‌کند** — نه باز می‌شود، نه دوباره قفل.
///
/// هر تغییر وضعیت Audit می‌شود، داخل همان تراکنش.
/// </summary>
public sealed class FiscalPeriodLockService(
    ApplicationDbContext db,
    IAuditService audit) : IFiscalPeriodLockService
{
    public async Task<FiscalPeriodLockResult> ChangeStatusAsync(
        int fiscalPeriodId,
        FiscalPeriodStatus newStatus,
        int? actorUserId,
        CancellationToken cancellationToken = default)
    {
        var period = await db.FiscalPeriods
            .Include(p => p.FiscalYear)
            .SingleOrDefaultAsync(p => p.Id == fiscalPeriodId, cancellationToken);

        if (period is null)
            return new FiscalPeriodLockResult(false, "PERIOD_NOT_FOUND");

        if (period.FiscalYear is { Status: FiscalYearStatus.Closed })
            return new FiscalPeriodLockResult(false, "FISCAL_YEAR_CLOSED");

        if (period.Status == FiscalPeriodStatus.HardLocked)
            return new FiscalPeriodLockResult(false, "PERIOD_HARD_LOCKED");

        if (period.Status == newStatus)
            return new FiscalPeriodLockResult(true, null);

        var oldStatus = period.Status;
        period.Status = newStatus;
        period.UpdatedByUserId = actorUserId;
        period.UpdatedAtUtc = DateTime.UtcNow;

        if (newStatus == FiscalPeriodStatus.Open)
        {
            period.LockedAt = null;
            period.LockedByUserId = null;
        }
        else
        {
            period.LockedAt = DateTime.UtcNow;
            period.LockedByUserId = actorUserId;
        }

        await audit.LogAsync(
            nameof(FiscalPeriod),
            period.Id,
            AuditAction.Update,
            actorUserId,
            JsonSerializer.Serialize(new
            {
                Action = "ChangeFiscalPeriodLock",
                period.CompanyId,
                period.FiscalYearId,
                FiscalPeriodId = period.Id,
                OldStatus = oldStatus.ToString(),
                NewStatus = newStatus.ToString()
            }),
            cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        return new FiscalPeriodLockResult(true, null);
    }
}
