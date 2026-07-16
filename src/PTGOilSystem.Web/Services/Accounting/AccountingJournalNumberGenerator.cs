namespace PTGOilSystem.Web.Services.Accounting;

public interface IAccountingJournalNumberGenerator
{
    string ForContractBalanceTransfer(int companyId, int transferId);
    string ForSupplierPaymentAllocation(int companyId, int allocationId);
    string ForSupplierPaymentAllocationReversal(int companyId, int allocationId);
    string ForPayment(int companyId, int paymentId);
    string ForViaSarrafSupplierPayment(int companyId, int supplierLedgerEntryId);
    string ForExpense(int companyId, int expenseId);
}

public sealed class AccountingJournalNumberGenerator : IAccountingJournalNumberGenerator
{
    public string ForContractBalanceTransfer(int companyId, int transferId)
    {
        if (companyId <= 0)
            throw new ArgumentOutOfRangeException(nameof(companyId));
        if (transferId <= 0)
            throw new ArgumentOutOfRangeException(nameof(transferId));

        // Transfer ids are database-generated and globally unique. Including the
        // company keeps the number readable while remaining deterministic on retry.
        return $"CBT-{companyId:D6}-{transferId:D10}";
    }

    public string ForSupplierPaymentAllocation(int companyId, int allocationId)
    {
        if (companyId <= 0)
            throw new ArgumentOutOfRangeException(nameof(companyId));
        if (allocationId <= 0)
            throw new ArgumentOutOfRangeException(nameof(allocationId));

        return $"SPA-{companyId:D6}-{allocationId:D10}";
    }

    public string ForSupplierPaymentAllocationReversal(int companyId, int allocationId)
    {
        if (companyId <= 0)
            throw new ArgumentOutOfRangeException(nameof(companyId));
        if (allocationId <= 0)
            throw new ArgumentOutOfRangeException(nameof(allocationId));

        return $"SPAR-{companyId:D6}-{allocationId:D10}";
    }

    public string ForPayment(int companyId, int paymentId)
    {
        if (companyId <= 0)
            throw new ArgumentOutOfRangeException(nameof(companyId));
        if (paymentId <= 0)
            throw new ArgumentOutOfRangeException(nameof(paymentId));

        return $"PAY-{companyId:D6}-{paymentId:D10}";
    }

    public string ForViaSarrafSupplierPayment(int companyId, int supplierLedgerEntryId)
    {
        if (companyId <= 0)
            throw new ArgumentOutOfRangeException(nameof(companyId));
        if (supplierLedgerEntryId <= 0)
            throw new ArgumentOutOfRangeException(nameof(supplierLedgerEntryId));

        // The via-sarraf flow writes no PaymentTransaction, so the supplier ledger row is the
        // only stable, database-generated identity for the event.
        return $"VSS-{companyId:D6}-{supplierLedgerEntryId:D10}";
    }

    public string ForExpense(int companyId, int expenseId)
    {
        if (companyId <= 0)
            throw new ArgumentOutOfRangeException(nameof(companyId));
        if (expenseId <= 0)
            throw new ArgumentOutOfRangeException(nameof(expenseId));

        return $"EXP-{companyId:D6}-{expenseId:D10}";
    }
}
