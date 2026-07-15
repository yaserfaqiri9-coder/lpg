using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PTGOilSystem.Web.Configuration;
using PTGOilSystem.Web.Data;
using PTGOilSystem.Web.Models.Entities;

namespace PTGOilSystem.Web.Services.Accounting;

public enum SupplierPaymentAllocationPostingStatus
{
    Skipped = 0,
    Posted = 1,
    Duplicate = 2
}

public sealed record SupplierPaymentAllocationAccountingResult(
    SupplierPaymentAllocationPostingStatus Status,
    JournalEntry? Journal,
    string? Reason);

public interface ISupplierPaymentAllocationAccountingAdapter
{
    Task<SupplierPaymentAllocationAccountingResult> TryPostAllocationAsync(
        SupplierPaymentAllocation allocation,
        PaymentTransaction payment,
        Contract contract,
        CancellationToken cancellationToken = default);

    Task<SupplierPaymentAllocationAccountingResult> TryPostReversalAsync(
        SupplierPaymentAllocation allocation,
        PaymentTransaction payment,
        Contract contract,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Dual-write pilot: mirrors supplier prepayment allocations into the double-entry
/// journal while the legacy ledger remains the operational source.
///
/// Mapping (per allocation):
///   Debit  Supplier Prepayment — PartyType=Supplier, ContractId = destination contract
///   Credit Supplier Prepayment — PartyType=Supplier, ContractId = null (free prepayment pool,
///                                matching the legacy credit row exactly)
///
/// Company ownership: PaymentTransaction/CashAccount carry no CompanyId today, so the
/// company is only definite when the payment itself is bound to a contract whose company
/// equals the destination contract's company. Otherwise the pilot skips and legacy
/// behaviour continues unchanged (Stage 3 adds provable company ownership).
///
/// Both journal lines use the payment-currency pair (AllocatedPaymentAmount ×
/// PaymentFxRateToUsd) because AllocatedBookAmountUsd is rounded from exactly that
/// product, which the posting service re-validates. The contract-currency figures stay
/// on the allocation record itself.
/// </summary>
public sealed class SupplierPaymentAllocationAccountingAdapter(
    ApplicationDbContext db,
    IAccountingPostingService postingService,
    IAccountingJournalNumberGenerator journalNumberGenerator,
    IOptions<AccountingOptions> options,
    ILogger<SupplierPaymentAllocationAccountingAdapter> logger)
    : ISupplierPaymentAllocationAccountingAdapter
{
    public const string SourceModule = "SupplierPaymentAllocation";
    public const string SourceEntityType = "SupplierPaymentAllocation";

    private readonly AccountingOptions _options = options.Value;

    public async Task<SupplierPaymentAllocationAccountingResult> TryPostAllocationAsync(
        SupplierPaymentAllocation allocation,
        PaymentTransaction payment,
        Contract contract,
        CancellationToken cancellationToken = default)
    {
        var (companyId, skipReason) = await ResolveCompanyAndSkipReasonAsync(
            allocation,
            payment,
            contract,
            cancellationToken);
        if (skipReason is not null)
        {
            LogOutcome(allocation, contract, companyId, payment.SupplierId, 0m,
                SupplierPaymentAllocationPostingStatus.Skipped.ToString(), skipReason, "Created");
            return new SupplierPaymentAllocationAccountingResult(
                SupplierPaymentAllocationPostingStatus.Skipped, null, skipReason);
        }

        var sourceEventId = BuildCreatedSourceEventId(allocation.Id);
        var existing = await FindJournalAsync(companyId, sourceEventId, cancellationToken);
        if (existing is not null)
        {
            LogOutcome(allocation, contract, companyId, payment.SupplierId,
                existing.Lines.Sum(x => x.Debit),
                SupplierPaymentAllocationPostingStatus.Duplicate.ToString(), "DUPLICATE_SOURCE_EVENT", "Created");
            return new SupplierPaymentAllocationAccountingResult(
                SupplierPaymentAllocationPostingStatus.Duplicate, existing, "DUPLICATE_SOURCE_EVENT");
        }

        var settings = await db.AccountingSettings
            .AsNoTracking()
            .SingleAsync(x => x.CompanyId == companyId, cancellationToken);
        var supplierId = payment.SupplierId!.Value;

        var request = new AccountingPostRequest(
            companyId,
            journalNumberGenerator.ForSupplierPaymentAllocation(companyId, allocation.Id),
            allocation.AllocationDate.Date,
            allocation.AllocationDate.Date,
            allocation.AllocationDate.Date,
            SourceModule,
            [
                new AccountingPostLine(
                    settings.SupplierPrepaymentAccountId,
                    Debit: allocation.AllocatedBookAmountUsd,
                    Credit: 0m,
                    allocation.PaymentCurrencyCode,
                    allocation.AllocatedPaymentAmount,
                    allocation.PaymentFxRateToUsd,
                    AccountingPartyType.Supplier,
                    supplierId,
                    ContractId: contract.Id,
                    Description: $"Prepayment allocated to contract {contract.ContractNumber}"),
                new AccountingPostLine(
                    settings.SupplierPrepaymentAccountId,
                    Debit: 0m,
                    Credit: allocation.AllocatedBookAmountUsd,
                    allocation.PaymentCurrencyCode,
                    allocation.AllocatedPaymentAmount,
                    allocation.PaymentFxRateToUsd,
                    AccountingPartyType.Supplier,
                    supplierId,
                    ContractId: null,
                    Description: "Free supplier prepayment reduced by allocation")
            ],
            SourceEventId: sourceEventId,
            SourceEntityType: SourceEntityType,
            SourceEntityId: allocation.Id,
            Description: $"Supplier prepayment allocation #{allocation.Id} to contract {contract.ContractNumber}");

        try
        {
            var journal = await postingService.PostAsync(request, cancellationToken);
            LogOutcome(allocation, contract, companyId, supplierId,
                journal.Lines.Sum(x => x.Debit),
                SupplierPaymentAllocationPostingStatus.Posted.ToString(), null, "Created");
            return new SupplierPaymentAllocationAccountingResult(
                SupplierPaymentAllocationPostingStatus.Posted, journal, null);
        }
        catch (Exception exception)
        {
            LogFailure(allocation, exception, "Created");
            throw;
        }
    }

    public async Task<SupplierPaymentAllocationAccountingResult> TryPostReversalAsync(
        SupplierPaymentAllocation allocation,
        PaymentTransaction payment,
        Contract contract,
        CancellationToken cancellationToken = default)
    {
        var (companyId, skipReason) = await ResolveCompanyAndSkipReasonAsync(
            allocation,
            payment,
            contract,
            cancellationToken);
        if (skipReason is not null)
        {
            LogOutcome(allocation, contract, companyId, payment.SupplierId, 0m,
                SupplierPaymentAllocationPostingStatus.Skipped.ToString(), skipReason, "Reversed");
            return new SupplierPaymentAllocationAccountingResult(
                SupplierPaymentAllocationPostingStatus.Skipped, null, skipReason);
        }

        var reversedEventId = BuildReversedSourceEventId(allocation.Id);
        var existingReversal = await FindJournalAsync(companyId, reversedEventId, cancellationToken);
        if (existingReversal is not null)
        {
            LogOutcome(allocation, contract, companyId, payment.SupplierId,
                existingReversal.Lines.Sum(x => x.Debit),
                SupplierPaymentAllocationPostingStatus.Duplicate.ToString(), "DUPLICATE_SOURCE_EVENT", "Reversed");
            return new SupplierPaymentAllocationAccountingResult(
                SupplierPaymentAllocationPostingStatus.Duplicate, existingReversal, "DUPLICATE_SOURCE_EVENT");
        }

        var original = await FindJournalAsync(
            companyId,
            BuildCreatedSourceEventId(allocation.Id),
            cancellationToken);
        if (original is null)
        {
            // Allocation was created legacy-only (pilot off / skipped); the reversal
            // stays legacy-only too so both books remain internally consistent.
            LogOutcome(allocation, contract, companyId, payment.SupplierId, 0m,
                SupplierPaymentAllocationPostingStatus.Skipped.ToString(), "ORIGINAL_JOURNAL_NOT_POSTED", "Reversed");
            return new SupplierPaymentAllocationAccountingResult(
                SupplierPaymentAllocationPostingStatus.Skipped, null, "ORIGINAL_JOURNAL_NOT_POSTED");
        }

        var request = new AccountingReversalRequest(
            original.Id,
            journalNumberGenerator.ForSupplierPaymentAllocationReversal(companyId, allocation.Id),
            allocation.AllocationDate.Date,
            SourceModule,
            reversedEventId,
            Description: $"Reversal of supplier prepayment allocation #{allocation.Id}");

        try
        {
            var journal = await postingService.ReverseAsync(request, cancellationToken);
            LogOutcome(allocation, contract, companyId, payment.SupplierId,
                journal.Lines.Sum(x => x.Debit),
                SupplierPaymentAllocationPostingStatus.Posted.ToString(), null, "Reversed");
            return new SupplierPaymentAllocationAccountingResult(
                SupplierPaymentAllocationPostingStatus.Posted, journal, null);
        }
        catch (Exception exception)
        {
            LogFailure(allocation, exception, "Reversed");
            throw;
        }
    }

    public static string BuildCreatedSourceEventId(int allocationId)
        => $"SupplierPaymentAllocation:{allocationId}:Created";

    public static string BuildReversedSourceEventId(int allocationId)
        => $"SupplierPaymentAllocation:{allocationId}:Reversed";

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

    private async Task<(int CompanyId, string? SkipReason)> ResolveCompanyAndSkipReasonAsync(
        SupplierPaymentAllocation allocation,
        PaymentTransaction payment,
        Contract contract,
        CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
            return (0, "ACCOUNTING_DISABLED");
        if (!_options.Pilots.SupplierPaymentAllocation)
            return (0, "PILOT_DISABLED");
        if (payment.PaymentKind != PaymentKind.SupplierPayment || !payment.SupplierId.HasValue)
            return (0, "NOT_SUPPLIER_PAYMENT");
        if (contract.ContractType != ContractType.Purchase)
            return (0, "CONTRACT_NOT_PURCHASE");
        if (contract.SupplierId != payment.SupplierId)
            return (0, "SUPPLIER_MISMATCH");
        if (allocation.AllocatedPaymentAmount <= 0m || allocation.AllocatedBookAmountUsd <= 0m)
            return (0, "INVALID_ALLOCATION_AMOUNT");
        if (SystemCurrency.IsBaseCurrency(allocation.PaymentCurrencyCode)
            && allocation.PaymentFxRateToUsd != 1m)
            return (0, "INVALID_PAYMENT_FX");

        // Company is only provable through the payment's own contract today.
        if (!payment.ContractId.HasValue)
            return (0, "PAYMENT_COMPANY_UNKNOWN");
        var paymentContractCompanyId = await db.Contracts
            .AsNoTracking()
            .Where(x => x.Id == payment.ContractId.Value)
            .Select(x => (int?)x.CompanyId)
            .SingleOrDefaultAsync(cancellationToken);
        if (paymentContractCompanyId is null)
            return (0, "PAYMENT_CONTRACT_NOT_FOUND");
        if (paymentContractCompanyId.Value != contract.CompanyId)
            return (0, "COMPANY_MISMATCH");

        var companyId = contract.CompanyId;
        var settings = await db.AccountingSettings
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.CompanyId == companyId, cancellationToken);
        if (settings is null)
            return (companyId, "ACCOUNTING_SETTINGS_MISSING");
        if (!string.Equals(settings.FunctionalCurrencyCode?.Trim(), "USD", StringComparison.OrdinalIgnoreCase))
            return (companyId, "UNSUPPORTED_FUNCTIONAL_CURRENCY");

        var configuredAccountIds = GetConfiguredAccountIds(settings);
        if (configuredAccountIds.Any(x => x <= 0)
            || configuredAccountIds.Distinct().Count() != configuredAccountIds.Length)
            return (companyId, "ACCOUNTING_SETTINGS_INCOMPLETE");

        var validAccountCount = await db.Accounts.AsNoTracking().CountAsync(
            x => configuredAccountIds.Contains(x.Id)
                && x.CompanyId == companyId
                && x.IsActive,
            cancellationToken);
        if (validAccountCount != configuredAccountIds.Length)
            return (companyId, "ACCOUNTING_SETTINGS_INVALID_ACCOUNTS");

        var prepaymentIsValid = await db.Accounts.AsNoTracking().AnyAsync(
            x => x.Id == settings.SupplierPrepaymentAccountId
                && x.CompanyId == companyId
                && x.IsActive,
            cancellationToken);
        return prepaymentIsValid ? (companyId, null) : (companyId, "SUPPLIER_PREPAYMENT_INVALID");
    }

    private static int[] GetConfiguredAccountIds(AccountingSettings settings)
        =>
        [
            settings.CashBankControlAccountId,
            settings.AccountsReceivableAccountId,
            settings.AccountsPayableAccountId,
            settings.InventoryAccountId,
            settings.InventoryInTransitAccountId,
            settings.SupplierPrepaymentAccountId,
            settings.CustomerAdvanceAccountId,
            settings.FreightPayableAccountId,
            settings.CommissionPayableAccountId,
            settings.EmployeeAdvanceAccountId,
            settings.EmployeePayableAccountId,
            settings.AccruedExpenseAccountId,
            settings.SalesRevenueAccountId,
            settings.CostOfGoodsSoldAccountId,
            settings.GeneralExpenseAccountId,
            settings.ExchangeGainAccountId,
            settings.ExchangeLossAccountId,
            settings.InventoryLossAccountId,
            settings.CurrentYearProfitLossAccountId,
            settings.RetainedEarningsAccountId
        ];

    private void LogOutcome(
        SupplierPaymentAllocation allocation,
        Contract contract,
        int companyId,
        int? supplierId,
        decimal journalDebitTotal,
        string status,
        string? reason,
        string eventKind)
    {
        // Legacy writes two balanced rows of AllocatedBookAmountUsd each.
        logger.LogInformation(
            "Supplier payment allocation pilot comparison: AllocationId {AllocationId}, EventKind {EventKind}, CompanyId {CompanyId}, ContractId {ContractId}, SupplierId {SupplierId}, LegacyAmountUsd {LegacyAmountUsd}, JournalDebitTotal {JournalDebitTotal}, Difference {Difference}, PostingStatus {PostingStatus}, SkipOrFailureReason {SkipOrFailureReason}",
            allocation.Id,
            eventKind,
            companyId,
            contract.Id,
            supplierId,
            allocation.AllocatedBookAmountUsd,
            journalDebitTotal,
            journalDebitTotal - allocation.AllocatedBookAmountUsd,
            status,
            reason);
    }

    private void LogFailure(SupplierPaymentAllocation allocation, Exception exception, string eventKind)
    {
        var failureReason = exception is AccountingValidationException validation
            ? validation.Code
            : exception.GetType().Name;
        logger.LogError(
            exception,
            "Supplier payment allocation accounting pilot posting failed for AllocationId {AllocationId} ({EventKind}) with FailureReason {FailureReason}",
            allocation.Id,
            eventKind,
            failureReason);
    }
}
