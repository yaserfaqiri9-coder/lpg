using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PTGOilSystem.Web.Data;
using PTGOilSystem.Web.Models.Entities;

namespace PTGOilSystem.Web.Services.Accounting;

public interface ITrialCloseService
{
    Task<TrialClosePreview?> PreviewAsync(int companyId, int fiscalYearId, CancellationToken cancellationToken = default);

    Task<TrialCloseRunResult> RunTrialCloseAsync(
        int companyId, int fiscalYearId, int? userId, bool acknowledgeWarnings,
        CancellationToken cancellationToken = default);

    Task<TrialCloseRunResult> ApplyRevaluationAsync(
        int companyId, int fiscalYearId, int? userId, CancellationToken cancellationToken = default);
}

/// <summary>
/// مرحله ۱۳ — Trial Close و تسعیرِ پایان دوره.
///
/// تصمیم‌های قطعیِ ثبت‌شده: Trial Close سال را نمی‌بندد، دوره را HardLock نمی‌کند و ClosedAt را
/// تنظیم نمی‌کند؛ فقط یک Snapshotِ قابل‌حسابرسی می‌سازد. تسعیر فقط روی ماندهٔ پولیِ باز و فقط با
/// نرخِ دقیقِ FiscalYear.EndDate از DailyFxRate (usd = source / Rate) انجام می‌شود؛ نبودِ نرخ
/// Blocker است و هیچ fallback نرخ استفاده نمی‌شود. حساب‌های غیرپولی، درآمد، هزینه، موجودی و
/// کالای در راه تسعیر نمی‌شوند. برگشتِ خودکارِ تسعیر در اولین دورهٔ بازِ سالِ بعد ثبت می‌شود.
/// </summary>
public sealed class TrialCloseService(
    ApplicationDbContext db,
    IClosingChecklistService checklist,
    IAccountingPostingService posting) : ITrialCloseService
{
    public const string SourceModule = "FiscalYearClose";
    private const decimal Epsilon = 0.00005m;

    public async Task<TrialClosePreview?> PreviewAsync(
        int companyId, int fiscalYearId, CancellationToken cancellationToken = default)
    {
        var year = await db.FiscalYears.AsNoTracking()
            .SingleOrDefaultAsync(y => y.Id == fiscalYearId && y.CompanyId == companyId, cancellationToken);
        if (year is null)
            return null;

        return await ComputePreviewAsync(companyId, year, cancellationToken);
    }

    private async Task<TrialClosePreview> ComputePreviewAsync(
        int companyId, FiscalYear year, CancellationToken cancellationToken)
    {
        var endDate = year.EndDate.Date;

        var report = await checklist.BuildAsync(companyId, year.Id, cancellationToken);
        var blockers = report?.Checks
            .Where(c => c.Status == ClosingCheckStatus.Blocked).Select(c => c.Code).ToList() ?? new();
        var warnings = report?.Checks
            .Where(c => c.Status == ClosingCheckStatus.Warning).Select(c => c.Code).ToList() ?? new();

        var unclassified = await db.Accounts.AsNoTracking()
            .Where(a => a.CompanyId == companyId && a.IsActive
                && a.MonetaryTreatment == MonetaryTreatment.Unspecified
                && db.JournalEntryLines.Any(l => l.AccountId == a.Id
                    && l.JournalEntry!.FiscalYearId == year.Id
                    && l.JournalEntry.Status == JournalEntryStatus.Posted))
            .Select(a => $"AccountId={a.Id}, Code={a.Code}")
            .ToListAsync(cancellationToken);

        var (groups, missingRates) = await ComputeRevaluationAsync(companyId, year, cancellationToken);

        var (nextExists, reversalDate) = await FindNextYearReversalDateAsync(companyId, year, cancellationToken);

        string? blocking =
            blockers.Count > 0 ? "CHECKLIST_BLOCKED"
            : unclassified.Count > 0 ? "MONETARY_TREATMENT_UNSPECIFIED"
            : missingRates.Count > 0 ? "CLOSING_RATE_MISSING"
            : groups.Count > 0 && !nextExists ? "NEXT_YEAR_OPEN_PERIOD_MISSING"
            : null;

        return new TrialClosePreview(
            companyId, year.Id, endDate,
            blockers.Count > 0, blockers, warnings, unclassified,
            groups, missingRates, nextExists, reversalDate, blocking);
    }

    /// <summary>
    /// ماندهٔ پولیِ بازِ هر گروه را تا EndDate از سطرهای Posted می‌سازد و تسعیرش می‌کند.
    /// فقط حساب‌های Monetary و فقط ارزهای غیر-USD. Reversalها چون سطرِ برعکس دارند خودبه‌خود در
    /// ماندهٔ خالص لحاظ می‌شوند.
    /// </summary>
    private async Task<(IReadOnlyList<RevaluationCurrencyGroup> Groups, IReadOnlyList<MissingClosingRate> Missing)>
        ComputeRevaluationAsync(int companyId, FiscalYear year, CancellationToken cancellationToken)
    {
        var endDate = year.EndDate.Date;

        var rows = await db.JournalEntryLines.AsNoTracking()
            .Where(l => l.JournalEntry!.CompanyId == companyId
                && l.JournalEntry.FiscalYearId == year.Id
                && l.JournalEntry.Status == JournalEntryStatus.Posted
                && l.JournalEntry.AccountingDate <= endDate
                && l.Account!.MonetaryTreatment == MonetaryTreatment.Monetary
                && l.TransactionCurrencyCode != "USD")
            .Select(l => new
            {
                l.AccountId,
                AccountCode = l.Account!.Code,
                Currency = l.TransactionCurrencyCode,
                l.PartyType, l.PartyId, l.ContractId, l.ShipmentId, l.CashAccountId,
                l.Debit, l.Credit, l.TransactionAmount
            })
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
            return (Array.Empty<RevaluationCurrencyGroup>(), Array.Empty<MissingClosingRate>());

        var currencies = rows.Select(r => r.Currency).Distinct().ToList();
        var rates = await db.DailyFxRates.AsNoTracking()
            .Where(r => r.BaseCurrency == "USD" && r.RateDate == endDate && currencies.Contains(r.QuoteCurrency))
            .Select(r => new { r.QuoteCurrency, r.Rate })
            .ToListAsync(cancellationToken);
        var rateByCurrency = rates
            .GroupBy(r => r.QuoteCurrency)
            .ToDictionary(g => g.Key, g => g.First().Rate, StringComparer.OrdinalIgnoreCase);

        var grouped = rows
            .GroupBy(r => new { r.AccountId, r.AccountCode, r.Currency,
                r.PartyType, r.PartyId, r.ContractId, r.ShipmentId, r.CashAccountId });

        var groups = new List<RevaluationCurrencyGroup>();
        var missing = new List<MissingClosingRate>();
        var perCurrencyLines = new Dictionary<string, List<RevaluationLine>>(StringComparer.OrdinalIgnoreCase);
        var missingCurrencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var g in grouped)
        {
            var carryingUsd = decimal.Round(g.Sum(x => x.Debit - x.Credit), 4, MidpointRounding.AwayFromZero);
            var openSource = g.Sum(x => x.Debit > 0m ? x.TransactionAmount : -x.TransactionAmount);
            if (openSource == 0m)
                continue;

            if (!rateByCurrency.TryGetValue(g.Key.Currency, out var rate) || rate <= 0m)
            {
                missingCurrencies.Add(g.Key.Currency);
                continue;
            }

            var closingUsd = decimal.Round(openSource / rate, 4, MidpointRounding.AwayFromZero);
            var difference = decimal.Round(closingUsd - carryingUsd, 4, MidpointRounding.AwayFromZero);
            if (Math.Abs(difference) < Epsilon)
                continue;

            if (!perCurrencyLines.TryGetValue(g.Key.Currency, out var list))
                perCurrencyLines[g.Key.Currency] = list = new List<RevaluationLine>();

            list.Add(new RevaluationLine(
                g.Key.AccountId, g.Key.AccountCode, g.Key.Currency,
                g.Key.PartyType, g.Key.PartyId, g.Key.ContractId, g.Key.ShipmentId, g.Key.CashAccountId,
                openSource, carryingUsd, rate, closingUsd, difference));
        }

        foreach (var currency in missingCurrencies)
            missing.Add(new MissingClosingRate(currency, endDate));

        foreach (var (currency, list) in perCurrencyLines.OrderBy(kv => kv.Key))
        {
            if (list.Count == 0)
                continue;
            groups.Add(new RevaluationCurrencyGroup(currency, list[0].ClosingRate, endDate,
                list.OrderBy(l => l.AccountId).ThenBy(l => l.PartyId).ToList()));
        }

        return (groups, missing);
    }

    /// <summary>
    /// اولین دورهٔ بازِ سالِ بعد (سالی که دقیقاً پس از EndDate شروع می‌شود). تاریخِ برگشتِ خودکار
    /// = شروعِ آن دوره. اگر سال بعد یا دورهٔ بازِ آن نباشد، برگشت ممکن نیست و Apply رد می‌شود.
    /// </summary>
    private async Task<(bool Exists, DateTime? ReversalDate)> FindNextYearReversalDateAsync(
        int companyId, FiscalYear year, CancellationToken cancellationToken)
    {
        var nextStart = year.EndDate.Date.AddDays(1);
        var nextYear = await db.FiscalYears.AsNoTracking()
            .Where(y => y.CompanyId == companyId && y.StartDate == nextStart)
            .OrderBy(y => y.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (nextYear is null)
            return (false, null);

        var firstOpen = await db.FiscalPeriods.AsNoTracking()
            .Where(p => p.FiscalYearId == nextYear.Id && p.Status == FiscalPeriodStatus.Open)
            .OrderBy(p => p.StartDate).ThenBy(p => p.PeriodNumber)
            .Select(p => (DateTime?)p.StartDate)
            .FirstOrDefaultAsync(cancellationToken);

        return firstOpen is null ? (false, null) : (true, firstOpen.Value.Date);
    }

    public async Task<TrialCloseRunResult> RunTrialCloseAsync(
        int companyId, int fiscalYearId, int? userId, bool acknowledgeWarnings,
        CancellationToken cancellationToken = default)
    {
        var year = await db.FiscalYears.AsNoTracking()
            .SingleOrDefaultAsync(y => y.Id == fiscalYearId && y.CompanyId == companyId, cancellationToken);
        if (year is null)
            return TrialCloseRunResult.Fail("FISCAL_YEAR_NOT_FOUND", "سال مالی یافت نشد.");

        var preview = await ComputePreviewAsync(companyId, year, cancellationToken);

        if (preview.ChecklistBlocked)
            return TrialCloseRunResult.Fail("CHECKLIST_BLOCKED",
                "چک‌لیست Blocked دارد؛ Trial Close ممکن نیست.", preview.ChecklistBlockers);

        if (preview.ChecklistWarnings.Count > 0 && !acknowledgeWarnings)
            return TrialCloseRunResult.WarningsPending(preview.ChecklistWarnings);

        var report = await checklist.BuildAsync(companyId, fiscalYearId, cancellationToken);
        var yearStats = await LoadYearStatsAsync(companyId, fiscalYearId, year.EndDate.Date, cancellationToken);

        var rateSnapshot = preview.Revaluations
            .Select(g => new { g.Currency, g.ClosingRate, Date = g.ClosingRateDate })
            .ToList();

        var run = new FiscalYearCloseRun
        {
            CompanyId = companyId,
            FiscalYearId = fiscalYearId,
            RunType = FiscalYearCloseRunType.Trial,
            Revision = 0,
            Status = FiscalYearCloseRunStatus.Completed,
            StartedAt = DateTime.UtcNow,
            StartedByUserId = userId,
            CompletedAt = DateTime.UtcNow,
            CompletedByUserId = userId,
            ChecklistSnapshotJson = JsonSerializer.Serialize(report),
            WarningAcknowledgementsJson = acknowledgeWarnings
                ? JsonSerializer.Serialize(preview.ChecklistWarnings) : null,
            ClosingRateSnapshotJson = JsonSerializer.Serialize(rateSnapshot),
            JournalCount = yearStats.JournalCount,
            DebitTotal = yearStats.Debit,
            CreditTotal = yearStats.Credit,
            SourceDataCutoff = year.EndDate.Date,
            LastJournalEntryId = yearStats.LastJournalId,
            LastJournalPostedAt = yearStats.LastPostedAt
        };
        run.SnapshotHash = ComputeHash(run);

        db.FiscalYearCloseRuns.Add(run);
        await db.SaveChangesAsync(cancellationToken);

        return new TrialCloseRunResult(
            TrialCloseResultStatus.Succeeded, run.Id, null, null,
            Array.Empty<int>(), preview.ChecklistWarnings);
    }

    public async Task<TrialCloseRunResult> ApplyRevaluationAsync(
        int companyId, int fiscalYearId, int? userId, CancellationToken cancellationToken = default)
    {
        var year = await db.FiscalYears.AsNoTracking()
            .SingleOrDefaultAsync(y => y.Id == fiscalYearId && y.CompanyId == companyId, cancellationToken);
        if (year is null)
            return TrialCloseRunResult.Fail("FISCAL_YEAR_NOT_FOUND", "سال مالی یافت نشد.");

        var preview = await ComputePreviewAsync(companyId, year, cancellationToken);
        if (!preview.CanApply)
            return TrialCloseRunResult.Fail(preview.BlockingReason ?? "BLOCKED",
                $"تسعیر قابل اعمال نیست: {preview.BlockingReason}.", preview.ChecklistBlockers);

        var settings = await db.AccountingSettings.AsNoTracking()
            .SingleAsync(s => s.CompanyId == companyId, cancellationToken);
        var reversalDate = preview.NextYearReversalDate!.Value;

        var postedIds = new List<int>();
        foreach (var group in preview.Revaluations)
        {
            var existing = await LoadActiveRevaluationAsync(companyId, fiscalYearId, group.Currency, cancellationToken);
            if (existing is not null)
            {
                if (EffectMatches(existing, group, settings))
                    continue; // نرخ و مانده تغییر نکرده — Duplicate و بی‌اثر.

                // نرخ/مانده تغییر کرده: نسخهٔ قبلی و برگشت خودکارش Supersede می‌شوند، سپس Revision جدید.
                await SupersedeAsync(existing, reversalDate, userId, cancellationToken);
            }

            var revision = (existing is null ? -1 : RevisionOf(existing.SourceEventId)) + 1;
            var journal = await PostRevaluationAsync(companyId, fiscalYearId, group, year.EndDate.Date, revision, settings, userId, cancellationToken);
            postedIds.Add(journal.Id);

            // برگشتِ خودکار در اولین دورهٔ بازِ سالِ بعد — Idempotent از طریق SourceEventId.
            await PostAutoReversalAsync(companyId, fiscalYearId, group, reversalDate, revision, settings, journal, userId, cancellationToken);
        }

        // آخرین Trial Run را با شناسه‌های تسعیر به‌روزرسانی می‌کنیم (Snapshot تغییرناپذیر می‌ماند).
        var run = await db.FiscalYearCloseRuns
            .Where(r => r.CompanyId == companyId && r.FiscalYearId == fiscalYearId
                && r.RunType == FiscalYearCloseRunType.Trial)
            .OrderByDescending(r => r.StartedAt).ThenByDescending(r => r.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (run is not null)
        {
            var prior = string.IsNullOrEmpty(run.RevaluationJournalIdsJson)
                ? new List<int>()
                : JsonSerializer.Deserialize<List<int>>(run.RevaluationJournalIdsJson) ?? new();
            prior.AddRange(postedIds);
            run.RevaluationJournalIdsJson = JsonSerializer.Serialize(prior.Distinct().ToList());
            await db.SaveChangesAsync(cancellationToken);
        }

        return new TrialCloseRunResult(
            TrialCloseResultStatus.Succeeded, run?.Id, null, null, postedIds, preview.ChecklistWarnings);
    }

    private async Task<JournalEntry?> LoadActiveRevaluationAsync(
        int companyId, int fiscalYearId, string currency, CancellationToken cancellationToken)
    {
        var prefix = $"FiscalYearRevaluation:{fiscalYearId}:{currency}:";
        var loaded = await db.JournalEntries.AsNoTracking()
            .Include(j => j.Lines)
            .Where(j => j.CompanyId == companyId && j.SourceModule == SourceModule
                && j.SourceEventId != null && j.SourceEventId.StartsWith(prefix)
                && j.Status == JournalEntryStatus.Posted && !j.IsReversal)
            .ToListAsync(cancellationToken);

        // فقط سندِ خودِ تسعیر: SourceEventId دقیقاً چهار بخشِ colon دارد
        // (FiscalYearRevaluation:{fy}:{ccy}:{rev}). برگشتِ خودکار (…:Reversal) و Supersede
        // (…:Superseded) بخش‌های بیشتری دارند و نباید به‌عنوان «نسخهٔ فعال» خوانده شوند.
        var candidates = loaded
            .Where(j => j.SourceEventId!.Split(':').Length == 4)
            .ToList();

        // فعال = بالاترین Revisionی که Supersede نشده (سندِ ...:Superseded برایش وجود ندارد).
        var supersededIds = await db.JournalEntries.AsNoTracking()
            .Where(j => j.CompanyId == companyId && j.SourceModule == SourceModule
                && j.SourceEventId != null && j.SourceEventId.StartsWith(prefix)
                && j.SourceEventId.EndsWith(":Superseded"))
            .Select(j => j.ReversalOfJournalEntryId)
            .ToListAsync(cancellationToken);

        return candidates
            .Where(j => !supersededIds.Contains(j.Id))
            .OrderByDescending(j => j.Id)
            .FirstOrDefault();
    }

    private static int RevisionOf(string? sourceEventId)
    {
        // FiscalYearRevaluation:{fy}:{ccy}:{rev}
        if (string.IsNullOrEmpty(sourceEventId)) return 0;
        var parts = sourceEventId.Split(':');
        return parts.Length >= 4 && int.TryParse(parts[3], out var rev) ? rev : 0;
    }

    private static bool EffectMatches(JournalEntry prior, RevaluationCurrencyGroup group, AccountingSettings settings)
    {
        // اثرِ سندِ قبلی روی حساب‌های پولی (به‌جز سود/زیان) با اختلافِ محاسبه‌شدهٔ فعلی مقایسه می‌شود.
        var priorNet = prior.Lines
            .Where(l => l.AccountId != settings.ExchangeGainAccountId && l.AccountId != settings.ExchangeLossAccountId)
            .GroupBy(l => (l.AccountId, l.PartyType, l.PartyId, l.ContractId, l.ShipmentId, l.CashAccountId))
            .ToDictionary(g => g.Key, g => decimal.Round(g.Sum(x => x.Debit - x.Credit), 4, MidpointRounding.AwayFromZero));

        var currentNet = group.Lines
            .GroupBy(l => (l.AccountId, l.PartyType, l.PartyId, l.ContractId, l.ShipmentId, l.CashAccountId))
            .ToDictionary(g => g.Key, g => decimal.Round(g.Sum(x => x.DifferenceUsd), 4, MidpointRounding.AwayFromZero));

        if (priorNet.Count != currentNet.Count)
            return false;
        foreach (var (key, value) in currentNet)
            if (!priorNet.TryGetValue(key, out var pv) || pv != value)
                return false;
        return true;
    }

    // SourceEventId پایدار: FiscalYearRevaluation:{FiscalYearId}:{Currency}:{Revision}
    private static string RevaluationSourceEventId(int fiscalYearId, string currency, int revision)
        => $"FiscalYearRevaluation:{fiscalYearId}:{currency}:{revision}";

    private async Task<JournalEntry> PostRevaluationAsync(
        int companyId, int fiscalYearId, RevaluationCurrencyGroup group, DateTime endDate, int revision,
        AccountingSettings settings, int? userId, CancellationToken cancellationToken)
    {
        var lines = BuildRevaluationLines(group, settings, reverse: false);
        return await posting.PostAsync(new AccountingPostRequest(
            companyId,
            $"REV-{fiscalYearId}-{group.Currency}-{revision}",
            endDate, endDate, endDate,
            SourceModule,
            lines,
            SourceEventId: RevaluationSourceEventId(fiscalYearId, group.Currency, revision),
            SourceEntityType: nameof(FiscalYear),
            SourceEntityId: fiscalYearId,
            Description: $"تسعیر پایان دوره {group.Currency} (Revision {revision})",
            IsAdjustment: true,
            PostedByUserId: userId), cancellationToken);
    }

    private static IReadOnlyCollection<AccountingPostLine> BuildRevaluationLines(
        RevaluationCurrencyGroup group, AccountingSettings settings, bool reverse)
    {
        var lines = new List<AccountingPostLine>();
        foreach (var l in group.Lines)
        {
            var diff = l.DifferenceUsd;
            var amount = Math.Abs(diff);
            var accountDebit = diff > 0m;
            if (reverse) accountDebit = !accountDebit;

            var gainLossAccountId = diff > 0m ? settings.ExchangeGainAccountId : settings.ExchangeLossAccountId;
            var gainLossDebit = diff > 0m ? false : true; // سود بستانکار، زیان بدهکار
            if (reverse) gainLossDebit = !gainLossDebit;

            lines.Add(new AccountingPostLine(
                l.AccountId,
                accountDebit ? amount : 0m,
                accountDebit ? 0m : amount,
                "USD", amount, 1m,
                l.PartyType, l.PartyId, l.ContractId, l.ShipmentId, null, null, l.CashAccountId,
                $"Revaluation {l.Currency} @ {l.ClosingRate}"));

            lines.Add(new AccountingPostLine(
                gainLossAccountId,
                gainLossDebit ? amount : 0m,
                gainLossDebit ? 0m : amount,
                "USD", amount, 1m,
                Description: $"Revaluation {l.Currency} gain/loss"));
        }
        return lines;
    }

    private async Task PostAutoReversalAsync(
        int companyId, int fiscalYearId, RevaluationCurrencyGroup group, DateTime reversalDate, int revision,
        AccountingSettings settings, JournalEntry revaluationJournal, int? userId, CancellationToken cancellationToken)
    {
        var lines = BuildRevaluationLines(group, settings, reverse: true);
        try
        {
            await posting.PostAsync(new AccountingPostRequest(
                companyId,
                $"REVR-{fiscalYearId}-{group.Currency}-{revision}",
                reversalDate, reversalDate, reversalDate,
                SourceModule,
                lines,
                SourceEventId: RevaluationSourceEventId(fiscalYearId, group.Currency, revision) + ":Reversal",
                SourceEntityType: nameof(FiscalYear),
                SourceEntityId: revaluationJournal.Id,
                Description: $"برگشت خودکار تسعیر {group.Currency} (Revision {revision})",
                IsAdjustment: true,
                PostedByUserId: userId), cancellationToken);
        }
        catch (AccountingValidationException ex) when (ex.Code == "DUPLICATE_SOURCE_EVENT")
        {
            // قبلاً ثبت شده — Idempotent.
        }
    }

    private async Task SupersedeAsync(
        JournalEntry active, DateTime reversalDate, int? userId, CancellationToken cancellationToken)
    {
        // برگردانِ سندِ تسعیرِ قبلی و برگشتِ خودکارش، تا Revision جدید اثرِ تکراری نسازد.
        await ReverseIfPostedAsync(active.Id, active.AccountingDate, $"{active.SourceEventId}:Superseded", userId, cancellationToken);

        // برگشتِ خودکار با SourceEventId شناخته می‌شود (سندِ Adjustmentِ مستقل، نه Reversalِ رسمی).
        var autoReversal = await db.JournalEntries.AsNoTracking()
            .FirstOrDefaultAsync(j => j.CompanyId == active.CompanyId
                && j.SourceModule == SourceModule
                && j.SourceEventId == active.SourceEventId + ":Reversal"
                && j.Status == JournalEntryStatus.Posted, cancellationToken);
        if (autoReversal is not null)
            await ReverseIfPostedAsync(autoReversal.Id, autoReversal.AccountingDate,
                $"{active.SourceEventId}:Reversal:Superseded", userId, cancellationToken);
    }

    private async Task ReverseIfPostedAsync(
        int journalId, DateTime accountingDate, string sourceEventId, int? userId, CancellationToken cancellationToken)
    {
        try
        {
            await posting.ReverseAsync(new AccountingReversalRequest(
                journalId,
                $"SUPERSEDE-{journalId}",
                accountingDate,
                SourceModule,
                sourceEventId,
                Description: "Supersede revaluation revision",
                PostedByUserId: userId), cancellationToken);
        }
        catch (AccountingValidationException ex) when (ex.Code is "DUPLICATE_SOURCE_EVENT" or "JOURNAL_ALREADY_REVERSED")
        {
            // قبلاً برگشت خورده — Idempotent.
        }
    }

    private static string ComputeHash(FiscalYearCloseRun run)
    {
        var payload = string.Join("|",
            run.CompanyId, run.FiscalYearId, run.RunType, run.JournalCount,
            run.DebitTotal.ToString(CultureInfo.InvariantCulture),
            run.CreditTotal.ToString(CultureInfo.InvariantCulture),
            run.SourceDataCutoff?.ToString("O"), run.LastJournalEntryId,
            run.ChecklistSnapshotJson, run.ClosingRateSnapshotJson);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(bytes);
    }

    private async Task<(int JournalCount, decimal Debit, decimal Credit, int? LastJournalId, DateTime? LastPostedAt)>
        LoadYearStatsAsync(int companyId, int fiscalYearId, DateTime endDate, CancellationToken cancellationToken)
    {
        var journals = await db.JournalEntries.AsNoTracking()
            .Where(j => j.CompanyId == companyId && j.FiscalYearId == fiscalYearId
                && j.Status == JournalEntryStatus.Posted)
            .Select(j => new { j.Id, j.PostedAt })
            .ToListAsync(cancellationToken);

        var totals = await db.JournalEntryLines.AsNoTracking()
            .Where(l => l.JournalEntry!.CompanyId == companyId
                && l.JournalEntry.FiscalYearId == fiscalYearId
                && l.JournalEntry.Status == JournalEntryStatus.Posted)
            .Select(l => new { l.Debit, l.Credit })
            .ToListAsync(cancellationToken);

        var last = journals.OrderByDescending(j => j.Id).FirstOrDefault();
        return (journals.Count, totals.Sum(t => t.Debit), totals.Sum(t => t.Credit), last?.Id, last?.PostedAt);
    }
}
