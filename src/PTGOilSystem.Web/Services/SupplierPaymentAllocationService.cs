using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using PTGOilSystem.Web.Data;
using PTGOilSystem.Web.Models.Entities;
using PTGOilSystem.Web.Services.Accounting;
using PTGOilSystem.Web.Services.Exceptions;

namespace PTGOilSystem.Web.Services;

public sealed record SupplierPaymentAllocationCreateRequest(
    int PaymentTransactionId,
    int ContractId,
    DateTime AllocationDate,
    decimal AllocatedPaymentAmount,
    decimal ContractCurrencyPerUsdRate,
    string? ReferenceNumber,
    string? Notes,
    string? CreatedByUserName);

public sealed record SupplierPaymentAllocationReverseRequest(
    int AllocationId,
    string ReversalReason,
    string? ReversedByUserName);

public interface ISupplierPaymentAllocationService
{
    Task<decimal> GetAllocatableBalanceUsdAsync(int paymentTransactionId, CancellationToken ct = default);
    Task<SupplierPaymentAllocation> CreateAsync(SupplierPaymentAllocationCreateRequest request, CancellationToken ct = default);
    Task<SupplierPaymentAllocation> ReverseAsync(SupplierPaymentAllocationReverseRequest request, CancellationToken ct = default);
}

/// <summary>
/// تخصیص پیش‌پرداخت آزاد تأمین‌کننده به یک قرارداد خرید.
///
/// این یک «پرداخت جدید» نیست؛ فقط بخشی از پیش‌پرداخت آزاد را به قرارداد منتقل می‌کند.
/// به همین دلیل برای هر تخصیص دو LedgerEntry متوازن (مانند ContractBalanceTransferService)
/// ساخته می‌شود تا اثر خالص روی مانده کلی تأمین‌کننده صفر بماند:
///   - Credit با ContractId = null  → کاهش پیش‌پرداخت آزاد
///   - Debit  با ContractId = قرارداد → انتقال همان مبلغ به قرارداد
/// نرخ‌ها و تمام مبالغ هنگام ثبت قفل می‌شوند و رکورد تخصیص ویرایش/حذف نمی‌شود؛
/// اصلاح فقط از طریق «برگشت تخصیص» با ثبت‌های معکوس انجام می‌شود.
/// </summary>
public sealed class SupplierPaymentAllocationService : ISupplierPaymentAllocationService
{
    public const string LedgerSourceType = "SupplierPaymentAllocation";
    public const string ReversalLedgerSourceType = "SupplierPaymentAllocationReversal";

    private readonly ApplicationDbContext _db;
    private readonly ISupplierPaymentAllocationAccountingAdapter? _accountingAdapter;

    public SupplierPaymentAllocationService(
        ApplicationDbContext db,
        ISupplierPaymentAllocationAccountingAdapter? accountingAdapter = null)
    {
        _db = db;
        _accountingAdapter = accountingAdapter;
    }

    public async Task<decimal> GetAllocatableBalanceUsdAsync(int paymentTransactionId, CancellationToken ct = default)
    {
        var paymentUsd = await _db.PaymentTransactions
            .AsNoTracking()
            .Where(p => p.Id == paymentTransactionId)
            .Select(p => (decimal?)p.AmountUsd)
            .FirstOrDefaultAsync(ct);

        if (paymentUsd is null)
        {
            return 0m;
        }

        var allocated = await _db.SupplierPaymentAllocations
            .AsNoTracking()
            .Where(a => a.PaymentTransactionId == paymentTransactionId && a.Status == SupplierPaymentAllocationStatus.Active)
            .SumAsync(a => (decimal?)a.AllocatedBookAmountUsd, ct) ?? 0m;
        return decimal.Round(paymentUsd.Value - allocated, 4, MidpointRounding.AwayFromZero);
    }

    public async Task<SupplierPaymentAllocation> CreateAsync(
        SupplierPaymentAllocationCreateRequest request,
        CancellationToken ct = default)
    {
        if (request.AllocatedPaymentAmount <= 0m)
        {
            throw new BusinessRuleException(
                "SUPPLIER_PAYMENT_ALLOCATION_AMOUNT_INVALID",
                "مبلغ مصرف‌شده باید بزرگ‌تر از صفر باشد.");
        }

        if (request.ContractCurrencyPerUsdRate <= 0m)
        {
            throw new BusinessRuleException(
                "SUPPLIER_PAYMENT_ALLOCATION_RATE_INVALID",
                "نرخ تبدیل ارز قرارداد باید بزرگ‌تر از صفر باشد.");
        }

        var payment = await _db.PaymentTransactions
            .FirstOrDefaultAsync(p => p.Id == request.PaymentTransactionId, ct)
            ?? throw new BusinessRuleException(
                "SUPPLIER_PAYMENT_ALLOCATION_PAYMENT_NOT_FOUND",
                "پرداخت انتخاب‌شده معتبر نیست.");

        if (!payment.SupplierId.HasValue || payment.PaymentKind != PaymentKind.SupplierPayment)
        {
            throw new BusinessRuleException(
                "SUPPLIER_PAYMENT_ALLOCATION_NOT_SUPPLIER",
                "فقط «پرداخت به تأمین‌کننده» قابل تخصیص به قرارداد است.");
        }

        var contract = await _db.Contracts
            .FirstOrDefaultAsync(c => c.Id == request.ContractId, ct)
            ?? throw new BusinessRuleException(
                "SUPPLIER_PAYMENT_ALLOCATION_CONTRACT_NOT_FOUND",
                "قرارداد انتخاب‌شده معتبر نیست.");

        if (contract.ContractType != ContractType.Purchase)
        {
            throw new BusinessRuleException(
                "SUPPLIER_PAYMENT_ALLOCATION_CONTRACT_NOT_PURCHASE",
                "قرارداد باید قرارداد خرید باشد.");
        }

        if (contract.SupplierId != payment.SupplierId)
        {
            throw new BusinessRuleException(
                "SUPPLIER_PAYMENT_ALLOCATION_SUPPLIER_MISMATCH",
                "قرارداد باید متعلق به همان تأمین‌کنندهٔ پرداخت باشد.");
        }

        // نرخ پرداخت قفل‌شده (برای USD برابر 1). کنوانسیون: AmountUsd = AmountOriginal × FxRateToUsd.
        var paymentFxRateToUsd = payment.AppliedFxRateToUsd ?? 1m;
        var bookAmountUsd = decimal.Round(request.AllocatedPaymentAmount * paymentFxRateToUsd, 4, MidpointRounding.AwayFromZero);
        if (bookAmountUsd <= 0m)
        {
            throw new BusinessRuleException(
                "SUPPLIER_PAYMENT_ALLOCATION_AMOUNT_INVALID",
                "مبلغ مصرف‌شده باید بزرگ‌تر از صفر باشد.");
        }

        var contractCurrency = SystemCurrency.Normalize(contract.Currency);
        var isContractUsd = SystemCurrency.IsBaseCurrency(contractCurrency);
        var perUsdRate = isContractUsd ? 1m : request.ContractCurrencyPerUsdRate;
        var contractFxRateToUsd = isContractUsd
            ? 1m
            : decimal.Round(1m / perUsdRate, 6, MidpointRounding.AwayFromZero);
        var contractCurrencyAmount = decimal.Round(bookAmountUsd * perUsdRate, 4, MidpointRounding.AwayFromZero);

        IDbContextTransaction? transaction = null;
        if (_db.Database.IsRelational())
        {
            transaction = await _db.Database.BeginTransactionAsync(ct);
        }

        try
        {
            // محاسبهٔ مانده قابل تخصیص داخل transaction تا تخصیص هم‌زمان باعث over-allocation نشود.
            var alreadyAllocatedUsd = await _db.SupplierPaymentAllocations
                .Where(a => a.PaymentTransactionId == payment.Id && a.Status == SupplierPaymentAllocationStatus.Active)
                .SumAsync(a => (decimal?)a.AllocatedBookAmountUsd, ct) ?? 0m;
            var allocatableUsd = decimal.Round(payment.AmountUsd - alreadyAllocatedUsd, 4, MidpointRounding.AwayFromZero);

            if (bookAmountUsd > allocatableUsd)
            {
                throw new BusinessRuleException(
                    "SUPPLIER_PAYMENT_ALLOCATION_EXCEEDS_BALANCE",
                    $"مبلغ مصرف‌شده از مانده قابل تخصیص بیشتر است. مانده فعلی: {allocatableUsd:N2} USD.");
            }

            var allocation = new SupplierPaymentAllocation
            {
                PaymentTransactionId = payment.Id,
                ContractId = contract.Id,
                AllocationDate = request.AllocationDate.Date,
                AllocatedPaymentAmount = request.AllocatedPaymentAmount,
                PaymentCurrencyCode = SystemCurrency.Normalize(payment.Currency),
                PaymentFxRateToUsd = paymentFxRateToUsd,
                AllocatedBookAmountUsd = bookAmountUsd,
                ContractCurrencyCode = contractCurrency,
                ContractCurrencyPerUsdRate = perUsdRate,
                ContractCurrencyFxRateToUsd = contractFxRateToUsd,
                AllocatedContractCurrencyAmount = contractCurrencyAmount,
                ReferenceNumber = string.IsNullOrWhiteSpace(request.ReferenceNumber) ? null : request.ReferenceNumber.Trim(),
                Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
                Status = SupplierPaymentAllocationStatus.Active,
                CreatedByUserName = string.IsNullOrWhiteSpace(request.CreatedByUserName) ? null : request.CreatedByUserName.Trim()
            };

            _db.SupplierPaymentAllocations.Add(allocation);
            await _db.SaveChangesAsync(ct);

            _db.LedgerEntries.AddRange(
                BuildLedgerEntry(
                    allocation,
                    payment.SupplierId.Value,
                    LedgerSide.Credit,
                    contractId: null,
                    sourceAmount: allocation.AllocatedPaymentAmount,
                    sourceCurrency: allocation.PaymentCurrencyCode,
                    appliedFxRateToUsd: allocation.PaymentFxRateToUsd,
                    sourceType: LedgerSourceType,
                    description: $"کاهش پیش‌پرداخت آزاد تأمین‌کننده بابت تخصیص به قرارداد {contract.ContractNumber}"),
                BuildLedgerEntry(
                    allocation,
                    payment.SupplierId.Value,
                    LedgerSide.Debit,
                    contractId: contract.Id,
                    sourceAmount: allocation.AllocatedContractCurrencyAmount,
                    sourceCurrency: allocation.ContractCurrencyCode,
                    appliedFxRateToUsd: allocation.ContractCurrencyFxRateToUsd,
                    sourceType: LedgerSourceType,
                    description: $"انتقال پیش‌پرداخت به قرارداد {contract.ContractNumber}"));

            await _db.SaveChangesAsync(ct);

            // Dual-write pilot: journal + legacy ledgers share this transaction, so a
            // posting failure rolls back the whole allocation.
            if (_accountingAdapter is not null)
            {
                await _accountingAdapter.TryPostAllocationAsync(allocation, payment, contract, ct);
            }

            if (transaction is not null)
            {
                await transaction.CommitAsync(ct);
            }

            return allocation;
        }
        catch
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(ct);
            }

            throw;
        }
    }

    public async Task<SupplierPaymentAllocation> ReverseAsync(
        SupplierPaymentAllocationReverseRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.ReversalReason))
        {
            throw new BusinessRuleException(
                "SUPPLIER_PAYMENT_ALLOCATION_REVERSAL_REASON_REQUIRED",
                "دلیل برگشت تخصیص الزامی است.");
        }

        var allocation = await _db.SupplierPaymentAllocations
            .Include(a => a.Contract)
            .Include(a => a.PaymentTransaction)
            .FirstOrDefaultAsync(a => a.Id == request.AllocationId, ct)
            ?? throw new BusinessRuleException(
                "SUPPLIER_PAYMENT_ALLOCATION_NOT_FOUND",
                "تخصیص انتخاب‌شده معتبر نیست.");

        if (allocation.Status != SupplierPaymentAllocationStatus.Active)
        {
            throw new BusinessRuleException(
                "SUPPLIER_PAYMENT_ALLOCATION_ALREADY_REVERSED",
                "این تخصیص قبلاً برگشت داده شده است.");
        }

        var supplierId = allocation.PaymentTransaction?.SupplierId
            ?? throw new BusinessRuleException(
                "SUPPLIER_PAYMENT_ALLOCATION_NOT_SUPPLIER",
                "پرداخت این تخصیص تأمین‌کننده ندارد.");

        IDbContextTransaction? transaction = null;
        if (_db.Database.IsRelational())
        {
            transaction = await _db.Database.BeginTransactionAsync(ct);
        }

        try
        {
            allocation.Status = SupplierPaymentAllocationStatus.Reversed;
            allocation.ReversedAtUtc = DateTime.UtcNow;
            allocation.ReversedByUserName = string.IsNullOrWhiteSpace(request.ReversedByUserName)
                ? null
                : request.ReversedByUserName.Trim();
            allocation.ReversalReason = request.ReversalReason.Trim();

            // ثبت‌های معکوس و جداگانه؛ Ledgerهای اصلی حذف یا ویرایش نمی‌شوند.
            _db.LedgerEntries.AddRange(
                BuildLedgerEntry(
                    allocation,
                    supplierId,
                    LedgerSide.Debit,
                    contractId: null,
                    sourceAmount: allocation.AllocatedPaymentAmount,
                    sourceCurrency: allocation.PaymentCurrencyCode,
                    appliedFxRateToUsd: allocation.PaymentFxRateToUsd,
                    sourceType: ReversalLedgerSourceType,
                    description: $"برگشت تخصیص پیش‌پرداخت — بازگشت به پیش‌پرداخت آزاد (#{allocation.Id})"),
                BuildLedgerEntry(
                    allocation,
                    supplierId,
                    LedgerSide.Credit,
                    contractId: allocation.ContractId,
                    sourceAmount: allocation.AllocatedContractCurrencyAmount,
                    sourceCurrency: allocation.ContractCurrencyCode,
                    appliedFxRateToUsd: allocation.ContractCurrencyFxRateToUsd,
                    sourceType: ReversalLedgerSourceType,
                    description: $"برگشت تخصیص پیش‌پرداخت از قرارداد (#{allocation.Id})"));

            await _db.SaveChangesAsync(ct);

            // Dual-write pilot: independent reversal journal; the original journal is
            // never edited or deleted. Shares this transaction with the legacy rows.
            if (_accountingAdapter is not null
                && allocation.PaymentTransaction is not null
                && allocation.Contract is not null)
            {
                await _accountingAdapter.TryPostReversalAsync(
                    allocation,
                    allocation.PaymentTransaction,
                    allocation.Contract,
                    ct);
            }

            if (transaction is not null)
            {
                await transaction.CommitAsync(ct);
            }

            return allocation;
        }
        catch
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(ct);
            }

            throw;
        }
    }

    private static LedgerEntry BuildLedgerEntry(
        SupplierPaymentAllocation allocation,
        int supplierId,
        LedgerSide side,
        int? contractId,
        decimal sourceAmount,
        string sourceCurrency,
        decimal appliedFxRateToUsd,
        string sourceType,
        string description)
        => new()
        {
            EntryDate = allocation.AllocationDate.Date,
            Side = side,
            AmountUsd = allocation.AllocatedBookAmountUsd,
            Currency = SystemCurrency.BaseCurrencyCode,
            SourceAmount = sourceAmount,
            SourceCurrencyCode = sourceCurrency,
            AppliedFxRateToUsd = appliedFxRateToUsd,
            AppliedFxRateDate = allocation.AllocationDate.Date,
            AppliedFxRateSource = sourceType,
            Description = description,
            SourceType = sourceType,
            SourceId = allocation.Id,
            Reference = allocation.ReferenceNumber,
            SupplierId = supplierId,
            ContractId = contractId
        };
}
