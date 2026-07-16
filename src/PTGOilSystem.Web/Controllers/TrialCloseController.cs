using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PTGOilSystem.Web.Data;
using PTGOilSystem.Web.Models.Accounting;
using PTGOilSystem.Web.Models.Entities;
using PTGOilSystem.Web.Security;
using PTGOilSystem.Web.Services;
using PTGOilSystem.Web.Services.Accounting;

namespace PTGOilSystem.Web.Controllers;

/// <summary>
/// مرحله ۱۳ — Trial Close و تسعیرِ پایان دوره.
///
/// خواندن (Preview) با GET؛ هر عملیاتِ نوشتنی فقط POST + antiforgery + AdminOnly. هیچ عملیاتِ
/// تغییردهنده‌ای با GET نیست. Trial Close سال را نمی‌بندد و دوره‌ای را HardLock نمی‌کند.
/// </summary>
[Authorize(Policy = AuthPolicies.AdminOnly)]
[Route("accounting/trial-close")]
public class TrialCloseController(
    ITrialCloseService trialClose,
    ApplicationDbContext db,
    ICurrentUserContext currentUser) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(int? companyId, int? fiscalYearId, CancellationToken cancellationToken)
    {
        var companies = await db.Companies.AsNoTracking()
            .Where(c => c.IsActive).OrderBy(c => c.Code)
            .Select(c => new { c.Id, c.Name }).ToListAsync(cancellationToken);

        var selectedCompany = companyId is int cid && companies.Any(c => c.Id == cid)
            ? cid : companies.Select(c => (int?)c.Id).FirstOrDefault();

        var years = selectedCompany is int scid
            ? await db.FiscalYears.AsNoTracking().Where(y => y.CompanyId == scid)
                .OrderByDescending(y => y.StartDate).ThenByDescending(y => y.Id)
                .Select(y => new { y.Id, y.Name }).ToListAsync(cancellationToken)
            : new();

        var selectedYear = fiscalYearId is int fyid && years.Any(y => y.Id == fyid)
            ? fyid : years.Select(y => (int?)y.Id).FirstOrDefault();

        var preview = selectedCompany is int c2 && selectedYear is int y2
            ? await trialClose.PreviewAsync(c2, y2, cancellationToken)
            : null;

        var runs = selectedYear is int y3
            ? await db.FiscalYearCloseRuns.AsNoTracking()
                .Where(r => r.FiscalYearId == y3)
                .OrderByDescending(r => r.StartedAt).ThenByDescending(r => r.Id)
                .Select(r => new TrialCloseRunRow(
                    r.Id, r.RunType.ToString(), r.Revision, r.Status.ToString(),
                    r.StartedAt, r.StartedByUser != null ? r.StartedByUser.Username : null,
                    r.JournalCount, r.DebitTotal, r.CreditTotal, r.SnapshotHash))
                .ToListAsync(cancellationToken)
            : new List<TrialCloseRunRow>();

        return View(new TrialClosePageViewModel(
            companies.Select(c => new ClosingChecklistCompanyOption(c.Id, c.Name, c.Id == selectedCompany)).ToList(),
            selectedCompany,
            years.Select(y => new ClosingChecklistYearOption(y.Id, y.Name, y.Id == selectedYear)).ToList(),
            selectedYear, preview, runs));
    }

    [HttpGet("preview")]
    public async Task<IActionResult> Preview(int companyId, int fiscalYearId, CancellationToken cancellationToken)
    {
        var preview = await trialClose.PreviewAsync(companyId, fiscalYearId, cancellationToken);
        return preview is null ? NotFound() : Json(preview);
    }

    [HttpPost("run")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RunTrialClose(
        int companyId, int fiscalYearId, bool acknowledgeWarnings, CancellationToken cancellationToken)
    {
        var result = await trialClose.RunTrialCloseAsync(
            companyId, fiscalYearId, currentUser.UserId, acknowledgeWarnings, cancellationToken);

        if (result.Status == TrialCloseResultStatus.Succeeded)
            TempData["ok"] = $"Trial Close ثبت شد (Run #{result.CloseRunId}).";
        else
            TempData["err"] = result.FailureCode ?? "Trial Close ممکن نیست.";

        return RedirectToAction(nameof(Index), new { companyId, fiscalYearId });
    }

    [HttpPost("apply-revaluation")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApplyRevaluation(int companyId, int fiscalYearId, CancellationToken cancellationToken)
    {
        var result = await trialClose.ApplyRevaluationAsync(companyId, fiscalYearId, currentUser.UserId, cancellationToken);

        if (result.Status == TrialCloseResultStatus.Succeeded)
            TempData["ok"] = $"تسعیر پایان دوره اعمال شد ({result.RevaluationJournalIds.Count} سند).";
        else
            TempData["err"] = result.FailureCode ?? "اعمال تسعیر ممکن نیست.";

        return RedirectToAction(nameof(Index), new { companyId, fiscalYearId });
    }
}
