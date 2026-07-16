using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PTGOilSystem.Web.Security;
using PTGOilSystem.Web.Services.Accounting;

namespace PTGOilSystem.Web.Controllers;

/// <summary>
/// مرحله ۱۰ — صفحه‌های سال مالی.
///
/// خواندن برای نقش‌های حسابداری/مدیریت باز است (<see cref="AuthPolicies.ManageData"/>) ولی هر
/// عملیاتِ تغییردهنده فقط POST + ضدجعل است و <see cref="AuthPolicies.AdminOnly"/> می‌خواهد:
/// ساختِ سال مالی ساختارِ دفتر را تعیین می‌کند و کارِ نقشِ عملیاتی نیست.
/// </summary>
[Authorize(Policy = AuthPolicies.ManageData)]
public class FiscalYearsController(
    IFiscalYearOverviewService overview,
    IFiscalYearProvisioningService provisioning,
    ICurrentUserContext currentUser) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(int? companyId, CancellationToken cancellationToken)
        => View(await overview.BuildIndexAsync(companyId, CanManage, cancellationToken));

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var model = await overview.BuildDetailsAsync(id, CanManage, cancellationToken);
        return model is null ? NotFound() : View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    public async Task<IActionResult> CreateNextYear(int companyId, CancellationToken cancellationToken)
    {
        var result = await provisioning.CreateNextYearAsync(
            companyId,
            currentUser.UserId,
            cancellationToken);

        if (result.Succeeded)
            TempData["ok"] = "سال مالی بعدی به‌صورت پیش‌نویس ساخته شد.";
        else
            TempData["err"] = result.ErrorCode ?? "ساخت سال مالی بعدی مجاز نیست.";

        return RedirectToAction(nameof(Index), new { companyId });
    }

    private bool CanManage => RoleAccessRules.CanManageUsers(User);
}
