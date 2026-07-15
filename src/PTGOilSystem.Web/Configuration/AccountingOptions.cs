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
}
