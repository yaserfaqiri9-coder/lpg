using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using PTGOilSystem.Web.Data;
using PTGOilSystem.Web.Diagnostics;
using PTGOilSystem.Web.Infrastructure.ModelBinding;
using PTGOilSystem.Web.Middleware;
using PTGOilSystem.Web.Security;
using PTGOilSystem.Web.Services;
using PTGOilSystem.Web.Services.Accounting;
using PTGOilSystem.Web.Services.AutoCode;
using PTGOilSystem.Web.Services.DeleteSafety;
using PTGOilSystem.Web.Services.Employees;
using System.IO.Compression;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// ---- Database ---------------------------------------------------------------
// Connection string is read from DATABASE_URL first, then DefaultConnection.
// DATABASE_URL is converted from the URL form
// (postgres://user:pass@host:port/db) to the Npgsql key-value form.
var rawConnectionString =
    Environment.GetEnvironmentVariable("DATABASE_URL")
    ?? builder.Configuration.GetConnectionString("DefaultConnection");
var hasDatabaseConnection = !string.IsNullOrWhiteSpace(rawConnectionString);

if (!hasDatabaseConnection)
{
    throw new InvalidOperationException(
        "PTG Oil System database connection is not configured. "
        + "Set DATABASE_URL or ConnectionStrings__DefaultConnection before starting the app. "
        + "The application no longer falls back to an InMemory database because that can make real PostgreSQL data look empty.");
}

builder.Services.AddScoped<MvcRequestTimingState>();
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddScoped<MvcQueryCountingInterceptor>();
    builder.Services.AddScoped<MvcRequestTimingFilter>();
}

builder.Services.AddDbContext<ApplicationDbContext>((serviceProvider, options) =>
{
    options.UseNpgsql(BuildPostgresConnectionString(rawConnectionString!));
    if (builder.Environment.IsDevelopment())
    {
        options.AddInterceptors(serviceProvider.GetRequiredService<MvcQueryCountingInterceptor>());
    }
});

// ---- Domain services (business rules, system rules #3-#9, #11, #13) --------
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IStockService, StockService>();
// ---- Independent accounting core (Stage 2; feature flag defaults to off). ----
builder.Services.Configure<PTGOilSystem.Web.Configuration.AccountingOptions>(
    builder.Configuration.GetSection(PTGOilSystem.Web.Configuration.AccountingOptions.SectionName));
builder.Services.AddScoped<IFiscalCalendarService, FiscalCalendarService>();
builder.Services.AddScoped<IPeriodGuard, PeriodGuard>();
builder.Services.AddScoped<IAccountingPostingService, AccountingPostingService>();
builder.Services.AddScoped<IAccountingChartSeeder, AccountingChartSeeder>();
builder.Services.AddScoped<IAccountingJournalNumberGenerator, AccountingJournalNumberGenerator>();
builder.Services.AddScoped<IContractBalanceTransferAccountingAdapter, ContractBalanceTransferAccountingAdapter>();
builder.Services.AddScoped<ISupplierPaymentAllocationAccountingAdapter, SupplierPaymentAllocationAccountingAdapter>();
builder.Services.AddScoped<ICompanyOwnershipReportService, CompanyOwnershipReportService>();
// مرحله ۹ — گزارش فقط‌خواندنیِ آمادگی Cutover. هیچ Flag را روشن و هیچ Migration را اجرا نمی‌کند.
builder.Services.AddScoped<IAccountingReadinessService, AccountingReadinessService>();
// مرحله ۱۰ — صفحه‌های سال مالی. Overview فقط می‌خواند؛ Provisioning تنها مسیر نوشتنِ آن است.
builder.Services.AddScoped<IFiscalYearOverviewService, FiscalYearOverviewService>();
builder.Services.AddScoped<IFiscalYearProvisioningService, FiscalYearProvisioningService>();
// مرحله ۱۲ — چک‌لیستِ بستنِ سال. کاملاً فقط‌خواندنی؛ هیچ Close/Lock/Migration/Posting ندارد.
builder.Services.AddScoped<IClosingChecklistService, ClosingChecklistService>();
// مرحله ۱۱ — تنها مسیر نوشتنِ وضعیت قفلِ دوره. قفل سخت از اینجا هم برگشت‌ناپذیر است.
builder.Services.AddScoped<IFiscalPeriodLockService, FiscalPeriodLockService>();
builder.Services.AddScoped<IPaymentCompanyResolver, PaymentCompanyResolver>();
builder.Services.AddScoped<IExpenseAccountingAdapter, ExpenseAccountingAdapter>();
builder.Services.AddScoped<IPaymentAccountingAdapter, PaymentAccountingAdapter>();
builder.Services.AddScoped<IViaSarrafAccountingAdapter, ViaSarrafAccountingAdapter>();
builder.Services.AddScoped<IInventoryValuationService, InventoryValuationService>();
builder.Services.AddScoped<IPurchaseAccountingAdapter, PurchaseAccountingAdapter>();
builder.Services.AddScoped<ISalesAccountingAdapter, SalesAccountingAdapter>();
builder.Services.AddScoped<IInventoryLossAccountingAdapter, InventoryLossAccountingAdapter>();
builder.Services.AddScoped<IShortageChargeAccountingAdapter, ShortageChargeAccountingAdapter>();
builder.Services.AddScoped<ISarrafSettlementAccountingAdapter, SarrafSettlementAccountingAdapter>();
builder.Services.AddScoped<IThreeWaySettlementAccountingAdapter, ThreeWaySettlementAccountingAdapter>();
builder.Services.AddScoped<IInventoryTransferAccountingAdapter, InventoryTransferAccountingAdapter>();
// ---- Inventory Lineage (Phase 2). Feature flags + parallel reference-layer services. ----
builder.Services.Configure<PTGOilSystem.Web.Configuration.LineageOptions>(
    builder.Configuration.GetSection(PTGOilSystem.Web.Configuration.LineageOptions.SectionName));
builder.Services.AddScoped<IInventoryLineageWriter, InventoryLineageWriter>();
builder.Services.AddScoped<InventoryLineageBackfillService>();
builder.Services.AddScoped<InventoryLineagePnlService>();
builder.Services.AddScoped<InventoryTransportLegLoadService>();
builder.Services.AddScoped<InventoryTransportBatchService>();
builder.Services.AddScoped<IPurchaseAggregationService, PurchaseAggregationService>();
builder.Services.AddScoped<ILossEventWorkflowService, LossEventWorkflowService>();
builder.Services.AddScoped<IPricingService, PricingService>();
builder.Services.AddScoped<ICurrencyConversionService, CurrencyConversionService>();
builder.Services.AddScoped<IUnitConversionService, UnitConversionService>();
builder.Services.AddScoped<ISarrafSettlementService, SarrafSettlementService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IExpenseRuleEngine, ExpenseRuleEngine>();
builder.Services.AddScoped<IContractAmendmentService, ContractAmendmentService>();
builder.Services.AddScoped<IContractBalanceTransferService, ContractBalanceTransferService>();
builder.Services.AddScoped<ISupplierPaymentAllocationService, SupplierPaymentAllocationService>();
builder.Services.AddScoped<IEmployeeSalaryService, EmployeeSalaryService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IAutoCodeService, AutoCodeService>();
builder.Services.AddScoped<AutoGeneratedCodeFilter>();
builder.Services.AddScoped<MasterDataDeleteSafetyService>();
builder.Services.AddScoped<ICurrentUserContext, CurrentUserContext>();
builder.Services.AddScoped<IFormTokenGuard, FormTokenGuard>();
builder.Services.AddScoped<RoleNavigationAuthorizationFilter>();
builder.Services.AddScoped<AuthBootstrapper>();
builder.Services.AddHttpContextAccessor();

var configuredDataProtectionKeysPath = builder.Configuration["PTG_DATA_PROTECTION_KEYS_PATH"];
var shouldUseLocalDataProtectionKeys =
    !string.IsNullOrWhiteSpace(configuredDataProtectionKeysPath)
    || builder.Environment.IsDevelopment()
    || OperatingSystem.IsWindows();

if (shouldUseLocalDataProtectionKeys)
{
    var dataProtectionKeysPath = string.IsNullOrWhiteSpace(configuredDataProtectionKeysPath)
        ? Path.Combine(builder.Environment.ContentRootPath, ".aspnet-data-protection-keys")
        : configuredDataProtectionKeysPath;

    Directory.CreateDirectory(dataProtectionKeysPath);

    builder.Services
        .AddDataProtection()
        .SetApplicationName("PTGOilSystem.Web")
        .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath));
}

builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[]
    {
        "text/css", "application/javascript", "text/javascript",
        "application/json", "text/html", "image/svg+xml",
        "font/woff2", "font/woff", "font/ttf", "font/otf",
        "application/font-woff2", "application/font-woff"
    });
});
builder.Services.Configure<BrotliCompressionProviderOptions>(o => o.Level = System.IO.Compression.CompressionLevel.Fastest);
builder.Services.Configure<GzipCompressionProviderOptions>(o => o.Level = System.IO.Compression.CompressionLevel.Fastest);
builder.Services.AddMemoryCache();

// ---- Authentication / Authorization -----------------------------------------
// Behind Replit's reverse proxy the app receives plain HTTP; trust the
// X-Forwarded-Proto header so the framework knows the original request was
// HTTPS. This makes the Secure cookie policy below behave correctly.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // Proxy IP is not fixed on Replit autoscale; clear the known-proxy allow-list.
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "PTGOilSystem.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.LoginPath = "/Auth/Login";
        options.AccessDeniedPath = "/Auth/AccessDenied";
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(12);
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AuthPolicies.ManageData,
        policy => policy.RequireAssertion(context => RoleAccessRules.CanManageData(context.User)));
    options.AddPolicy(AuthPolicies.AdminOnly,
        policy => policy.RequireAssertion(context => RoleAccessRules.CanManageUsers(context.User)));
});

// ---- MVC --------------------------------------------------------------------
builder.Services.AddControllersWithViews(options =>
{
    // فرم انتقال گروهی می‌تواند هزاران ردیف داشته باشد؛ محدودیت قدیمی ۱۰۰ ردیف حذف شده است.
    options.MaxModelBindingCollectionSize = 20_000;
    // تاریخ‌های query/form با Kind=Unspecified بایند می‌شوند و Npgsql آن‌ها را برای
    // ستون timestamptz رد می‌کند؛ این provider همه را به UTC نرمال می‌کند.
    options.ModelBinderProviders.Insert(0, new UtcDateTimeModelBinderProvider());
    options.Filters.AddService<AutoGeneratedCodeFilter>();
    options.Filters.AddService<RoleNavigationAuthorizationFilter>();
    if (builder.Environment.IsDevelopment())
    {
        options.Filters.AddService<MvcRequestTimingFilter>();
    }
});

// نکته: Razor Runtime Compilation عمداً فعال نشد. کامپایلر runtime نسخهٔ زبان C# 12 را
// رعایت نمی‌کند و collection expression مثل `[]` را رد می‌کند (Invalid expression term '[').
// برای دیدن زندهٔ تغییرات .cshtml بدون restart از dotnet watch استفاده کنید (run-dev.bat).

var app = builder.Build();

// ---- Auto-migrate database --------------------------------------------------
// Migrations run on startup by default to preserve existing server behaviour.
// Set PTG_AUTO_MIGRATE=false (or ConnectionStrings/Database:AutoMigrate=false)
// to disable — the desktop wrapper sets this so it never migrates implicitly.
var autoMigrate = builder.Configuration.GetValue("PTG_AUTO_MIGRATE", true)
    && builder.Configuration.GetValue("Database:AutoMigrate", true);
if (autoMigrate)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    DatabaseSafetyGuard.EnsureMigrationAllowed(db.Database.GetDbConnection().Database);
    db.Database.Migrate();
}

await SeedAuthenticationAsync(app);

// Must run first so downstream middleware sees the real scheme/host from the proxy.
app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}
// Note: HTTPS termination is handled by Replit's reverse proxy.
// UseHsts / UseHttpsRedirection are intentionally omitted to avoid
// redirect loops when the app receives plain HTTP from the proxy.
app.UseResponseCompression();

// Health-check probe — must respond 200 before auth middleware runs
// so that Replit's autoscale startup probe succeeds.
app.MapGet("/health", () => Results.Ok("healthy")).AllowAnonymous();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = context =>
    {
        var path = context.Context.Request.Path;

        if (path.StartsWithSegments("/css")
            || path.StartsWithSegments("/js")
            || path.StartsWithSegments("/vendor")
            || path.StartsWithSegments("/images")
            || path.StartsWithSegments("/img")
            || path.StartsWithSegments("/assets")
            || path.StartsWithSegments("/favicon.svg")
            || path.StartsWithSegments("/favicon.ico"))
        {
            context.Context.Response.Headers.CacheControl = "public,max-age=31536000,immutable";
        }
        else if (path.StartsWithSegments("/uploads"))
        {
            context.Context.Response.Headers.CacheControl = "public,max-age=86400";
        }
    }
});

app.UseRouting();
app.UseAuthentication();

if (app.Environment.IsDevelopment())
{
    app.UseMiddleware<DevAutoSignInMiddleware>();
}

app.UseAuthorization();
app.UseMiddleware<ActivityLogMiddleware>();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

// ---- Helpers ----------------------------------------------------------------
static async Task SeedAuthenticationAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var bootstrapper = scope.ServiceProvider.GetRequiredService<AuthBootstrapper>();
    var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

    await bootstrapper.EnsureSeedDataAsync(new BootstrapAdminOptions
    {
        Username = configuration["PTG_BOOTSTRAP_ADMIN_USERNAME"] ?? "admin",
        FullName = configuration["PTG_BOOTSTRAP_ADMIN_FULLNAME"] ?? "System Administrator",
        Password = configuration["PTG_BOOTSTRAP_ADMIN_PASSWORD"]
    });
}

static string BuildPostgresConnectionString(string raw)
{
    if (raw.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
        raw.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
    {
        var uri = new Uri(raw);
        var userInfo = uri.UserInfo.Split(':', 2);
        var username = Uri.UnescapeDataString(userInfo[0]);
        var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty;
        var database = uri.AbsolutePath.TrimStart('/');
        var port = uri.Port > 0 ? uri.Port : 5432;
        return $"Host={uri.Host};Port={port};Username={username};Password={password};Database={database};{DbTlsSettings()}";
    }
    return raw;
}

// TLS hardening is opt-in via PTG_DB_STRICT_TLS=true so the default Replit/Postgres
// connection (self-signed cert) keeps working unless strict TLS is explicitly enabled.
static string DbTlsSettings()
{
    var strict = string.Equals(
        Environment.GetEnvironmentVariable("PTG_DB_STRICT_TLS"),
        "true",
        StringComparison.OrdinalIgnoreCase);
    return strict
        ? "SSL Mode=Require;Trust Server Certificate=false"
        : "SSL Mode=Prefer;Trust Server Certificate=true";
}
