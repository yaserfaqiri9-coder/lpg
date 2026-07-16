# حسابداری دوطرفه و سال مالی — وضعیت پیشرفت و راهنمای ادامه

> آخرین به‌روزرسانی: 2026-07-16
> این سند نقطه‌ی توقف کار و نقشه‌ی ادامه‌ی مراحل را نگه می‌دارد.
> کار در Batchهای کوچک انجام می‌شود؛ هر مرحله Build + Test مستقل دارد.

---

## خلاصه‌ی وضعیت کلی

| مرحله | عنوان | وضعیت |
|------|-------|-------|
| ۰ | تأیید وضعیت موجود | ✅ کامل |
| ۱ | تثبیت Pilot انتقال مانده قرارداد | ✅ کامل |
| ۲ | اتصال Supplier Payment Allocation | ✅ کامل |
| ۳ | مالکیت Company برای حساب‌های نقدی | ✅ کامل (زیرساخت + Backfill + گزارش) |
| ۴ | دریافت و پرداخت | ✅ کامل |
| ۵ | مصارف، کرایه، کمیسیون | ✅ کامل (Adapter + تسویه؛ اتصال ۷ مسیر فرعی باقی) |
| ۶ | خرید، موجودی، بهای تمام‌شده | ⛔ شروع‌نشده |
| ۷ | فروش و COGS | ⛔ شروع‌نشده |
| ۸ | کسری، ضایعات، صراف، تسعیر | ⛔ شروع‌نشده |
| ۹ | Cutover و AccountingReadiness | ⛔ شروع‌نشده |
| ۱۰ | UI ساده‌ی سال مالی | ⛔ شروع‌نشده |
| ۱۱ | قفل دوره و AccountingDate | ⛔ شروع‌نشده |
| ۱۲ | چک‌لیست بستن سال | ⛔ شروع‌نشده |
| ۱۳ | Trial Close | ⛔ شروع‌نشده |
| ۱۴ | Final Close | ⛔ شروع‌نشده |
| ۱۵ | بازگشایی کنترل‌شده | ⛔ شروع‌نشده |

**Build فعلی:** ۰ خطا (۳ هشدار پیش‌موجود و نامرتبط: `_Layout.cshtml` ×۲، `MaintenanceController` EF1002).
**تست‌های Accounting مرتبط:** Accounting Core + DatabaseSafety + ContractBalanceTransfer +
SupplierPaymentAllocation + PaymentCompanyOwnership + مرحله ۴ (۴۳ تست) + **مرحله ۵ (۱۴ تست جدید)**.
**Full Suite:** ۱۰۱۱ پاس / ۱۸ شکست قدیمی (همان لیست baseline زیر، تک‌به‌تک منطبق) + ۰ شکست جدید.

---

## Baseline شکست‌های قدیمی (۱۸ عدد — پیش از این کار وجود داشتند)

این‌ها **قبل از** شروع حسابداری دوطرفه هم شکست بودند و به این تغییرات ربط ندارند
(همگی UI/View یا Loading/Freight هستند). هرگز نباید با شکست جدید اشتباه گرفته شوند:

```
ContractJourneyViewStructureTests.InventoryTransport_Active_Flow_Views_Use_Shared_Ak_Components
SarrafsControllerTests.Details_View_Uses_Two_Clear_Sarraf_Flow_Actions_And_Tabs
SuppliersControllerTests.Details_SarrafSettlement_FallsBack_To_LoadingRubRate_When_Ledger_Has_No_Exact_Rub
SuppliersControllerTests.Details_Separates_Actual_Rub_Paid_From_Rub_Applied_To_Supplier_Claim
InventoryTransportBatchServiceTests.Create_Rejects_Standalone_Operational_Asset_Without_Any_Capacity
ContractsControllerTests.EditPricing_Post_Finalizes_Only_Pending_Loadings_And_Keeps_Finalized
ContractsControllerTests.EditPricing_Post_Does_Not_Reprice_Or_Relock_Finalized_Loading
AuditLogsControllerTests.Index_Default_Request_Returns_Recent_Logs
MasterDataCleanupTests.Sidebar_Exposes_Primary_Items_And_Goods_Logistics_Group
UserManagementTests.Roles_Create_Adds_Custom_Role_And_User_Index_Lists_It
LoadingControllerTests.Create_Post_Allows_Imported_Freight_Without_Linked_ServiceProvider
LoadingControllerTests.Create_Post_Creates_One_Loading_Register_Per_Row_For_Selected_Transport_Type
LoadingControllerTests.Create_Post_Uses_OperationalAsset_And_FreightRate_For_Internal_Truck_Rent
LoadingControllerTests.Loading_Create_View_Uses_Shared_Ak_Form_Contract
LoadingControllerTests.Create_Post_Saves_Truck_Workbook_After_Import_When_Freight_Has_No_Provider
LoadingControllerTests.Create_Post_Uses_ServiceProvider_And_FreightRate_For_Wagon_Railway_Cost
LoadingControllerTests.Create_Post_Uses_ServiceProvider_And_FreightRate_For_Truck_Loading
ContractJourneyControllerTests.Details_Purchase_Summary_And_Costs_Only_Count_Expenses_Of_The_Current_Contract_Within_Shared_Shipment
```

---

## مرحله ۰ — تأیید وضعیت موجود ✅

بررسی‌شده و تأییدشده (بدون تغییر کد):
- هر دو Migration پایه موجود: `20260715162639_AddAccountingCoreAndFiscalCalendar`,
  `20260715165342_SplitEmployeeAndAccruedPayableAccounts`.
- `DatabaseSafetyGuard` فعال؛ دیتابیس‌های `postgres`/`template0`/`template1` هرگز migrate/seed نمی‌شوند.
- `AccountingChartSeeder` در Startup اجرا **نمی‌شود** (فقط DI ثبت شده).
- Auto-migrate در `Program.cs` از `DatabaseSafetyGuard.EnsureMigrationAllowed` عبور می‌کند.
- `ContractBalanceTransferAccountingAdapter` پشت Feature Flag دوگانه (`Accounting.Enabled` +
  `Accounting.Pilots.ContractBalanceTransfer`).
- Journal و Ledger قدیمی داخل یک Transaction ساخته می‌شوند
  (`ContractBalanceTransferService.CreateAsync`).
- `DUPLICATE_SOURCE_EVENT` سند دوم نمی‌سازد.

---

## مرحله ۱ — Pilot انتقال مانده قرارداد ✅

Pilot از قبل موجود بود؛ در این کار فقط **تست‌های کامل** اضافه شد.

- فایل تست جدید: `tests/PTGOilSystem.Web.Tests/ContractBalanceTransferAccountingAdapterTests.cs` (۱۵ تست).
- پوشش: Dual-write واقعی روی PostgreSQL، Mapping طرف‌حساب Supplier، پایداری `SourceEventId`
  (`ContractBalanceTransfer:{TransferId}:Created`)، Duplicate بدون سند دوم، Rollback کامل در دوره‌ی
  بسته، همه‌ی شرایط Skip (Accounting/Pilot off, non-purchase, company/supplier/currency mismatch,
  amount<=0, settings missing/invalid).

Mapping (بدون تغییر منطق موجود):
```
Debit  : Accounts Payable | PartyType=Supplier | PartyId=SupplierId | ContractId=مبدأ
Credit : Accounts Payable | PartyType=Supplier | PartyId=SupplierId | ContractId=مقصد
```

---

## مرحله ۲ — Supplier Payment Allocation ✅

### فایل‌های جدید
- `src/PTGOilSystem.Web/Services/Accounting/SupplierPaymentAllocationAccountingAdapter.cs`
- `tests/PTGOilSystem.Web.Tests/SupplierPaymentAllocationAccountingAdapterTests.cs` (۱۲ تست)

### فایل‌های تغییرکرده
- `Configuration/AccountingOptions.cs` — افزودن `Pilots.SupplierPaymentAllocation` (پیش‌فرض false).
- `appsettings.json` — `Accounting.Pilots.SupplierPaymentAllocation: false`.
- `Services/Accounting/AccountingJournalNumberGenerator.cs` — افزودن
  `ForSupplierPaymentAllocation` (`SPA-...`) و `ForSupplierPaymentAllocationReversal` (`SPAR-...`).
- `Services/SupplierPaymentAllocationService.cs` — تزریق اختیاری Adapter؛
  Dual-write در `CreateAsync` و `ReverseAsync` **داخل همان Transaction قدیمی**.
- `Program.cs` — ثبت DI: `ISupplierPaymentAllocationAccountingAdapter`.

### Mapping (تصمیم مستند)
پیش‌پرداخت آزاد در سیستم فعلی با **`ContractId = null`** ذخیره می‌شود (تأییدشده از کد Legacy:
`SupplierPaymentAllocationService.BuildLedgerEntry` — سطر Credit با `contractId: null`). حدس زده نشد.

```
Debit  : Supplier Prepayment | PartyType=Supplier | PartyId=SupplierId | ContractId=قرارداد مقصد
Credit : Supplier Prepayment | PartyType=Supplier | PartyId=SupplierId | ContractId=null (استخر آزاد)
```

- برگشت: `postingService.ReverseAsync` یک Journal Reversal مستقل می‌سازد؛ سند اصلی ویرایش/حذف نمی‌شود.
- `SourceEventId`ها: `SupplierPaymentAllocation:{AllocationId}:Created` و `:Reversed`.
- اگر برگشت وقتی اجرا شود که سند اصلی هرگز Post نشده (Pilot در زمان ایجاد خاموش بوده)،
  فقط Legacy برگشت می‌خورد و Reversal journal ساخته نمی‌شود (`ORIGINAL_JOURNAL_NOT_POSTED`).

### ⚠️ محدودیت مهم مالکیت Company در این مرحله
`PaymentTransaction`/`CashAccount` در زمان مرحله ۲ فیلد `CompanyId` نداشتند، بنابراین Adapter
**فقط** وقتی Post می‌کند که پرداخت به یک قراردادی متصل باشد که شرکتش == شرکت قرارداد مقصد.
در غیر این‌صورت Skip با یکی از دلایل: `PAYMENT_COMPANY_UNKNOWN`, `PAYMENT_CONTRACT_NOT_FOUND`,
`COMPANY_MISMATCH`. این محدودیت با مرحله ۳ کاهش می‌یابد ولی Adapter هنوز از قرارداد پرداخت
به‌عنوان منبع قطعی استفاده می‌کند (می‌توان بعداً به `payment.CompanyId` مستقیم ارتقا داد — نکته‌ی TODO زیر).

---

## مرحله ۳ — مالکیت Company برای حساب‌های نقدی ✅

### تحلیل (پاسخ به سؤالات مرحله)
- **CashAccount** قبلاً هیچ ربط شرکتی نداشت (فقط Code/Name/Currency). هیچ رابطه‌ی تاریخی قطعی
  برای شرکت وجود ندارد → **Backfill نمی‌شود**، فیلد nullable می‌ماند.
- **PaymentTransaction** شرکت را از روابطش می‌گیرد؛ قطعی‌ترین‌ها: قرارداد پرداخت، فروش/مصرف مرتبط،
  محموله‌ی تک‌شرکتی.
- **Sarraf Payment** شرکت قطعی ندارد (Sarraf سراسری است) → مبهم، null می‌ماند.
- **تراکنش چندقراردادی / محموله‌ی چندشرکتی** → مبهم، null می‌ماند.
- رکوردهای مبهم: هر پرداختی که هیچ‌کدام از مسیرهای قطعی زیر را نداشته باشد.

### فایل‌های جدید
- `src/PTGOilSystem.Web/Data/PaymentCompanyBackfillSql.cs` — SQLهای Backfill **افزایشی و اثبات‌پذیر**
  (فقط ردیف‌هایی که `CompanyId IS NULL`؛ Idempotent؛ هرگز Overwrite نمی‌کند).
- `src/PTGOilSystem.Web/Services/Accounting/CompanyOwnershipReportService.cs` — گزارش فقط‌خواندنی:
  تعداد کل/تعیین‌شده/مبهم پرداخت‌ها + تفکیک مبهم‌ها بر اساس `PaymentKind` + آمار CashAccount.
- `src/PTGOilSystem.Web/Migrations/20260715181837_AddCompanyOwnershipToPaymentsAndCashAccounts.cs`
  — افزودن ستون `CompanyId` nullable + FK(Restrict) + Index به `PaymentTransactions` و `CashAccounts`،
  سپس اجرای Backfill از `PaymentCompanyBackfillSql.Statements`.
- `tests/PTGOilSystem.Web.Tests/PaymentCompanyOwnershipTests.cs` (۳ تست).

### فایل‌های تغییرکرده
- `Models/Entities/FinanceAndAudit.cs` — `CompanyId?`+`Company?` به `CashAccount` و `PaymentTransaction`.
- `Data/ApplicationDbContext.cs` — FK/Index برای هر دو.
- `Program.cs` — ثبت DI: `ICompanyOwnershipReportService`.

### مسیرهای قطعی Backfill (به‌ترتیب اولویت)
1. قرارداد مستقیمِ پرداخت.
2. شرکت صریحِ فروشِ مرتبط.
3. قراردادِ فروشِ مرتبط.
4. قراردادِ مصرفِ مرتبط.
5. محموله‌ای که **همه‌ی** قراردادهایش (junction + primary) یک شرکت دارند.
6. مصرفِ روی محموله‌ی تک‌شرکتی.

هر چیز دیگر (sarraf/driver/employee/manual/چندشرکتی) **null می‌ماند** و در گزارش دیده می‌شود.

### ⚠️ Migration هنوز روی دیتابیس عملیاتی اجرا نشده
طبق قانون، Migration به‌صورت خودکار اجرا **نشد**. برای اعمال:
```
dotnet ef database update --project src/PTGOilSystem.Web   # فقط روی دیتابیس هدف درست
```
بعد از اجرا، گزارش مالکیت را از `CompanyOwnershipReportService` بخوان و تعداد رکوردهای مبهم را ثبت کن.

---

## مرحله ۴ — دریافت و پرداخت ✅

### دو تصمیم تجاری که کاربر تأیید کرد (حدس زده نشد)

1. **پیش‌دریافت مشتری:** سیستم هیچ نشانه‌ای برای آن نداشت (`IsAdvancePayment` طبق کامنت و متن
   راهنمای فرم مخصوص تأمین‌کننده است). کاربر **فیلد صریح جدید** را انتخاب کرد، نه استفاده‌ی
   دوگانه از فیلد تأمین‌کننده.
2. **دامنه‌ی صراف:** کاربر **هر دو مسیر** را تأیید کرد — هم ViaSarraf (غیرنقدی) و هم پرداخت
   نقدی `SarrafSettlement`. mapping دوم در پرامپت اصلی نبود و با تأیید صریح اضافه شد.

### فایل‌های جدید
- `Services/Accounting/PaymentCompanyResolver.cs` — تعیین شرکتِ پرداخت، **فقط‌خواندنی** و دقیقاً
  با همان ترتیب قابل‌اثباتِ `PaymentCompanyBackfillSql` (تا Pilot و Backfill هرگز اختلاف نکنند).
  legacy را نمی‌نویسد.
- `Services/Accounting/PaymentAccountingAdapter.cs` — جریان‌های نقدی.
- `Services/Accounting/ViaSarrafAccountingAdapter.cs` — جریان غیرنقدی صراف.
- `Migrations/20260716092121_AddCustomerAdvanceMarkerToPayments.cs` — فقط یک ستون nullable،
  **بدون Backfill** (رکوردهای قدیمی null می‌مانند و حدس زده نمی‌شوند).
- `tests/.../PaymentAccountingAdapterTests.cs` (۲۹ تست)
- `tests/.../ViaSarrafAccountingAdapterTests.cs` (۶ تست)
- `tests/.../PaymentCompanyResolverTests.cs` (۶ تست)

### فایل‌های تغییرکرده
- `Models/Entities/FinanceAndAudit.cs` — `PaymentTransaction.IsCustomerAdvance` (bool?).
- `Configuration/AccountingOptions.cs` + `appsettings.json` — پنج Flag مستقل (همه پیش‌فرض false).
- `Services/Accounting/AccountingJournalNumberGenerator.cs` — `ForPayment` (`PAY-...`) و
  `ForViaSarrafSupplierPayment` (`VSS-...`).
- `Services/Accounting/SupplierPaymentAllocationAccountingAdapter.cs` — **TODO مرحله ۲ انجام شد**:
  اول `payment.CompanyId`، سپس Fallback به قرارداد پرداخت.
- `Controllers/PaymentsController.cs` — تزریق اختیاری دو Adapter + Dual-write داخل همان
  Transaction قدیمیِ `Create` و `CreateViaSarrafAsync` + bind فیلد جدید در Create/Edit.
- `Models/Payments/PaymentViewModels.cs` + `Views/Payments/Create.cshtml` — چک‌باکس «پیش‌دریافت است».
- `Program.cs` — ثبت DI هر سه سرویس.

### Mapping پیاده‌شده
```
CustomerReceipt    : Dr Cash/Bank            Cr Accounts Receivable   (Party=Customer)
CustomerAdvance    : Dr Cash/Bank            Cr Customer Advance      (Party=Customer)
SupplierPayment    : Dr Accounts Payable     Cr Cash/Bank             (Party=Supplier)
SupplierPrepayment : Dr Supplier Prepayment  Cr Cash/Bank             (Party=Supplier)
SarrafCashPayment  : Dr Accounts Payable     Cr Cash/Bank             (Party=Sarraf)
ViaSarraf (غیرنقدی): Dr Accounts Payable      Cr Accounts Payable
                     (Party=Supplier)         (Party=Sarraf)   — بدون خط نقدی
```
- نوع رویداد از **ماهیت واقعی** پرداخت تعیین می‌شود (`PaymentKind` + نشانه‌های پیش‌پرداخت/پیش‌دریافت)،
  نه از `LedgerSide`؛ چون `LedgerSide` فقط جهت حرکت مانده را می‌گوید و بین «تسویه‌ی مطالبات» و
  «پیش‌دریافت» تفکیک نمی‌کند.
- `CashAccountId` همیشه روی خط نقدی، `PartyType`/`PartyId` همیشه روی خط طرف‌حساب،
  ابعاد Contract/Shipment روی خط طرف‌حساب.
- کل Debit/Credit به ارز Functional (USD) = `payment.AmountUsd`؛ جفت‌ارز روی هر دو خط.

### مسیرهای عمداً متصل‌نشده (Skip با `UNSUPPORTED_PAYMENT_KIND`)
`ManualPayment`, `ManualReceipt`, `ExpensePayment`, `TruckPayment`, `ServiceProviderPayment`,
`CommissionPayment`, `EmployeeSalaryPayment/Advance/Return`, `SupplierReceipt`, `CustomerPayment`.
فرم و منطق فعلی این‌ها تغییر نکرد.

### ⚠️ محدودیت شناخته‌شده — ViaSarraf غیر-USD legacy-only می‌ماند
مسیر ViaSarraf مبلغ دلاری را با **تقسیم** می‌سازد (`amount / documentRate`) ولی نرخ را جداگانه به
۶ رقم گرد می‌کند. بنابراین اتحاد `AmountUsd == round(Amount × FxRateToUsd, 4)` برای RUB برقرار
نیست و Posting Service (که مبلغ Functional را از همان جفت‌ارز بازتولید می‌کند) آن را رد می‌کند.
به‌جای ساختنِ یک نرخ جعلی، این رویدادها با `INVALID_PAYMENT_CONVERSION` **Skip** می‌شوند و
legacy دست‌نخورده ادامه می‌دهد. ViaSarraf با USD عادی پست می‌شود.
رفع آن نیاز به تصمیم درباره‌ی دقت نرخ در مسیر legacy دارد → مرحله‌ی جداگانه.

### ⚠️ Migration هنوز روی دیتابیس عملیاتی اجرا نشده
```
dotnet ef database update --project src/PTGOilSystem.Web   # فقط روی دیتابیس هدف درست
```

---

## مرحله ۵ — مصارف، کرایه، کمیسیون ✅

### سه تصمیم تجاری که کاربر تأیید کرد (حدس زده نشد)

legacy برای مصرف فقط **یک سطر تک‌طرفه** می‌نویسد و طرف مقابل را اصلاً ثبت نمی‌کند:
```
GetExpenseLedgerSide(expense) => expense.ServiceProviderId.HasValue ? Credit : Debit;
```
بنابراین هیچ منبع قابل‌اثباتی برای «حساب بستانکار» وجود نداشت و باید پرسیده می‌شد.

1. **مصرف بدون شرکت خدماتی** → `Cr 2510 Accrued Expense`.
2. **انتخاب حساب بدهی** → **فیلد صریح جدید روی `ExpenseType`**، نه استنتاج از `Category`
   (چون `Category` متن آزاد و قابل‌ویرایش کاربر است و حساب حسابداری نباید با تغییر نام یک
   دسته جابه‌جا شود).
3. **تسویه** → `ExpensePayment` و `CommissionPayment` که در مرحله ۴ عمداً Skip شده بودند،
   در همین مرحله اضافه شدند تا بدهیِ ساخته‌شده تلنبار نشود.

**ترکیب تصمیم ۱ و ۲:** فیلد صریح همیشه حساب بستانکار را تعیین می‌کند (وگرنه تصمیم ۲ بی‌اثر
می‌شد). تصمیم ۱ می‌گوید مقدار درست برای انواع مصرفِ بدون طرف‌حساب `AccruedExpense` است و متن
راهنمای فرم دقیقاً همین را می‌گوید. کمیسیون هم بدون طرف‌حساب است ولی `CommissionPayable` می‌گیرد.

### فایل‌های جدید
- `Services/Accounting/ExpenseAccountingAdapter.cs` — Adapter مصرف + `ExpenseCompanyResolver`
  (شرکت فقط از قرارداد یا محموله‌ی تک‌شرکتی؛ وگرنه Skip).
- `Migrations/20260716095651_AddExpenseTypePayableAccountKind.cs` — یک ستون nullable،
  **بدون Backfill**.
- `tests/.../ExpenseAccountingAdapterTests.cs` (۱۴ تست).

### فایل‌های تغییرکرده
- `Models/Entities/MasterData.cs` — enum `ExpensePayableKind` + `ExpenseType.PayableAccountKind`.
- `Configuration/AccountingOptions.cs` + `appsettings.json` — سه Flag جدید (پیش‌فرض false).
- `Services/Accounting/AccountingJournalNumberGenerator.cs` — `ForExpense` (`EXP-...`).
- `Services/Accounting/PaymentAccountingAdapter.cs` — دو نوع رویداد تسویه.
- `Controllers/ExpensesController.cs` — تزریق اختیاری Adapter + Dual-write در مسیر اصلی Create.
- `Controllers/PaymentsController.cs` — Dual-write کمیسیون (مصرف + خروج نقدی).
- `Controllers/ExpenseTypesController.cs` + `Views/ExpenseTypes/_CreateForm.cshtml` — فیلد جدید.
- `Program.cs` — ثبت DI.

### Mapping پیاده‌شده
```
مصرف/کرایه/کمیسیون : Dr 5200 General Expense    Cr <حساب فیلد صریح ExpenseType>
                     Party = ServiceProvider ?? Driver ?? (بدون طرف‌حساب)

ExpensePayment     : Dr <همان حساب بدهیِ مصرف>   Cr 1100 Cash/Bank
CommissionPayment  : Dr <همان حساب بدهیِ مصرف>   Cr 1100 Cash/Bank
```
حساب و طرف‌حسابِ تسویه **از خود مصرف** خوانده می‌شود، نه از پرداخت — تا تسویه هرگز روی حسابی
غیر از حسابِ تعهد ننشیند (تست‌شده).

### ⚠️ اتصال ناقص — ۷ مسیر دیگر هنوز Dual-write ندارند
`ExpenseTransaction` در ۹ جا ساخته می‌شود. فعلاً فقط دو مسیر متصل شده‌اند:
`ExpensesController` (Create اصلی) و `PaymentsController.PostCashCommissionAsync`.
باقی‌مانده: `DispatchController`, `InventoryTransportLegsController`, `LoadingController`,
`TruckSettlementsController`, `DispatchFreightExpenseSync`, `ExpenseRuleEngine`,
`InventoryTransportReceiptService` — و نیز مسیرهای Edit/Cancel/Batch در خود `ExpensesController`.
Adapter آماده است؛ فقط باید در همان Transaction هر مسیر صدا زده شود. تا آن‌وقت آن مصارف
legacy-only می‌مانند (بدون خطا، چون Flag پیش‌فرض خاموش است).

### ⚠️ لغو مصرف هنوز Reversal نمی‌سازد
مسیر لغو (`IsCancelled` + سند معکوس Ledger) هنوز `postingService.ReverseAsync` را صدا نمی‌زند.
تا وقتی Pilot خاموش است بی‌اثر است، ولی قبل از روشن‌کردن Flag باید اضافه شود.

---

## نقطه‌ی دقیق توقف

آخرین کار انجام‌شده: **مرحله ۵ کامل شد** (Adapter + تسویه + تست)، Build سبز (۰ خطا)،
۱۴ تست جدید سبز، Full Suite = ۱۰۱۱ پاس + همان ۱۸ شکست قدیمی + **۰ شکست جدید**.
هنوز وارد مرحله ۶ **نشده‌ایم**.

سه Migration اجرانشده روی دیتابیس عملیاتی باقی است:
`20260715181837_AddCompanyOwnershipToPaymentsAndCashAccounts` (مرحله ۳)،
`20260716092121_AddCustomerAdvanceMarkerToPayments` (مرحله ۴) و
`20260716095651_AddExpenseTypePayableAccountKind` (مرحله ۵).

هیچ Backfill حدسی انجام نشده. هیچ داده‌ی عملیاتی حذف نشده. تغییرات UI نامرتبط در Working Tree
(stat-cards `.webp`/`.css`، `docs/ui-references/*.png`) **دست‌نخورده** باقی مانده.

### TODO باز (کوچک، برای مرحله‌ی بعد)
`PaymentsController.Create` هنگام ساخت پرداخت جدید `CompanyId` را **نمی‌نویسد**؛ بنابراین
رکوردهای جدید null می‌مانند و Adapter شرکت را در لحظه از روابط قابل‌اثبات حساب می‌کند
(`PaymentCompanyResolver`). این عمدی است تا نوشتنِ legacy تغییر نکند، ولی یعنی ستون Stage 3
برای رکوردهای جدید پر نمی‌شود. اگر تصمیم گرفته شد که پر شود، همان Resolver قابل استفاده است.

---

## از کجا ادامه بدهم؟

### اول: دو بدهیِ باقی‌مانده از مرحله ۵ (کوچک، قبل از مرحله ۶)
1. اتصال Dual-write به ۷ مسیر دیگرِ ساخت مصرف (لیست بالا) — Adapter آماده است.
2. `ReverseAsync` برای لغو مصرف و لغو گروهی.

### بعد: مرحله ۶ — خرید، موجودی، بهای تمام‌شده
1. Mapping پایه (از پرامپت اصلی): رسید خرید → `Dr Inventory / Inventory In Transit`
   در برابر `Cr Accounts Payable تأمین‌کننده`.
2. **قبل از کد، منبع قطعیِ شرکت، مقدار و بهای هر رسید را از کد legacy استخراج کن — حدس ممنوع.**
   به‌ویژه: رابطه‌ی `LoadingReceipt` / `InventoryMovement` / `Contract` و اینکه بهای تمام‌شده
   کجا نهایی می‌شود (`PricingMethod`, `EditPricing`).
3. `Contract` تنها Entity با `CompanyId` الزامی است — احتمالاً منبع قطعی همان است.
4. هر زیرماژول: Build + Test مستقل. اگر تصمیم حسابداری مبهم شد، **توقف و سؤال دقیق**.

### الگوی موجود برای کپی‌برداری (مرجع دست‌اول)
`PaymentAccountingAdapter` (مراحل ۴ و ۵) کامل‌ترین قالب است: تشخیص نوع رویداد از ماهیت واقعی،
Flag مستقل هر زیرماژول، تعیین شرکتِ قابل‌اثبات، Skip بدون اثر روی legacy، Duplicate، بازتولید
مبلغ Functional از جفت‌ارز، و تست PostgreSQL واقعی از طریق `AccountingPostgreSqlFixture`.
`ExpenseAccountingAdapter` نمونه‌ی خوبِ «فیلد صریح به‌جای استنتاج از داده‌ی آزاد» است.

---

## چک‌لیست قوانین (رعایت‌شده تا اینجا)
- ✅ بدون Refactor عمومی / بدون تغییر فایل‌های UI نامرتبط.
- ✅ بدون git reset/checkout/clean/stash روی تغییرات کاربر.
- ✅ فقط فایل‌های Accounting/Fiscal تغییر کرد.
- ✅ Migration/Seeder روی دیتابیس عملیاتی خودکار اجرا نشد.
- ✅ بدون Backfill حدسی (فقط اثبات‌پذیر).
- ✅ LedgerEntry قدیمی حفظ شد.
- ✅ Dual-write + Feature Flag.
- ✅ سند Posted تغییرناپذیر (تست‌شده).
- ✅ هر مرحله Build + Test مستقل.
- ✅ شکست‌های جدید از ۱۸ شکست قدیمی جدا گزارش شد (۰ شکست جدید).
