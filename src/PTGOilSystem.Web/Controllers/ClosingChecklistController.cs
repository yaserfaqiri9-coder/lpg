using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PTGOilSystem.Web.Data;
using PTGOilSystem.Web.Models.Accounting;
using PTGOilSystem.Web.Security;
using PTGOilSystem.Web.Services.Accounting;

namespace PTGOilSystem.Web.Controllers;

/// <summary>
/// مرحله ۱۲ — چک‌لیستِ بستنِ سال.
///
/// این کنترلر **فقط GET** دارد و هیچ مسیر نوشتنی ندارد: نه Close، نه Lock، نه Migration، نه
/// Posting. اجرای چک‌لیست هیچ Entity یا Journal را تغییر نمی‌دهد. GET برای مشاهده و Export مجاز
/// است. دسترسی برای نقش‌های حسابداری/مدیریت باز است.
/// </summary>
[Authorize(Policy = AuthPolicies.ManageData)]
[Route("accounting/closing-checklist")]
public class ClosingChecklistController(
    IClosingChecklistService checklist,
    ApplicationDbContext db) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(int? companyId, int? fiscalYearId, CancellationToken cancellationToken)
    {
        var companies = await db.Companies.AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.Code)
            .Select(c => new { c.Id, c.Name })
            .ToListAsync(cancellationToken);

        var selectedCompany = companyId is int cid && companies.Any(c => c.Id == cid)
            ? cid
            : companies.Select(c => (int?)c.Id).FirstOrDefault();

        var years = selectedCompany is int scid
            ? await db.FiscalYears.AsNoTracking()
                .Where(y => y.CompanyId == scid)
                .OrderByDescending(y => y.StartDate).ThenByDescending(y => y.Id)
                .Select(y => new { y.Id, y.Name })
                .ToListAsync(cancellationToken)
            : new();

        var selectedYear = fiscalYearId is int fyid && years.Any(y => y.Id == fyid)
            ? fyid
            : years.Select(y => (int?)y.Id).FirstOrDefault();

        var report = selectedCompany is int c2 && selectedYear is int y2
            ? await checklist.BuildAsync(c2, y2, cancellationToken)
            : null;

        var model = new ClosingChecklistPageViewModel(
            companies.Select(c => new ClosingChecklistCompanyOption(c.Id, c.Name, c.Id == selectedCompany)).ToList(),
            selectedCompany,
            years.Select(y => new ClosingChecklistYearOption(y.Id, y.Name, y.Id == selectedYear)).ToList(),
            selectedYear,
            report);

        return View(model);
    }

    [HttpGet("json")]
    public async Task<IActionResult> Json(int companyId, int fiscalYearId, CancellationToken cancellationToken)
    {
        var report = await checklist.BuildAsync(companyId, fiscalYearId, cancellationToken);
        return report is null ? NotFound() : Json(report);
    }

    [HttpGet("csv")]
    public async Task<IActionResult> Csv(int companyId, int fiscalYearId, CancellationToken cancellationToken)
    {
        var report = await checklist.BuildAsync(companyId, fiscalYearId, cancellationToken);
        if (report is null)
            return NotFound();

        var sb = new StringBuilder();
        sb.AppendLine("Code,Status,Title,CompanyId,FiscalYearId,RecordCount,FeatureFlag,Link,RequiredAction,SampleRecords");
        foreach (var check in report.Checks)
        {
            sb.Append(Csv(check.Code)).Append(',')
              .Append(Csv(check.Status.ToString())).Append(',')
              .Append(Csv(check.Title)).Append(',')
              .Append(check.CompanyId.ToString(CultureInfo.InvariantCulture)).Append(',')
              .Append(check.FiscalYearId.ToString(CultureInfo.InvariantCulture)).Append(',')
              .Append(check.RecordCount.ToString(CultureInfo.InvariantCulture)).Append(',')
              .Append(Csv(check.FeatureFlag)).Append(',')
              .Append(Csv(check.Link)).Append(',')
              .Append(Csv(check.RequiredAction)).Append(',')
              .Append(Csv(string.Join(" ; ", check.SampleRecords)))
              .Append('\n');
        }

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return File(bytes, "text/csv", $"closing-checklist-{companyId}-{fiscalYearId}.csv");
    }

    // نقل‌قول‌گذاری استاندارد CSV: هر فیلد در گیومه، و گیومهٔ داخلی دوبار می‌شود.
    private static string Csv(string? value)
        => "\"" + (value ?? "").Replace("\"", "\"\"") + "\"";
}
