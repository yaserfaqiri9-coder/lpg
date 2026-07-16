namespace PTGOilSystem.Web.Configuration;

public sealed class AccountingOptions
{
    public const string SectionName = "Accounting";

    // Stage 2 is infrastructure-only. Operational posting remains opt-in.
    public bool Enabled { get; set; }
    public string DefaultFunctionalCurrencyCode { get; set; } = "USD";
    public AccountingPilotOptions Pilots { get; set; } = new();
}

public sealed class AccountingPilotOptions
{
    public bool ContractBalanceTransfer { get; set; }
    public bool SupplierPaymentAllocation { get; set; }

    // Stage 4 — receipts and payments. Each cash sub-module owns an independent flag so a
    // single mapping can be piloted without exposing the others.
    public bool CustomerReceipt { get; set; }
    public bool CustomerAdvance { get; set; }
    public bool SupplierPayment { get; set; }
    public bool SupplierPrepayment { get; set; }
    public bool SarrafPayment { get; set; }

    // Stage 5 — expenses, freight, commission. Expense accrues the liability; the two
    // payment flags settle it, so they are normally enabled together with Expense.
    public bool Expense { get; set; }
    public bool ExpensePayment { get; set; }
    public bool CommissionPayment { get; set; }

    // Stage 6 — purchase and inventory. Purchase raises the supplier payable against goods in
    // transit; InventoryReceipt moves those goods from in-transit into inventory, so the two
    // are normally enabled together.
    public bool Purchase { get; set; }
    public bool InventoryReceipt { get; set; }
}
