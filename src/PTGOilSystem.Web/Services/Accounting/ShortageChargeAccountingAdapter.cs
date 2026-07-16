using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PTGOilSystem.Web.Configuration;
using PTGOilSystem.Web.Data;
using PTGOilSystem.Web.Models.Entities;

namespace PTGOilSystem.Web.Services.Accounting;

public sealed record ShortageChargeAccountingResult(
    PaymentPostingStatus Status,
    JournalEntry? Journal,
    string? Reason);

public interface IShortageChargeAccountingAdapter
{
    Task<ShortageChargeAccountingResult> TryPostShortageChargeAsync(
        InventoryTransportReceipt receipt,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Stage 8 dual-write pilot for the shortage a carrier is charged for.
///
///   Dr Freight Payable  (party = service provider or driver)
///   Cr Inventory Loss
///
/// The debit mirrors the single legacy row exactly: SourceType "ShortageCharge", one Debit of
/// ShortageChargeUsd against the responsible carrier. Freight Payable is that carrier's control
/// account — the same account Stage 5 credits when their freight is accrued — so debiting it is
/// what "they owe us for what did not arrive" means in a chart with one payable per party type.
/// The legacy figure for the freight itself is untouched here, exactly as the legacy flow leaves
/// it untouched; the two meet in the carrier's balance rather than in one row.
///
/// The credit answers the confirmed decision: the charge recovers the loss, so it offsets
/// account 5400 rather than being recognised as separate income. The recovery and the write-off
/// therefore net against each other, which is what makes a fully recovered shortage cost nothing.
///
/// Amounts are USD at rate 1 because ShortageChargeUsd is derived in USD, so the rounding trap
/// that keeps non-USD via-sarraf payments legacy-only cannot arise here.
///
/// Known limitation — no reversal path: the legacy group-transfer cancellation cancels the
/// freight expenses and their ledger rows but leaves the "ShortageCharge" row standing, and no
/// other path deletes it. There is no legacy cancellation to mirror, so this adapter posts only.
/// Should that row ever gain a cancellation, this needs a matching reversal before the flag is
/// enabled.
/// </summary>
public sealed class ShortageChargeAccountingAdapter(
    ApplicationDbContext db,
    IAccountingPostingService postingService,
    IAccountingJournalNumberGenerator journalNumberGenerator,
    IOptions<AccountingOptions> options,
    ILogger<ShortageChargeAccountingAdapter> logger)
    : IShortageChargeAccountingAdapter
{
    public const string SourceModule = "ShortageCharge";
    public const string SourceEntityType = nameof(InventoryTransportReceipt);

    private readonly AccountingOptions _options = options.Value;

    public async Task<ShortageChargeAccountingResult> TryPostShortageChargeAsync(
        InventoryTransportReceipt receipt,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(receipt);

        if (!_options.Enabled)
            return Skipped(receipt, 0, "ACCOUNTING_DISABLED");
        if (!_options.Pilots.ShortageCharge)
            return Skipped(receipt, 0, "PILOT_DISABLED");

        var leg = await db.InventoryTransportLegs
            .AsNoTracking()
            .Where(x => x.Id == receipt.InventoryTransportLegId)
            .Select(x => new { x.Id, x.DriverId, x.SourcePurchaseContractId, x.ShipmentId })
            .SingleOrDefaultAsync(cancellationToken);
        if (leg is null)
            return Skipped(receipt, 0, "TRANSPORT_LEG_NOT_FOUND");

        // The legacy row's own gates, mirrored: a company-owned truck is never charged, and the
        // charge lands on the service provider when there is one, otherwise the driver.
        if (receipt.IsCancelled)
            return Skipped(receipt, 0, "RECEIPT_CANCELLED");
        if (receipt.OperationalAssetId.HasValue)
            return Skipped(receipt, 0, "OPERATIONAL_ASSET_NOT_CHARGED");

        var amountUsd = receipt.ShortageChargeUsd ?? 0m;
        if (amountUsd <= 0m)
            return Skipped(receipt, 0, "NO_SHORTAGE_CHARGE");

        var partyType = receipt.ServiceProviderId.HasValue
            ? AccountingPartyType.ServiceProvider
            : AccountingPartyType.Driver;
        var partyId = receipt.ServiceProviderId ?? leg.DriverId;
        if (partyId is null)
            return Skipped(receipt, 0, "PARTY_MISSING");

        var (companyId, skipReason) = await ResolveCompanyAndSkipReasonAsync(
            leg.SourcePurchaseContractId, cancellationToken);
        if (skipReason is not null)
            return Skipped(receipt, companyId, skipReason);

        var sourceEventId = BuildCreatedSourceEventId(receipt.Id);
        var existing = await FindJournalAsync(companyId, sourceEventId, cancellationToken);
        if (existing is not null)
        {
            LogOutcome(receipt, companyId, amountUsd, existing.Lines.Sum(x => x.Debit),
                PaymentPostingStatus.Duplicate, "DUPLICATE_SOURCE_EVENT");
            return new ShortageChargeAccountingResult(
                PaymentPostingStatus.Duplicate, existing, "DUPLICATE_SOURCE_EVENT");
        }

        var settings = await db.AccountingSettings
            .AsNoTracking()
            .SingleAsync(x => x.CompanyId == companyId, cancellationToken);

        var request = new AccountingPostRequest(
            companyId,
            journalNumberGenerator.ForShortageCharge(companyId, receipt.Id),
            receipt.ReceiptDate.Date,
            receipt.ReceiptDate.Date,
            receipt.ReceiptDate.Date,
            SourceModule,
            [
                new AccountingPostLine(
                    settings.FreightPayableAccountId,
                    Debit: amountUsd,
                    Credit: 0m,
                    SystemCurrency.BaseCurrencyCode,
                    amountUsd,
                    1m,
                    partyType,
                    partyId,
                    ContractId: leg.SourcePurchaseContractId,
                    ShipmentId: leg.ShipmentId,
                    Description: $"Shortage charged for transport leg #{leg.Id}, receipt #{receipt.Id}"),
                new AccountingPostLine(
                    settings.InventoryLossAccountId,
                    Debit: 0m,
                    Credit: amountUsd,
                    SystemCurrency.BaseCurrencyCode,
                    amountUsd,
                    1m,
                    ContractId: leg.SourcePurchaseContractId,
                    ShipmentId: leg.ShipmentId,
                    Description: "Shortage recovered from carrier")
            ],
            SourceEventId: sourceEventId,
            SourceEntityType: SourceEntityType,
            SourceEntityId: receipt.Id,
            Description: $"Shortage charge for transport receipt #{receipt.Id} on {receipt.ReceiptDate:yyyy-MM-dd}");

        try
        {
            var journal = await postingService.PostAsync(request, cancellationToken);
            LogOutcome(receipt, companyId, amountUsd, journal.Lines.Sum(x => x.Debit),
                PaymentPostingStatus.Posted, null);
            return new ShortageChargeAccountingResult(PaymentPostingStatus.Posted, journal, null);
        }
        catch (Exception exception)
        {
            LogFailure(receipt, exception);
            throw;
        }
    }

    public static string BuildCreatedSourceEventId(int transportReceiptId)
        => $"ShortageCharge:{transportReceiptId}:Created";

    private async Task<(int CompanyId, string? SkipReason)> ResolveCompanyAndSkipReasonAsync(
        int? sourcePurchaseContractId,
        CancellationToken cancellationToken)
    {
        // The source purchase contract is the only provable company for a transport leg.
        if (!sourcePurchaseContractId.HasValue)
            return (0, "SHORTAGE_COMPANY_UNKNOWN");

        var companyId = await db.Contracts
            .AsNoTracking()
            .Where(x => x.Id == sourcePurchaseContractId.Value)
            .Select(x => (int?)x.CompanyId)
            .SingleOrDefaultAsync(cancellationToken);
        if (companyId is null)
            return (0, "SHORTAGE_CONTRACT_NOT_FOUND");

        var settings = await db.AccountingSettings
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.CompanyId == companyId.Value, cancellationToken);
        if (settings is null)
            return (companyId.Value, "ACCOUNTING_SETTINGS_MISSING");
        if (!string.Equals(settings.FunctionalCurrencyCode?.Trim(), "USD", StringComparison.OrdinalIgnoreCase))
            return (companyId.Value, "UNSUPPORTED_FUNCTIONAL_CURRENCY");

        var accountIds = new[] { settings.FreightPayableAccountId, settings.InventoryLossAccountId };
        var validAccountCount = await db.Accounts.AsNoTracking().CountAsync(
            x => accountIds.Contains(x.Id) && x.CompanyId == companyId.Value && x.IsActive,
            cancellationToken);
        if (validAccountCount != accountIds.Distinct().Count())
            return (companyId.Value, "ACCOUNTING_SETTINGS_INVALID_ACCOUNTS");

        return (companyId.Value, null);
    }

    private async Task<JournalEntry?> FindJournalAsync(
        int companyId,
        string sourceEventId,
        CancellationToken cancellationToken)
        => await db.JournalEntries
            .AsNoTracking()
            .Include(x => x.Lines)
            .SingleOrDefaultAsync(
                x => x.CompanyId == companyId
                    && x.SourceModule == SourceModule
                    && x.SourceEventId == sourceEventId,
                cancellationToken);

    private ShortageChargeAccountingResult Skipped(
        InventoryTransportReceipt receipt,
        int companyId,
        string reason)
    {
        LogOutcome(receipt, companyId, receipt.ShortageChargeUsd ?? 0m, 0m, PaymentPostingStatus.Skipped, reason);
        return new ShortageChargeAccountingResult(PaymentPostingStatus.Skipped, null, reason);
    }

    private void LogOutcome(
        InventoryTransportReceipt receipt,
        int companyId,
        decimal expectedAmountUsd,
        decimal journalDebitTotal,
        PaymentPostingStatus status,
        string? reason)
    {
        // Legacy writes one Debit row of ShortageChargeUsd, so the journal must debit the same.
        logger.LogInformation(
            "Shortage charge accounting pilot comparison: TransportReceiptId {TransportReceiptId}, CompanyId {CompanyId}, ServiceProviderId {ServiceProviderId}, ShortageQuantityMt {ShortageQuantityMt}, LegacyAmountUsd {LegacyAmountUsd}, JournalDebitTotal {JournalDebitTotal}, Difference {Difference}, PostingStatus {PostingStatus}, SkipOrFailureReason {SkipOrFailureReason}",
            receipt.Id,
            companyId,
            receipt.ServiceProviderId,
            receipt.ShortageQuantityMt,
            expectedAmountUsd,
            journalDebitTotal,
            journalDebitTotal - expectedAmountUsd,
            status,
            reason);
    }

    private void LogFailure(InventoryTransportReceipt receipt, Exception exception)
    {
        var failureReason = exception is AccountingValidationException validation
            ? validation.Code
            : exception.GetType().Name;
        logger.LogError(
            exception,
            "Shortage charge accounting pilot posting failed for TransportReceiptId {TransportReceiptId} with FailureReason {FailureReason}",
            receipt.Id,
            failureReason);
    }
}
