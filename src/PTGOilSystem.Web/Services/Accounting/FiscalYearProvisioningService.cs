using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PTGOilSystem.Web.Data;
using PTGOilSystem.Web.Models.Entities;

namespace PTGOilSystem.Web.Services.Accounting;

public sealed record CreateNextFiscalYearResult(bool Succeeded, string? ErrorCode, int? FiscalYearId);

public interface IFiscalYearProvisioningService
{
    Task<CreateNextFiscalYearResult> CreateNextYearAsync(
        int companyId,
        int? actorUserId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// مرحله ۱۰ — تنها مسیرِ نوشتنِ صفحهٔ سال مالی: ساختِ سال بعد.
///
/// سالِ جدید **آینهٔ دقیقِ آخرین سال** است (همان تعداد دوره، هر تاریخ یک سال جلوتر) و با وضعیت
/// <see cref="FiscalYearStatus.Draft"/> ساخته می‌شود؛ بازکردنش تصمیم جداگانه‌ای است و اینجا
/// خودکار انجام نمی‌شود. <see cref="FiscalYear.IsCurrent"/> هم دست‌نخورده می‌ماند — جابه‌جایی
/// سالِ جاری کارِ همین دکمه نیست.
/// </summary>
public sealed class FiscalYearProvisioningService(
    ApplicationDbContext db,
    IFiscalYearOverviewService overview,
    IAuditService audit) : IFiscalYearProvisioningService
{
    public async Task<CreateNextFiscalYearResult> CreateNextYearAsync(
        int companyId,
        int? actorUserId,
        CancellationToken cancellationToken = default)
    {
        var proposal = await overview.BuildNextYearProposalAsync(companyId, cancellationToken);
        if (!proposal.IsAllowed
            || proposal.ProposedStartDate is not DateTime start
            || proposal.ProposedEndDate is not DateTime end)
        {
            return new CreateNextFiscalYearResult(false, proposal.BlockedReason ?? "NEXT_YEAR_NOT_ALLOWED", null);
        }

        var source = await overview.FindSourceYearAsync(companyId, cancellationToken);
        if (source is null)
            return new CreateNextFiscalYearResult(false, "SOURCE_FISCAL_YEAR_NOT_FOUND", null);

        var sourcePeriods = await db.FiscalPeriods.AsNoTracking()
            .Where(p => p.FiscalYearId == source.Id)
            .OrderBy(p => p.PeriodNumber)
            .ToListAsync(cancellationToken);

        if (sourcePeriods.Count == 0)
            return new CreateNextFiscalYearResult(false, "SOURCE_FISCAL_YEAR_HAS_NO_PERIODS", null);

        var owned = db.Database.IsRelational() && db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(cancellationToken)
            : null;

        try
        {
            var year = new FiscalYear
            {
                CompanyId = companyId,
                Name = proposal.ProposedName ?? $"FY-{start.Year}",
                StartDate = start,
                EndDate = end,
                Status = FiscalYearStatus.Draft,
                PreviousFiscalYearId = source.Id,
                IsCurrent = false,
                CreatedByUserId = actorUserId
            };
            db.FiscalYears.Add(year);
            await db.SaveChangesAsync(cancellationToken);

            foreach (var period in sourcePeriods)
            {
                db.FiscalPeriods.Add(new FiscalPeriod
                {
                    CompanyId = companyId,
                    FiscalYearId = year.Id,
                    PeriodNumber = period.PeriodNumber,
                    Name = period.Name,
                    StartDate = period.StartDate.AddYears(1).Date,
                    EndDate = period.EndDate.AddYears(1).Date,
                    Status = FiscalPeriodStatus.Open,
                    CreatedByUserId = actorUserId
                });
            }

            await audit.LogAsync(
                nameof(FiscalYear),
                year.Id,
                AuditAction.Insert,
                actorUserId,
                JsonSerializer.Serialize(new
                {
                    Action = "CreateNextFiscalYear",
                    CompanyId = companyId,
                    SourceFiscalYearId = source.Id,
                    year.Name,
                    StartDate = start,
                    EndDate = end,
                    PeriodCount = sourcePeriods.Count,
                    Status = nameof(FiscalYearStatus.Draft)
                }),
                cancellationToken);

            await db.SaveChangesAsync(cancellationToken);

            if (owned is not null)
                await owned.CommitAsync(cancellationToken);

            return new CreateNextFiscalYearResult(true, null, year.Id);
        }
        catch
        {
            if (owned is not null)
                await owned.RollbackAsync(cancellationToken);
            throw;
        }
        finally
        {
            if (owned is not null)
                await owned.DisposeAsync();
        }
    }
}
