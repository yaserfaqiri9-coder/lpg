using System;
using System.ComponentModel.DataAnnotations;

namespace PTGOilSystem.Web.Models.Entities;

public enum LedgerSide { Debit = 1, Credit = 2 }
public enum CashAccountType { Cash = 1, Bank = 2, Mixed = 3 }
public enum PaymentDirection { In = 1, Out = 2 }
public enum PaymentKind
{
    CustomerReceipt = 1,
    SupplierPayment = 2,
    ExpensePayment = 3,
    TruckPayment = 4,
    ManualPayment = 5,
    ManualReceipt = 6,
    EmployeeSalaryPayment = 7,
    EmployeeSalaryAdvance = 8,
    SupplierReceipt = 9,
    CustomerPayment = 10,
    EmployeeReturn = 11,
    ServiceProviderPayment = 12,
    SarrafSettlement = 13,
    // خروجِ نقدیِ کمیسیون از صندوق/بانک. فقط cash movement است؛ مصرف واقعی در P&L
    // توسط ExpenseTransaction مرتبط (SourceType="Expense") ثبت می‌شود، نه این پرداخت.
    CommissionPayment = 14
}

public enum SarrafSettlementDifferenceType
{
    None = 0,
    Gain = 1,
    Loss = 2,
    SupplierShortfall = 3
}

// Phase 1 — دلیل تفاوت (فقط ثبت و نمایش، هیچ منطق حسابداری به آن وابسته نیست).
// برای توضیح فرق بین «مبلغی که یک طرف داد» و «مبلغی که طرف دیگر گرفت».
public enum DifferenceReason
{
    [System.ComponentModel.DataAnnotations.Display(Name = "تفاوت نرخ")] FxDifference = 1,
    [System.ComponentModel.DataAnnotations.Display(Name = "کمیشن")] Commission = 2,
    [System.ComponentModel.DataAnnotations.Display(Name = "کرایه حواله")] TransferFee = 3,
    [System.ComponentModel.DataAnnotations.Display(Name = "مارجین دلال")] BrokerMargin = 4,
    [System.ComponentModel.DataAnnotations.Display(Name = "تخفیف")] Discount = 5,
    [System.ComponentModel.DataAnnotations.Display(Name = "اصلاح حساب")] Adjustment = 6,
    [System.ComponentModel.DataAnnotations.Display(Name = "سایر")] Other = 99
}

public enum SarrafSettlementDifferenceTreatment
{
    AcceptedAmountOnly = 1,
    RecognizeExchangeGainLoss = 2
}

public enum SarrafSettlementStatus
{
    Draft = 1,
    Posted = 2,
    Cancelled = 3
}

// جهت عملیاتِ صراف نسبت به شرکت:
//   Out = صراف از طرف شرکت به طرف مقابل پرداخت کرد (ما به صراف بدهکار می‌شویم).
//   In  = صراف برای شرکت از طرف مقابل پول دریافت کرد (صراف به ما بدهکار می‌شود).
// پیش‌فرض Out است تا رکوردهای قدیمی (پرداخت به تأمین‌کننده) بدون تغییر بمانند.
public enum SarrafSettlementDirection
{
    Out = 1,
    In = 2
}

// نوع طرف‌حسابِ تسویهٔ صراف. Phase 2: راننده و کارمند هم اضافه شدند.
// Expense/Customs/Other عمداً اضافه نشده‌اند (بعداً قابل افزودن بدون شکستن داده).
public enum SarrafSettlementCounterpartyType
{
    Supplier = 1,
    Customer = 2,
    ServiceProvider = 3,
    Driver = 4,
    Employee = 5
}

public class CashAccount : BaseEntity
{
    [Required, MaxLength(50)] public string Code { get; set; } = "";
    [Required, MaxLength(200)] public string Name { get; set; } = "";
    // مالکیت شرکت (nullable). هیچ رابطه قطعی تاریخی وجود ندارد، بنابراین Backfill نمی‌شود؛
    // فقط برای رکوردهای جدید/تأییدشده توسط مدیر مالی تعیین می‌گردد. GL جدید بدون Company قطعی
    // به این حساب متصل نمی‌شود.
    public int? CompanyId { get; set; }
    public Company? Company { get; set; }
    public CashAccountType AccountType { get; set; } = CashAccountType.Bank;
    [Required, MaxLength(10)] public string Currency { get; set; } = "USD";
    public bool IsActive { get; set; } = true;
    [MaxLength(50)] public string? AccountNumber { get; set; }
    [MaxLength(150)] public string? BankName { get; set; }
    [MaxLength(150)] public string? Branch { get; set; }
    [MaxLength(1000)] public string? Notes { get; set; }
}

public class PaymentTransaction : BaseEntity
{
    public DateTime PaymentDate { get; set; }
    public PaymentDirection Direction { get; set; }
    public PaymentKind PaymentKind { get; set; }

    // مالکیت شرکت (nullable). Backfill فقط از روابط قابل‌اثبات (قرارداد، فروش، مصرف، محموله
    // تک‌شرکتی) انجام شده؛ رکوردهای مبهم null می‌مانند و به GL جدید متصل نمی‌شوند.
    public int? CompanyId { get; set; }
    public Company? Company { get; set; }

    public int CashAccountId { get; set; }
    public CashAccount? CashAccount { get; set; }

    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public int? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    public int? ServiceProviderId { get; set; }
    public ServiceProvider? ServiceProvider { get; set; }
    public int? SarrafId { get; set; }
    public Sarraf? Sarraf { get; set; }
    public int? DriverId { get; set; }
    public Driver? Driver { get; set; }
    public int? EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    public int? ContractId { get; set; }
    public Contract? Contract { get; set; }
    public int? ShipmentId { get; set; }
    public Shipment? Shipment { get; set; }
    public int? SalesTransactionId { get; set; }
    public SalesTransaction? SalesTransaction { get; set; }
    public int? ExpenseTransactionId { get; set; }
    public ExpenseTransaction? ExpenseTransaction { get; set; }
    public int? TruckDispatchId { get; set; }
    public TruckDispatch? TruckDispatch { get; set; }

    public decimal Amount { get; set; }
    [Required, MaxLength(10)] public string Currency { get; set; } = "USD";
    public decimal? AppliedFxRateToUsd { get; set; }
    public decimal AmountUsd { get; set; }
    [MaxLength(200)] public string? Reference { get; set; }
    [MaxLength(1000)] public string? Description { get; set; }

    // Phase 1 — نشانهٔ «پیش‌پرداخت» برای پرداخت تأمین‌کننده (فقط ثبت/نمایش، nullable).
    // منطق Ledger و مانده تغییری نمی‌کند؛ فقط در صورت‌حساب جدا نشان داده می‌شود.
    public bool? IsAdvancePayment { get; set; }

    public int? LedgerEntryId { get; set; }
    public LedgerEntry? LedgerEntry { get; set; }

    // کمیسیون — لینک ردیابی. روی پرداخت/دریافت اصلی: ExpenseTransactionِ کمیسیون مرتبط.
    // ستون سادهٔ nullable بدون navigation/FK (backward-compatible). برای نمایش و ویرایش کمیسیون.
    public int? RelatedExpenseTransactionId { get; set; }
}

public class Sarraf : BaseEntity
{
    [Required, MaxLength(200)] public string Name { get; set; } = "";
    [MaxLength(50)] public string? PhoneNumber { get; set; }
    [MaxLength(300)] public string? Address { get; set; }
    [MaxLength(1000)] public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<SarrafSettlement> Settlements { get; set; } = new List<SarrafSettlement>();
    public ICollection<PaymentTransaction> PaymentTransactions { get; set; } = new List<PaymentTransaction>();
}

public class SarrafSettlement : BaseEntity
{
    public DateTime SettlementDate { get; set; }

    // جهت و نوع طرف‌حساب (Phase 1 عمومی‌سازی). پیش‌فرض Out/Supplier تا رفتار قبلی حفظ شود.
    public SarrafSettlementDirection Direction { get; set; } = SarrafSettlementDirection.Out;
    public SarrafSettlementCounterpartyType CounterpartyType { get; set; } = SarrafSettlementCounterpartyType.Supplier;

    public int SarrafId { get; set; }
    public Sarraf? Sarraf { get; set; }
    public int? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    // طرف‌حساب‌های جدید (فقط یکی هم‌زمان پر می‌شود، بسته به CounterpartyType).
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public int? ServiceProviderId { get; set; }
    public ServiceProvider? ServiceProvider { get; set; }
    public int? DriverId { get; set; }
    public Driver? Driver { get; set; }
    public int? EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public int? ContractId { get; set; }
    public Contract? Contract { get; set; }
    public int? PaymentTransactionId { get; set; }
    public PaymentTransaction? PaymentTransaction { get; set; }
    public int? CashAccountId { get; set; }
    public CashAccount? CashAccount { get; set; }

    [MaxLength(200)] public string? ReferenceNumber { get; set; }
    [MaxLength(1000)] public string? Description { get; set; }

    public decimal RequestedAmount { get; set; }
    [Required, MaxLength(10)] public string RequestedCurrency { get; set; } = "USD";
    public decimal RequestedFxRateToUsd { get; set; } = 1m;
    public decimal RequestedAmountUsd { get; set; }

    [Required, MaxLength(10)] public string SarrafCurrency { get; set; } = "AFN";
    public decimal SarrafRate { get; set; }
    public decimal SarrafChargedAmount { get; set; }
    public decimal SarrafFxRateToUsd { get; set; }
    public decimal SarrafChargedAmountUsd { get; set; }

    public decimal SupplierAcceptedAmount { get; set; }
    [Required, MaxLength(10)] public string SupplierAcceptedCurrency { get; set; } = "USD";
    public decimal SupplierAcceptedFxRateToUsd { get; set; } = 1m;
    public decimal SupplierAcceptedAmountUsd { get; set; }
    public decimal? SupplierRate { get; set; }

    public decimal DifferenceAmountUsd { get; set; }
    public SarrafSettlementDifferenceType DifferenceType { get; set; } = SarrafSettlementDifferenceType.None;
    // Phase 1 — دلیل تفاوت (فقط ثبت/نمایش، nullable). به منطق تسویه وابسته نیست.
    public DifferenceReason? DifferenceReason { get; set; }
    public SarrafSettlementDifferenceTreatment DifferenceTreatment { get; set; } = SarrafSettlementDifferenceTreatment.AcceptedAmountOnly;
    public SarrafSettlementStatus Status { get; set; } = SarrafSettlementStatus.Draft;

    public DateTime? PostedAtUtc { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
    [MaxLength(500)] public string? CancelReason { get; set; }

    public int? LedgerEntryId { get; set; }
    public LedgerEntry? LedgerEntry { get; set; }
    public int? ExchangeDifferenceLedgerEntryId { get; set; }
    public LedgerEntry? ExchangeDifferenceLedgerEntry { get; set; }
}

public class LedgerEntry : BaseEntity
{
    public DateTime EntryDate { get; set; }
    public LedgerSide Side { get; set; }
    public decimal AmountUsd { get; set; }
    [MaxLength(10)] public string Currency { get; set; } = "USD";
    public decimal? SourceAmount { get; set; }
    [MaxLength(10)] public string? SourceCurrencyCode { get; set; }
    public decimal? AppliedFxRateToUsd { get; set; }
    public DateTime? AppliedFxRateDate { get; set; }
    [MaxLength(500)] public string? AppliedFxRateSource { get; set; }
    [MaxLength(500)] public string Description { get; set; } = "";

    // Mandatory traceability (rule #9)
    [Required, MaxLength(50)] public string SourceType { get; set; } = ""; // Sale / Expense / Adjustment / ...
    public int SourceId { get; set; }

    // Human-readable trace reference (e.g. invoice number, BOL, voucher).
    // Optional but indexed; complements (SourceType, SourceId) for fast search.
    [MaxLength(200)] public string? Reference { get; set; }

    public int? ContractId { get; set; }
    public Contract? Contract { get; set; }
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public int? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    public int? ServiceProviderId { get; set; }
    public ServiceProvider? ServiceProvider { get; set; }
    // راننده/موتروانِ مستقل (مالکِ موترِ شخصی). کرایه/بدهیِ او روی همین حساب می‌نشیند تا صورت‌حساب راننده کامل شود.
    public int? DriverId { get; set; }
    public Driver? Driver { get; set; }
    public int? EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public int? ShipmentId { get; set; }
    public Shipment? Shipment { get; set; }
}

public class ContractBalanceTransfer : BaseEntity
{
    public DateTime TransferDate { get; set; }
    public int FromContractId { get; set; }
    public Contract? FromContract { get; set; }
    public int ToContractId { get; set; }
    public Contract? ToContract { get; set; }
    public decimal AmountOriginal { get; set; }
    [Required, MaxLength(10)] public string CurrencyCode { get; set; } = "USD";
    public decimal FxRateToUsd { get; set; }
    public decimal AmountUsd { get; set; }
    public DateTime? FxRateDate { get; set; }
    [MaxLength(500)] public string? FxRateSource { get; set; }
    public int? OriginalPaymentTransactionId { get; set; }
    public PaymentTransaction? OriginalPaymentTransaction { get; set; }
    public decimal? OriginalPaymentFxRateToUsd { get; set; }
    [MaxLength(200)] public string? Reference { get; set; }
    [MaxLength(1000)] public string? Notes { get; set; }
    public bool IsCancelled { get; set; }
}

public enum SupplierPaymentAllocationStatus
{
    Active = 1,
    Reversed = 2
}

// تخصیص پیش‌پرداخت تأمین‌کننده به یک قرارداد خرید.
// این «پرداخت جدید» نیست؛ فقط بخشی از پیش‌پرداخت آزاد تأمین‌کننده را به همان قرارداد منتقل می‌کند،
// بنابراین اثر خالص آن روی مانده کلی تأمین‌کننده صفر است (دو LedgerEntry متوازن مانند ContractBalanceTransfer).
public class SupplierPaymentAllocation : BaseEntity
{
    public int PaymentTransactionId { get; set; }
    public PaymentTransaction? PaymentTransaction { get; set; }
    public int ContractId { get; set; }
    public Contract? Contract { get; set; }

    public DateTime AllocationDate { get; set; }

    // مبلغ مصرف‌شده به ارز پرداخت (مثلاً 650,000 USD)
    public decimal AllocatedPaymentAmount { get; set; }
    [Required, MaxLength(10)] public string PaymentCurrencyCode { get; set; } = "USD";
    public decimal PaymentFxRateToUsd { get; set; } = 1m;

    // ارزش دفتری به USD — مبنای مانده قابل تخصیص و ثبت‌های Ledger
    public decimal AllocatedBookAmountUsd { get; set; }

    // ارز قرارداد و نرخ‌های قفل‌شده
    [Required, MaxLength(10)] public string ContractCurrencyCode { get; set; } = "USD";
    // نرخ قابل‌فهم: «هر ۱ دلار چند واحد ارز قرارداد» (مثلاً 80 برای RUB/USD)
    public decimal ContractCurrencyPerUsdRate { get; set; } = 1m;
    // کنوانسیون داخلی سیستم: AmountUsd = AmountOriginal × FxRateToUsd → برای ارز قرارداد = 1 / PerUsd
    public decimal ContractCurrencyFxRateToUsd { get; set; } = 1m;
    // معادل ارز قرارداد (مثلاً 52,000,000 RUB)
    public decimal AllocatedContractCurrencyAmount { get; set; }

    [MaxLength(200)] public string? ReferenceNumber { get; set; }
    [MaxLength(1000)] public string? Notes { get; set; }

    public SupplierPaymentAllocationStatus Status { get; set; } = SupplierPaymentAllocationStatus.Active;

    // اصلاح فقط با «برگشت تخصیص»؛ این رکورد ویرایش/حذف نمی‌شود.
    public int? ReversalOfAllocationId { get; set; }
    public DateTime? ReversedAtUtc { get; set; }
    [MaxLength(150)] public string? ReversedByUserName { get; set; }
    [MaxLength(500)] public string? ReversalReason { get; set; }

    [MaxLength(150)] public string? CreatedByUserName { get; set; }
}

public static class AuditLogCategories
{
    public const string Entity = "Entity";
    public const string Request = "Request";
    public const string Authentication = "Authentication";
    public const string Security = "Security";
    public const string System = "System";
}

public class AuditLog : BaseEntity
{
    public DateTime ActionAtUtc { get; set; } = DateTime.UtcNow;
    public int? ActorUserId { get; set; }
    [MaxLength(150)] public string? ActorUsername { get; set; }
    [Required, MaxLength(30)] public string Category { get; set; } = AuditLogCategories.Entity;
    [MaxLength(100)] public string? Module { get; set; }
    [Required, MaxLength(100)] public string EntityName { get; set; } = "";
    public int EntityId { get; set; }
    [Required, MaxLength(20)] public string Action { get; set; } = ""; // Insert / Update / Delete
    [MaxLength(500)] public string? Description { get; set; }
    [MaxLength(4000)] public string? Diff { get; set; }
    [MaxLength(10)] public string? HttpMethod { get; set; }
    [MaxLength(260)] public string? RequestPath { get; set; }
    [MaxLength(100)] public string? ControllerName { get; set; }
    [MaxLength(100)] public string? ActionName { get; set; }
    public int? StatusCode { get; set; }
    public bool IsSuccess { get; set; } = true;
    [MaxLength(80)] public string? CorrelationId { get; set; }
    [MaxLength(80)] public string? IpAddress { get; set; }
    [MaxLength(1000)] public string? UserAgent { get; set; }
    public long? DurationMs { get; set; }
    [MaxLength(4000)] public string? MetadataJson { get; set; }
}
