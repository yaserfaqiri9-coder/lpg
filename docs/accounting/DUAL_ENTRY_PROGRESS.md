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
| ۵ | مصارف، کرایه، کمیسیون | ✅ کامل (همه‌ی ۹ مسیر + Reversal لغو) |
| ۶ | خرید، موجودی، بهای تمام‌شده | ✅ خرید + موجودی + Reversal (COGS در مرحله ۷) |
| ۷ | فروش و COGS | ✅ کامل (میانگین موزون متحرک) |
| ۸ | کسری، ضایعات، صراف | ✅ کامل (تسعیر با تأیید کاربر به مرحله ۱۳ موکول شد) |
| ۸.۵ | انتقال بین ترمینال‌ها (بستن بدهی‌ها) | ✅ کامل (بدهی ۱ بسته؛ ۲ و ۳ منتظر تصمیم) |
| ۹ | Cutover و AccountingReadiness | ✅ کامل |
| ۱۰ | UI ساده‌ی سال مالی | ✅ کامل |
| ۱۱ | قفل دوره و AccountingDate | ✅ کامل |
| ۱۲ | چک‌لیست بستن سال | ✅ کامل (فقط‌خواندنی + Export JSON/CSV) |
| ۱۳ | Trial Close (+ تسعیر پایان‌دوره) | ✅ کامل (Migration ساخته‌شده، اجرانشده) |
| ۱۴ | Final Close | ⛔ شروع‌نشده |
| ۱۵ | بازگشایی کنترل‌شده | ⛔ شروع‌نشده |

**Build فعلی:** ۰ خطا (۳ هشدار پیش‌موجود و نامرتبط: `_Layout.cshtml` ×۲، `MaintenanceController` EF1002).
**تست‌های Accounting مرتبط:** Accounting Core + DatabaseSafety + ContractBalanceTransfer +
SupplierPaymentAllocation + PaymentCompanyOwnership + مرحله ۴ (۴۳ تست) + مرحله ۵ (۱۴ تست) +
مرحله ۶ (۱۱ تست) + Reversalها (۱۱ تست) + مرحله ۷ (۱۷ تست) + مرحله ۸ (۳۲ تست) +
**انتقال بین ترمینال‌ها (۱۹ تست جدید)**.

**✅ Full Suite کامل اجرا شد** (اولین اجرای کامل بعد از مرحله ۶): **۱۱۰۰ پاس / ۱۹ شکست**.
تفکیک آن ۱۹ شکست:
- ۱۸ تا **دقیقاً** همان baseline قدیمی زیر است.
- ۱ تا `ContractJourneyViewStructureTests.InventoryTransportLeg_Details_Uses_Transport_Detail_Reference_Layout`
  است که **از کار UI ناتمامِ داخل Working Tree** می‌آید، نه از حسابداری. اثبات: تست فایل
  `Views/InventoryTransportLegs/Details.cshtml` را مستقیم از دیسک می‌خواند و دنبال رشتهٔ
  `class="ak-summary ak-detail-summary"` می‌گردد؛ این رشته **در HEAD هست** ولی در نسخهٔ
  Working Tree صفر بار (بلوک summary در ویرایش UI برداشته شده). هیچ‌کدام از تغییرات
  حسابداری هیچ View‌ای را دست نمی‌زند. آن فایل عمداً دست‌نخورده رها شد.

→ **۰ شکست جدید از حسابداری.**

---

## Baseline شکست‌های قدیمی (۱۶ عدد — پیش از این کار وجود داشتند)

این‌ها **قبل از** شروع حسابداری دوطرفه هم شکست بودند و به این تغییرات ربط ندارند
(همگی UI/View یا Loading/Freight هستند). هرگز نباید با شکست جدید اشتباه گرفته شوند:

> ۲ شکست از این فهرست (`EditPricing_Post_Finalizes_Only_Pending_Loadings_And_Keeps_Finalized` و
> `EditPricing_Post_Does_Not_Reprice_Or_Relock_Finalized_Loading`) **باگ واقعی بودند، نه تست کهنه**،
> و در «اصلاح قاعدهٔ #9» پایین‌تر برطرف شدند. باقی‌ماندهٔ Baseline اکنون ۱۶ عدد است.

```
ContractJourneyViewStructureTests.InventoryTransport_Active_Flow_Views_Use_Shared_Ak_Components
SarrafsControllerTests.Details_View_Uses_Two_Clear_Sarraf_Flow_Actions_And_Tabs
SuppliersControllerTests.Details_SarrafSettlement_FallsBack_To_LoadingRubRate_When_Ledger_Has_No_Exact_Rub
SuppliersControllerTests.Details_Separates_Actual_Rub_Paid_From_Rub_Applied_To_Supplier_Claim
InventoryTransportBatchServiceTests.Create_Rejects_Standalone_Operational_Asset_Without_Any_Capacity
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

### ✅ اتصال کامل — هر ۹ مسیر ساخت مصرف
`ExpensesController` (Create + Batch)، `PaymentsController.PostCashCommissionAsync`،
`DispatchController` (مستقیم + سه wrapper کرایه)، `InventoryTransportLegsController` (۲ نقطه)،
`LoadingController` (۲ نقطه)، `TruckSettlementsController`، `DispatchFreightExpenseSync`،
`ExpenseRuleEngine`، `InventoryTransportReceiptService`.

`DispatchFreightExpenseSync` کلاس static است، پس Adapter پارامتر اختیاری گرفت (نه تزریق).
مسیر به‌روزرسانی‌اش هم Adapter را صدا می‌زند؛ مبلغِ تغییرنکرده Duplicate می‌شود و بی‌اثر است.

### ✅ Reversal لغو مصرف
`Expense:{id}:Reversed` — Idempotent. در این مسیرها وصل است: لغو تکی، لغو گروهی،
`LoadingController.CancelLoadingServiceExpenseAsync`، `DispatchFreightExpenseSync.CancelExpenseAsync`.
همه **قبل** از علامت‌خوردن `IsCancelled` صدا زده می‌شوند تا Adapter شرکت را از همان روابط
قبلی حل کند. اگر سند اصلی هرگز پست نشده باشد → `ORIGINAL_JOURNAL_NOT_POSTED` و فقط legacy
برگشت می‌خورد. تاریخ Reversal مثل legacy امروز است، نه بازکردن دورهٔ قبلی.

---

## مرحله ۶ — خرید و موجودی ✅ (COGS به مرحله ۷ موکول شد)

### سه تصمیم تجاری که کاربر تأیید کرد (حدس زده نشد)

کشف کلیدی: legacy **دو مفهوم موازیِ طلب تأمین‌کننده** دارد و هیچ‌کدام کامل نیست:
- فقط بارگیری‌های **روبلیِ قفل‌شده** سطر لجر `SourceType="Loading"` می‌سازند
  (`IsRubSettlement && RubRateStatus == Locked && AmountUsdAtRubLock > 0`).
- بارگیری‌های **دلاری هیچ سطر لجری ندارند**؛ طلبشان فقط با تجمیع
  (`PurchaseAggregationService`) محاسبه می‌شود.

1. **رویداد خرید** → **هر بارگیریِ قیمت‌دار** (دلاری و روبلی)، با همان حساب تجمیع.
2. **طرف بدهکار** → بارگیری به `1310 Inventory In Transit`؛ رسید به `1300 Inventory` منتقلش می‌کند.
3. **تغییر قیمت** → **Reversal سند قبلی + سند جدید** (سند Posted هرگز ویرایش نمی‌شود).

### فایل‌های جدید
- `Services/Accounting/PurchaseAccountingAdapter.cs`
- `tests/.../PurchaseAccountingAdapterTests.cs` (۱۱ تست)

### فایل‌های تغییرکرده
- `Configuration/AccountingOptions.cs` + `appsettings.json` — `Purchase` و `InventoryReceipt`.
- `Services/Accounting/AccountingJournalNumberGenerator.cs` — `ForPurchase` (`PUR-...`)،
  `ForPurchaseReversal` (`PURR-...`)، `ForInventoryReceipt` (`INV-...`).
- `Controllers/LoadingController.cs` — Dual-write داخل `PostSupplierLoadingLedgerIfReadyAsync`
  (عمداً **بیرون** شرط روبلی، تا بارگیری دلاری هم پست شود).
- `Controllers/LoadingReceiptsController.cs` — Dual-write رسید.
- `Controllers/ContractsController.cs` — `PostRepricedPurchasesAsync` بعد از هر سه مسیر
  بازقیمت‌گذاری (`Edit`، `EditPricing`، `RepricePurchaseLoadings`).
- `Program.cs` — ثبت DI.

### Mapping پیاده‌شده
```
خرید (بارگیریِ قیمت‌دار) : Dr 1310 Inventory In Transit   Cr 2100 Accounts Payable
                          مبلغ = round(LoadedQuantityMt × effectivePrice, 4)   (Party=Supplier)

رسید کالا               : Dr 1300 Inventory              Cr 1310 Inventory In Transit
                          مبلغ = round(ReceivedQuantityMt × effectivePrice, 4)
```
- `effectivePrice` = `LoadingPriceUsd` بارگیری، وگرنه قیمت نهایی قرارداد — **دقیقاً** همان
  fallback ای که `PurchaseAggregationService` استفاده می‌کند.
- بدون قیمت → `PURCHASE_PRICE_PENDING` (همان‌طور که تجمیع هم آن را Pending می‌شمارد).
- ارز همیشه USD با نرخ ۱ (چون `LoadingPriceUsd` دلاری است) — پس **تله‌ی گرد کردن روبل که در
  ViaSarraf داشتیم اینجا اصلاً پیش نمی‌آید**.
- اختلاف مقدارِ بارگیری و رسید (کسری) عمداً در `1310` باقی می‌ماند تا **مرحله ۸** آن را
  به‌عنوان ضایعات بشناسد (تست‌شده).

### نسخه‌بندی و بازقیمت‌گذاری
`SourceEventId` = `Purchase:{loadingId}:Created:{revision}`. اگر مبلغ عوض نشده باشد →
`Duplicate` (بی‌اثر). اگر عوض شده باشد → `ReverseAsync` نسخهٔ قبلی + پست نسخهٔ بعدی.
سند قبلی Posted و دست‌نخورده می‌ماند و تاریخچه کامل حفظ می‌شود (تست‌شده: اثر خالص = قیمت جدید).

### ⚠️ اختلاف مورد انتظار با سطر legacy روبلی
برای بارگیری‌های روبلیِ قفل‌شده، legacy مبلغ را از `AmountUsdAtRubLock` می‌گیرد ولی دفتر کل
جدید از `LoadedQuantityMt × effectivePrice`. این دو می‌توانند فرق کنند. عمداً از حساب تجمیع
استفاده شد (تصمیم ۱)؛ لاگ مقایسه اختلاف را نشان می‌دهد. قبل از روشن‌کردن Flag باید روی
داده‌ی واقعی بررسی شود.

### ✅ Reversal خرید و رسید — با یک تصحیح مهم
یادداشت قبلی این سند («لغو بارگیری/رسید هنوز Reversal نمی‌سازد») **فرض غلطی بود**. بررسی کد
نشان داد `LoadingRegister` و `LoadingReceipt` اصلاً **قابل لغو نیستند**: نه فیلد `IsCancelled`
دارند، نه هیچ مسیر Delete/Cancel در کنترلرها. پس هیچ نقطه‌ای برای اتصال وجود ندارد.

دو متد Reversal با تست کامل ساخته شدند و **عمداً بدون فراخواننده** می‌مانند تا وقتی مسیر لغو
واقعی اضافه شود:
- `TryPostPurchaseReversalAsync` — همهٔ نسخه‌های پست‌شده را برمی‌گرداند (نه فقط آخری).
  **محافظ:** اگر رسیدی هنوز پست باشد، `RECEIPT_STILL_POSTED` می‌دهد و کاری نمی‌کند؛ وگرنه
  «در راه» با بستانکارِ بی‌پشتوانه می‌ماند. اول رسید باید برگردد (تست‌شده).
- `TryPostInventoryReceiptReversalAsync` — کالا را به «در راه» برمی‌گرداند.

هر دو Idempotent. تنها مسیر واقعیِ Reversal در مرحله ۶ همان بازقیمت‌گذاری است که از قبل کار می‌کرد.

---

## مرحله ۷ — فروش و COGS ✅

### دو تصمیم تجاری که کاربر تأیید کرد (حدس زده نشد)
1. **روش ارزش‌گذاری** → **میانگین موزون متحرک**، در سطح **شرکت + محصول + ترمینال**.
   `InventoryLineage` فقط ردیابی منشأ و مقدار است و **روش مستقل ارزش‌گذاری نیست**.
2. **کمبود موجودی** → درآمد پست شود، **COGS با `INVENTORY_NOT_VALUED` رها شود**.

### چرا یک جدول جدید لازم بود
دفتر کل فقط پول را نگه می‌دارد؛ `JournalEntryLine` هیچ فیلد مقداری ندارد. برای دانستن «هر تن
الان چند است» هم پول لازم است هم تن. پس `InventoryAverageCosts` ساخته شد:
`(CompanyId, ProductId, TerminalId)` یکتا + `QuantityMt` + `TotalValueUsd`.
**میانگین هرگز ذخیره نمی‌شود** — همیشه `TotalValueUsd / QuantityMt` است تا نتواند از دو عددی
که از آن‌ها می‌آید جدا بیفتد. Check constraint مانع منفی‌شدن است.

### فایل‌های جدید
- `Models/Entities/Accounting/InventoryAverageCost.cs`
- `Services/Accounting/InventoryValuationService.cs` — تنها مرجع ارزش‌گذاری.
- `Services/Accounting/SalesAccountingAdapter.cs`
- `Migrations/20260716114201_AddInventoryAverageCost.cs` — فقط جدول جدید، هیچ جدول موجودی
  تغییر نکرد، بدون Backfill.
- `tests/.../SalesAccountingAdapterTests.cs`

### Mapping پیاده‌شده
```
فروش : Dr 1200 Accounts Receivable   Cr 4100 Sales Revenue   (Party=Customer)
COGS : Dr 5100 Cost of Goods Sold    Cr 1300 Inventory
```
- **دو سند جدا با دو Flag جدا.** درآمد همان لحظه معلوم است؛ بها فقط وقتی معلوم است که خریدِ
  متناظر ارزش‌گذاری شده باشد. جدا بودنشان یعنی فروش هرگز گروگانِ بهایش نمی‌شود.
- مقدار و ترمینالِ COGS از همان سطرهای `InventoryMovement` نوع Out خوانده می‌شود که خود فروش
  نوشته — نه محاسبهٔ دوباره. فروشِ چندترمینالی از هر کاسه جدا برمی‌دارد.
- **اگر یکی از کاسه‌ها کم بیاورد، هیچ‌کدام مصرف نمی‌شوند** و آنچه قبلاً برداشته شده برگردانده
  می‌شود؛ فروشِ نیمه‌ارزش‌گذاری‌شده بدتر از ارزش‌گذاری‌نشده است (تست‌شده).
- برداشتِ کلِ کاسه، کلِ ارزش را می‌برد (نه `مقدار × میانگین`) تا خردهٔ گرد کردن جا نماند و
  میانگین را به‌مرور مسموم نکند (تست‌شده).

### اتصال — هر ۷ مسیر فروش
`SalesController` (۲ نقطه) + `SalesController.Group` (۲ نقطه) + `DispatchController` +
`LoadingReceiptsController` (فروش مستقیم) + `InventoryTransportReceiptService`.
رسیدِ مرحله ۶ هم حالا کاسه را پر می‌کند؛ Reversal رسید کاسه را خالی می‌کند و اگر کالا قبلاً
فروخته شده باشد `INVENTORY_ALREADY_CONSUMED` می‌دهد و دست نمی‌زند.

### ⚠️ محدودیت شناخته‌شده — انتقال بین ترمینال‌ها بها را منتقل نمی‌کند
چون کلید کاسه شامل ترمینال است، `MovementDirection.Transfer` باید بها را هم جابه‌جا کند ولی
هنوز هیچ مسیری این سرویس را برای انتقال صدا نمی‌زند. یعنی انتقال، مقدار را در نمای موجودیِ
legacy عوض می‌کند بدون اینکه هیچ کاسه‌ای تکان بخورد، و فروش در مقصد با میانگینِ قبلیِ همان
مقصد ارزش‌گذاری می‌شود. **قبل از روشن‌کردن Flag `Cogs` باید بسته شود.**

---

## مرحله ۸ — کسری، ضایعات، صراف ✅

### پنج تصمیم تجاری که کاربر تأیید کرد (حدس زده نشد)
1. **ضایعات** → فقط Stageهایی که واقعاً موجودی را کم می‌کنند
   (`TankNaturalLoss`، `ManualAdjustment`، `TankFinalSettlement`) سند می‌گیرند.
2. **کسری** → `Cr 5400 Inventory Loss`؛ یعنی وصولی از راننده زیان ضایعات را جبران می‌کند،
   نه اینکه درآمد جدا باشد.
3. **دامنهٔ صراف** → هم `SarrafSettlement` کامل، هم `ThreeWaySettlement` با `PayeeType=Sarraf`.
4. **بدهی ما به صراف** → `SarrafChargedAmountUsd` (چیزی که صراف واقعاً از ما گرفت).
5. **تسعیر پایان‌دوره** → **موکول به مرحله ۱۳** (Trial Close). هیچ مسیر legacy ندارد.

### ⚠️ سه مورد از چهار مورد، mirror نیستند — رفتار جدیدند
این را باید قبل از هر بازبینی دانست:
- **ضایعات**: `LossEvent` فقط موجودی را تکان می‌دهد و **صفر سطر دفتر** می‌نویسد. حساب `5400`
  تا امروز هرگز پست نشده بود. پس عددِ سند، **اولین بیان پولیِ ضایعات در کل سیستم** است و هیچ
  عدد legacy برای مقایسه ندارد.
- **طرف صراف در `SarrafSettlement`**: legacy فقط یک سطر طرف‌حساب می‌نویسد و «ماندهٔ صراف» را
  در حافظه بازمی‌سازد (`SarrafsController.Details`). سطر صراف اصلاً وجود ندارد.
- **تسعیر**: نه Entity، نه سرویس، نه Migration. `4200`/`5300` هرگز پست نشده‌اند.

### فایل‌های جدید
- `Services/Accounting/InventoryLossAccountingAdapter.cs`
- `Services/Accounting/ShortageChargeAccountingAdapter.cs`
- `Services/Accounting/SarrafSettlementAccountingAdapter.cs`
- `Services/Accounting/ThreeWaySettlementAccountingAdapter.cs`
- `tests/.../Stage8AccountingAdapterTests.cs` (۳۲ تست)

**هیچ Migration جدیدی لازم نشد** — هیچ Entity، DbContext یا ساختار دیتابیسی تغییر نکرد.

### Mapping پیاده‌شده
```
ضایعات   : Dr 5400 Inventory Loss    Cr 1300 Inventory
           مبلغ = میانگین موزون متحرکِ همان کاسه‌ای که COGS از آن می‌خورد
کسری     : Dr 2300 Freight Payable   Cr 5400 Inventory Loss   (Party=ServiceProvider یا Driver)
           مبلغ = ShortageChargeUsd، دلاری با نرخ ۱
صراف     : Dr/Cr حساب کنترلِ طرف‌حساب  = SupplierLedgerAmountUsd
           Cr/Dr 2100 AP (Party=Sarraf) = SarrafChargedAmountUsd
           اختلاف → 5300 Exchange Loss یا 4200 Exchange Gain
سه‌جانبه : Dr 2100 AP (Party=Supplier) = SupplierAcceptedUsd
           Cr 1200 AR (Party=Customer) = CustomerPaidUsd
           اختلاف → 5300 یا 4200؛ **هیچ سطر صرافی ندارد**
```

- **ضایعات**: مقدار از همان `InventoryMovement` می‌آید که legacy نوشته، نه محاسبهٔ دوباره.
  ارزش از `IInventoryValuationService` — همان مرجعی که COGS را قیمت می‌گذارد، پس ضایعات و
  فروش از یک کاسه هرگز سر بهای کالا اختلاف پیدا نمی‌کنند. کاسه کم بیاورد →
  `INVENTORY_NOT_VALUED` و کاسه دست‌نخورده (تست‌شده). اگر Post شکست بخورد، آنچه از کاسه
  برداشته شده برگردانده می‌شود.
- **کسری**: `2300` حساب کنترلِ همان راننده/شرکت خدماتی است — همان حسابی که مرحله ۵ کرایه‌اش را
  بستانکار می‌کند. پس بدهکار کردنش دقیقاً یعنی «بابت آنچه نرسید به ما بدهکار است». legacy هم
  کرایه را دست نمی‌زند و این دو در ماندهٔ راننده به هم می‌رسند، نه در یک سطر.
- **سه‌جانبه**: نبودِ سطر صراف عمدی است — وقتی هر دو طرف هم‌زمان تسویه می‌شوند صراف چیزی نگه
  نمی‌دارد. `SarrafId` فقط منشأ است. legacy هم صریحاً همین را می‌گوید و هیچ `LedgerEntry` با
  `SarrafId` نمی‌سازد.
- **قاعدهٔ ضدِ نرخ‌سازی**: هر دو سطر پولیِ صراف و سه‌جانبه باید در بازتولیدِ
  `round(مبلغ × نرخ, 4)` سالم بمانند، وگرنه `INVALID_*_CONVERSION` و legacy-only — همان قاعده‌ای
  که ViaSarrafِ غیر-USD را بیرون نگه می‌دارد (تست‌شده).

### اتصال
- ضایعات: `LossEventWorkflowService.CreateAsync` (مسیر مشترک — `StorageTanksController.SettleFinal`
  از همین‌جا می‌آید) + `LossEventsController.Create` و `Cancel`.
- کسری: `InventoryTransportReceiptService.SyncShortageDebtAsync` — دقیقاً کنار همان سطر legacy.
- صراف: `SarrafSettlementService` هر سه مسیر — `CreatePostedAsync`، `EditPostedAsync`
  (اول Reversal نسخهٔ قبلی، بعد نسخهٔ بعدی) و `CancelAsync`.
- سه‌جانبه: `ThreeWaySettlementController` — `Confirm` و `Cancel`.

همه داخل تراکنش‌های موجود، همه به‌صورت پارامترِ اختیاریِ nullable، پس مسیر legacy بدون Adapter
مو‌به‌مو مثل قبل کار می‌کند.

### نسخه‌بندی صراف
`SourceEventId` = `SarrafSettlement:{id}:Created:{revision}`. ویرایش = برگرداندنِ **همهٔ**
نسخه‌های پست‌شده + پست نسخهٔ بعدی. سند قبلی Posted و دست‌نخورده می‌ماند (تست‌شده: اثر خالص =
مبلغ جدید؛ لغو = اثر خالص صفر).

### ⚠️ اختلاف مورد انتظار با legacy — قبل از روشن‌کردن Flag روی داده‌ی واقعی بررسی شود
سود/زیانِ سندِ صراف با سطر `SarrafSettlementExchangeDifference` لگسی **یکی نیست و نباید باشد**:
- سند: `SarrafCharged − مبلغ طرف‌حساب`
- legacy: `Requested − SupplierAccepted`

این دو شکاف متفاوتی را می‌سنجند. legacy سطر اختلاف را فقط زیر `RecognizeExchangeGainLoss`
می‌نویسد، ولی سند متوازن **همیشه** باید تکلیف شکاف را روشن کند. لاگ هر دو عدد را کنار هم
چاپ می‌کند تا واگرایی per-settlement دیده شود.

### ⚠️ محدودیت‌های شناخته‌شده (عمدی، مستند)
- **کسری Reversal ندارد** — چون legacy هم ندارد. `InventoryTransportLegsController.CancelGroupTransfer`
  مصارف کرایه و سطرهای دفترشان را پاک می‌کند ولی سطر `ShortageCharge` را **دست‌نخورده**
  می‌گذارد؛ هیچ مسیر دیگری هم آن را حذف نمی‌کند. نقطه‌ای برای اتصال Reversal وجود ندارد.
  اگر روزی آن سطر مسیر لغو پیدا کرد، **قبل از روشن‌کردن Flag** باید Reversal اضافه شود.
- **`ThreeWaySettlement` با `PayeeType=Supplier` پست نمی‌شود** (`UNSUPPORTED_PAYEE_TYPE`) —
  دامنهٔ تأییدشده فقط صراف بود. Mapping دقیقاً همان است و فقط یک شرط باید باز شود.
- **`LossEventsController.Edit` مقدار ضایعات را عوض می‌کند ولی `InventoryMovement` را نه.**
  چون سند از روی Movement ارزش‌گذاری می‌کند، ویرایش سند را بی‌اعتبار نمی‌کند. این ناسازگاری
  در خود legacy است و در دامنهٔ این مرحله نبود.

---

## مرحله ۸.۵ — بستن سه بدهیِ باز

هدف: سه مانعِ روشن‌کردن Flag که مرحله ۸ باقی گذاشت. **بدهی ۱ با کد بسته شد؛ بدهی‌های ۲ و ۳
تحلیل شدند و ریشه‌شان پیدا شد، ولی رفعشان تصمیم کاربر می‌خواهد (پایین).**

### بدهی ۱ — انتقال بین ترمینال‌ها بها را منتقل نمی‌کند ✅ بسته شد

Flag جدید: `Accounting:Pilots:InventoryTransfer` (پیش‌فرض `false`).

**چرا این مانع `Cogs` بود:** حوضچهٔ ارزش‌گذاری کلیدش `(شرکت، محصول، ترمینال)` است، چون کالای
هر ترمینال واقعاً بهای رسیدنِ متفاوتی دارد. legacy انتقال را با دو حرکت موجودی می‌نویسد
(خروج از مبدأ هنگام بارگیری، ورود به مقصد هنگام رسید) ولی **هیچ سطر لجری** نمی‌سازد. تا امروز
هیچ‌کدام از این دو حرکت به حوضچه دست نمی‌زد — یعنی انتقال، تُن را جابه‌جا می‌کرد و پول را نه:
حوضچهٔ مبدأ برای کالایی که دیگر ندارد پول نگه می‌داشت، و فروش در مقصد روی هرچه آن حوضچه
اتفاقاً داشت قیمت می‌خورد.

**Mapping — حساب `1310 کالای در راه` کلید کار است:**

```
بارگیری leg : Dr 1310 کالای در راه   Cr 1300 موجودی (ترمینال مبدأ، به میانگین موزون متحرک)
رسید مقصد   : Dr 1300 موجودی (مقصد) + Dr 5400 ضایعات (کسری)   Cr 1310 کالای در راه
```

`1310` از قبل در Chart بود و تا امروز هرگز استفاده نشده بود — دقیقاً برای همین ساخته شده.
کالای داخل موتر مال هیچ ترمینالی نیست، و تاریخ‌زدنِ بدهکارِ مقصد به تاریخ بارگیری دروغ است
دربارهٔ اینکه کالا کجا بوده. `1310` بهای کالا را دقیقاً به اندازه‌ای که در راه است نگه می‌دارد.
**legی که سرِ پایان دوره هنوز در راه است در `1310` مانده می‌گذارد — این جواب درست است، نه نشتی.**

**⚠️ بدهکارِ کسری به `5400` دو-بارشماری با مرحله ۸ نیست — مکمل آن است:**
مرحله ۸ برای کسری `Dr 2300 / Cr 5400` می‌زند (طلب از حمل‌کننده) و `5400` را با یک بستانکارِ
بی‌پشتوانه رها می‌کند. بهای بشکه‌هایی که نرسیدند باید از `1310` — جایی که آن بشکه‌ها واقعاً
هستند — بیرون بیاید و جایش `5400` است. **خالصِ `5400` آن‌وقت زیانِ واقعی است: بهای کالا منهای
آنچه از حمل‌کننده گرفته می‌شود.** آداپتر `InventoryLoss` مرحله ۸ عمداً `ReceiptShortage` را Skip
می‌کند و همین دلیلش است — آن بها را از حوضچهٔ ترمینالی برمی‌داشت که کالا هرگز به آن نرسید.

**سه نقطهٔ اتصال (همه پارامترِ اختیاریِ nullable، همه داخل تراکنش موجود):**
- `InventoryTransportLegLoadService.LoadAsync` → پست بارگیری
- `InventoryTransportReceiptService.ApplyAsync` → پست رسید
- `ShipmentsController.ReverseShipmentDerivedAsync` → برگشت بارگیری. **باید پیش از حذف legها
  صدا زده شود**، چون legها hard-delete می‌شوند و آداپتر مقدار و ترمینال مبدأ را از خود leg
  می‌خواند.

**بهای باقی‌ماندهٔ در راه از روی خودِ سندهای پست‌شده خوانده می‌شود، نه از یک جدولِ زمان‌بندیِ
بازمحاسبه‌شده.** یعنی رسیدی که Skip شده صرفاً چیزی مصرف نکرده و حساب همچنان جور است. آخرین
برداشت هرچه در راه مانده را می‌برد، پس هیچ خرده‌ای از گرد کردن در `1310` جا نمی‌ماند
(تست‌شده: ۳ تن به ارزش ۱٬۰۰۰ → دو رسید ۱ و ۲ تنی → ۳۳۳٫۳۳۳۳ + ۶۶۶٫۶۶۶۷ = ۱٬۰۰۰ دقیق).

**دامنه — فقط مسیر `ToInventory` با دریافتِ مثبت** (دقیقاً همان شرطی که legacy زیرش حرکت ورودی
می‌سازد). `DirectSale` و `DirectDispatch` و «فقط تسویه» Skip می‌شوند با دلیل صریح.

**محدودیت‌های شناخته‌شده (قبل از روشن‌کردن Flag باید تصمیم گرفته شوند):**
- **`DirectSale` از داخل موتر بهایش در `1310` گیر می‌کند.** کالا هرگز به میانگینِ ترمینالی
  نمی‌پیوندد، پس اینجا چیزی برای قیمت‌گذاری نیست. مرحله ۷ هم امروز همین فروش را
  `NO_OUTBOUND_MOVEMENT` می‌دهد و COGSش را Skip می‌کند — پس **امروز دو-بارشماری نیست، ولی
  `1310` مانده می‌گذارد.** بستنش کارِ COGS است نه این آداپتر.
- **`RECEIPT_EXCEEDS_IN_TRANSIT`:** موتر اجازه دارد بیشتر از باقیماندهٔ leg تحویل بدهد؛ اضافه‌اش
  بهایی در راه ندارد و ساختنِ بها misprice است، پس کل رسید legacy-only می‌ماند.
- **رسیدِ `ToInventory` با دریافت صفر و کسری مثبت** بهای کسری را در `1310` جا می‌گذارد.

**۱۹ تست، همه سبز در اولین اجرا.**

### بدهی ۲ — اختلاف مبلغ روبلی مرحله ۶ 🔍 ریشه پیدا شد، رفع نشد

**داده‌ی واقعی در دسترس نبود** (`DefaultConnection` در هر دو `appsettings` خالی است)، پس تحلیل
روی کد انجام شد. نتیجه از «بررسی روی داده» قطعی‌تر درآمد:

**۱. علتِ اصلی — سطر لجرِ legacy بعد از بازقیمت‌گذاری کهنه می‌ماند.**
`LoadingController.PostSupplierLoadingLedgerIfReadyAsync` گاردِ `alreadyPosted` دارد
(خطوط ۴۷۶–۴۸۱): سطر لجر روبلیِ تأمین‌کننده **یک‌بار نوشته می‌شود و هرگز به‌روز نمی‌شود**.
در مقابل، `ContractsController.PostRepricedPurchasesAsync` بعد از هر بازقیمت‌گذاری همهٔ
بارگیری‌ها را دوباره به آداپتر می‌دهد و آداپتر با Revision + Reversal سند را به قیمت جدید
اصلاح می‌کند. `SyncPurchaseLoadingPricesAsync` هم با `forceRelock: true` خودِ
`AmountUsdAtRubLock` را تازه می‌کند.

**نتیجه: بعد از بازقیمت‌گذاری، سطر لجر legacy با `AmountUsdAtRubLock`ِ خودِ legacy هم فرق
می‌کند.** سند جدید با `AmountUsdAtRubLock` موافق است و با سطر لجر مخالف. **یعنی آداپتر درست
است و سطر legacy کهنه است.** این نقصِ از پیش موجودِ legacy است، نه باگ آداپتر.

**۲. علتِ فرعی — حالتِ گرد کردن.** `LoadingRubSettlement.CalculateLoadingValueUsd` (خط ۲۰)
`Math.Round(qty × price, 4)` است **بدون `MidpointRounding`** → یعنی `ToEven` (بانکداری).
آداپتر `AwayFromZero` دارد. اختلاف فقط روی تساویِ دقیقِ رقم پنجم و حداکثر `0.0001` است. جالب
اینکه خواهرِ یک خط پایین‌تر (`CalculateRubAmount`، خط ۲۴) صریحاً `AwayFromZero` دارد — یعنی
این احتمالاً یک از قلم‌افتادگی در legacy است.

**۳. آنچه اختلاف نیست:** وقتی `LoadingPriceUsd` نباشد آداپتر به قیمت قرارداد fallback می‌کند
ولی legacy اصلاً قفل نمی‌شود و **هیچ سطری نمی‌سازد** — پس چیزی برای مقایسه نیست. این تفاوتِ
پوشش است و طبق تصمیم ۱ عمدی است.

**⛔ چرا رفع نشد:** هر دو علت در `Ledger`/`FX` legacy هستند و CLAUDE.md صریحاً می‌گوید بدون
درخواست مشخص دست نخورند. **تصمیم کاربر لازم است** (سؤالِ دقیق در بخش «از کجا ادامه بدهم؟»).

### بدهی ۳ — واگرایی سود/زیان صراف ✅ گزارش مقایسه کامل شد

لاگ `Sarraf settlement accounting pilot comparison` قبلاً عددهای legacy را چاپ می‌کرد ولی
**خودِ gap سند را نه** — یعنی دقیقاً همان عددی که واگرا می‌شود قابل مقایسه نبود. حالا اضافه شد:
- `JournalGapUsd` = `SarrafChargedAmountUsd − LegacyCounterpartyAmountUsd` (مثبت: صراف بیشتر از
  آنچه طرف‌حساب پذیرفت گرفته).
- `JournalGapAccountKind` = `ExchangeLoss` / `ExchangeGain` / `None` — چون اینکه gap کدام طرف
  P&L می‌نشیند به جهتِ تسویه هم بستگی دارد، نه فقط به علامتش.

حالا `LegacyDifferenceUsd` و `JournalGapUsd` کنار هم در یک خط لاگ‌اند و مقایسه روی داده‌ی
واقعی مستقیماً ممکن است. منطقِ «کدام طرف زیان است» در `JournalGapIsLoss` یکی شد تا سطرِ سند و
لاگ نتوانند از هم جدا بیفتند.

**واگرایی همچنان عمدی و مورد انتظار است:** سند `Charged − طرف‌حساب` را می‌سنجد، legacy
`Requested − SupplierAccepted` را. **این عدد هنوز روی داده‌ی واقعی تأیید نشده** — Flag تا آن
موقع خاموش می‌ماند.

---

## اصلاح قاعدهٔ #9 — بازقیمت‌گذاریِ بی‌صدای بارگیریِ قطعی‌شده ✅

**بعد از مرحله ۹، پیش از مرحله ۱۰.** دو شکست Baseline بررسی عمیق شد و معلوم شد **باگ واقعی
بودند، نه تست کهنه**. تست‌ها درست بودند و کد قاعدهٔ #9 را نقض می‌کرد.

**ریشه:** `ContractsController.EditPricing` (و `Edit`) با هر تغییرِ نرخ نهاییِ قرارداد
`repriceFinalized: contractPriceChanged` می‌فرستادند. آن Flag دو کار می‌کرد: کوئری را به
بارگیری‌های از پیش قیمت‌دار هم باز می‌کرد، و `forceRelock: true` به
`LoadingRubSettlement.TryLockFinalizedRub` می‌داد. نتیجه: مسیر عمومی، بارگیریِ قطعی‌شده را
بی‌صدا Reprice و Relock می‌کرد و `AmountUsdAtRubLock` و سطر Legacy Ledger و سند دفتر کل جدید
را بدون Reversal صریح کهنه/عوض می‌کرد.

**اثبات اینکه کد اشتباه بود، نه تست:** خودِ قاعدهٔ #9 در سه جای Source مستند بود و هر سه با
خط فراخوان تناقض داشتند —
`ContractsController.SyncPurchaseLoadingPricesAsync` (کامنت «فقط در انتظار قیمت … مگر
`repriceFinalized=true`»)، `LoadingRubSettlement.TryLockFinalizedRub` (کامنت «فقط مسیر صریحِ
اصلاح قیمت با `forceRelock=true`»)، و وجودِ خودِ `RepricePurchaseLoadings` به‌عنوان مسیر صریحِ
POST + ضدجعل + لاگ که تستش (`RepricePurchaseLoadings_Overwrites_Finalized_Price_And_Relocks_Rub`)
از قبل سبز بود. کد و تست هر دو در همان Commit اولیهٔ Squash‌شده (`d6a96ac`) آمده بودند، پس
History تفکیک نمی‌کرد؛ قاعدهٔ مستند و مسیر محافظت‌شده تعیین‌کننده بود.

**اصلاح (حداقلی، فقط همین مسیر):**

- `EditPricing` و `Edit` → `SyncPurchaseLoadingPricesAsync(contract)` بدون `repriceFinalized`.
  بارگیریِ قطعی‌شده فقط از `RepricePurchaseLoadings` عوض می‌شود (Reversal + Revision).
- `CountFinalizedPurchaseLoadingsAsync` — قبل از Sync شمرده می‌شود (بعدش، بارگیریِ در انتظار
  قیمتِ تازه‌قطعی‌شده هم اشتباهاً شمرده می‌شد).
- پیام هشدار در همان `TempData["ok"]` تجمیع شد تا کاربر بداند نرخ عوض شد ولی N بارگیریِ
  قطعی‌شده دست‌نخورده ماند و باید از «اصلاح قیمت» استفاده کند. **کلید TempData جدید ساخته نشد و
  هیچ View‌ای دست نخورد** (`_FlashAlerts` فقط `ok`/`err` را می‌شناسد).
- `SkippedFinalizedLoadingCount` به Audit diff هر دو مسیر اضافه شد.

**تست‌های افزوده (۴):** `Edit_Post_Does_Not_Reprice_Or_Relock_Finalized_Loading`،
`EditPricing_Post_Keeps_Legacy_Ledger_Row_Of_Finalized_Loading_Untouched`،
`RepricePurchaseLoadings_Syncs_Legacy_Ledger_Row_Of_Finalized_Loading`، و Helper مشترک
`SeedRubFinalizedLoadingWithLegacyLedger`. Reversal + Revision سند در سطح Adapter از قبل با
`Repricing_Reverses_The_Old_Revision_And_Posts_A_New_One` و
`Reposting_An_Unchanged_Purchase_Does_Nothing` پوشش داشت، پس تکرار نشد.

**نتیجه روی Worktree تمیز (فقط همین دو فایل روی HEAD):** Build سبز (۰ خطا، ۴ هشدارِ از پیش
موجود). Full Suite: **۱۱۳۱ سبز / ۱۶ شکست** — دقیقاً Baseline منهای همین ۲. **۰ شکست جدید.**
هیچ Flag روشن نشد و هیچ Migration اجرا نشد.

---

## مرحله ۱۰ — UI ساده‌ی سال مالی ✅

صفحه‌ی فقط‌خواندنیِ سال مالی + تنها یک مسیر نوشتنی: ساختِ سال بعد.

### فایل‌های جدید
- `Models/Accounting/FiscalYearViewModels.cs`
- `Services/Accounting/FiscalYearOverviewService.cs` — **فقط می‌خواند**. همه‌ی جمع‌ها و تصمیم‌های
  «چه چیزی مجاز است» اینجاست تا View فقط چاپ کند (تست‌شده: `Building_The_Pages_Writes_Nothing`).
- `Services/Accounting/FiscalYearProvisioningService.cs` — تنها مسیر نوشتن.
- `Controllers/FiscalYearsController.cs`
- `Views/FiscalYears/Index.cshtml`, `Views/FiscalYears/Details.cshtml`
- `Helpers/FiscalStatusDisplay.cs` — فقط برچسبِ نمایشی؛ هیچ تصمیم مالی‌ای ندارد.
- `tests/PTGOilSystem.Web.Tests/FiscalYearUiTests.cs` (۲۸ تست)

### فایل‌های تغییرکرده
- `Models/Entities/Accounting/AccountingEnums.cs` — `FiscalYearStatus.Reopened` و
  `FiscalPeriodStatus.SoftLocked`/`HardLocked`.
- `Program.cs` — ثبت DI دو سرویس جدید.

### ⚠️ چرا این افزودنِ enum هیچ Migration نمی‌خواهد
هر دو ستون `Status` در `FiscalYears` و `FiscalPeriods` از قبل `integer` هستند و **هیچ CHECK
constraintی روی مقدارشان نیست** (اثبات: `20260715162639_AddAccountingCoreAndFiscalCalendar`؛
CHECKهای موجود فقط `DateRange` و `PeriodNumber` هستند). پس مقدار جدیدِ enum فقط یک عدد جدید در
ستونی است که از قبل عدد می‌پذیرد. هیچ داده‌ی موجودی معنایش عوض نمی‌شود.

### دو تصمیمِ قابل‌استنتاج (حدس زده نشد، مستند شد)
1. **منبعِ آینه‌ی سالِ بعد = سالِ جاری، نه تازه‌ترین سال.** سالِ جدید آینه‌ی دقیقِ سالِ جاری است:
   همان تعداد دوره، هر تاریخ دقیقاً یک سال جلوتر (`AddYears(1)`). قرارداد تقویمِ شرکت اختراع
   نمی‌شود — اگر شرکت هیچ سال مالی نداشته باشد، دکمه اصلاً مجاز نیست و دلیلش گفته می‌شود.
   منبع عمداً سالِ جاری است تا سالِ تازه‌ساخته خودش منبعِ بعدی نشود و زنجیرهٔ بی‌پایانِ سال‌های
   پیش‌نویس ساخته نشود (تست‌شده).
2. **Trial Close و Final Close.** مرحله‌های ۱۳ و ۱۴ شروع نشده‌اند و `FiscalYearCloseRun` هیچ
   نشانه‌ای برای تفکیک «آزمایشی» از «نهایی» ندارد؛ ساختنِ آن نشانه حدس می‌بود. پس صفحه فقط آنچه
   اثبات‌پذیر است نشان می‌دهد: اجراهای ثبت‌شده‌ی `FiscalYearCloseRuns` و بسته‌شدنِ خودِ سال
   (`FiscalYear.ClosedAt`)، و صریحاً می‌گوید Trial Close کارِ مرحله ۱۳ است.

سالِ جدید با وضعیت `Draft` ساخته می‌شود و `IsCurrent` **جابه‌جا نمی‌شود** — بازکردنِ سال و
انتقالِ پرچمِ جاری تصمیم‌های جداگانه‌اند و این دکمه انجامشان نمی‌دهد (تست‌شده).

### دسترسی و ایمنی
- خواندن: `AuthPolicies.ManageData`. نوشتن (`CreateNextYear`): `AuthPolicies.AdminOnly` +
  `[HttpPost]` + `[ValidateAntiForgeryToken]` (تست‌شده: هیچ مسیر GETِ خطرناکی وجود ندارد).
- ویوها فقط از `_AkPageHeader` و `ak-table` و `ak-form`ِ موجود استفاده می‌کنند. **هیچ CSS یا
  کامپوننت موازی ساخته نشد** و هیچ فایل UI نامرتبطی لمس نشد (هر دو تست‌شده).
- Audit: ساختِ سال بعد `AuditAction.Insert` با diff کامل می‌نویسد، **داخل همان تراکنش**.

---

## مرحله ۱۱ — قفل دوره و AccountingDate ✅

### ✅ AccountingDate از قبل وجود داشت — Migration ساخته نشد
`JournalEntry` از همان مرحله ۲ سه تاریخ جدا دارد: `AccountingDate`، `DocumentDate` و
`OperationDate`. پس چیزی اضافه نشد و هیچ Backfill حدسی لازم نبود؛ فقط **enforce** شد.
`AccountingDate` تنها تاریخی است که گارد می‌سنجد: `DocumentDate`/`OperationDate` می‌گویند سند و
رویدادِ تجاری کِی بودند، `AccountingDate` می‌گوید سند در کدام دفتر می‌نشیند (تست‌شده:
`The_Guard_Reads_Only_The_Accounting_Date_Not_The_Document_Date`).

### ✅ چرا «همهٔ Adapterها از Guard رد می‌شوند» اثبات‌پذیر است، نه ادعا
معماری از قبل یک گلوگاه داشت و مرحله ۱۱ فقط آن را دقیق کرد:
- **تنها `AccountingPostingService` سند می‌سازد** — تست `Only_The_Posting_Service_Creates_Journal_Entries`
  کلِ `src/` را می‌گردد و اگر روزی جای دیگری `new JournalEntry` بنویسد می‌شکند.
- **تنها `PeriodGuard` تقویم را resolve می‌کند** — تست `Only_The_Period_Guard_Resolves_The_Fiscal_Calendar`.
- **`PostAsync` و `ReverseAsync` هر دو از `PostInternalAsync` رد می‌شوند** و آن پیش از ساختنِ هر
  چیزی گارد را صدا می‌زند — پس برگشت هم دقیقاً مثل ثبت گارد می‌خورد.

یعنی آنچه روی گارد تست می‌شود دربارهٔ **همهٔ** آداپترهای مراحل ۱ تا ۸.۵ صادق است و هیچ مسیر
دورزننده‌ای از Controller/Service/Adapter باقی نمانده.

### Reason Codeها
```
ACCOUNTING_DATE_OUT_OF_RANGE  تاریخ آینده، یا خارج از هر سال مالیِ این شرکت
FISCAL_YEAR_CLOSED            سالِ این تاریخ بسته است
FISCAL_YEAR_NOT_OPEN          سال Draft یا در حال بستن است
PERIOD_NOT_FOUND              سال هست، دوره‌ای برای این تاریخ نیست
COMPANY_PERIOD_MISMATCH       دوره متعلق به شرکت دیگری است
PERIOD_SOFT_LOCKED            ثبت عادی ممنوع
PERIOD_HARD_LOCKED            ثبت/برگشت/Repost/Backdate بدون استثنا ممنوع
INVALID_COMPANY               (از قبل موجود) شرکت نیست یا غیرفعال است
```

`COMPANY_PERIOD_MISMATCH` واقعاً قابلِ رسیدن است چون دوره عمداً **بدون فیلترِ شرکت** خوانده
می‌شود: اگر دوره‌ای از شرکت دیگری داخل سالِ این شرکت باشد باید دیده و رد شود، نه اینکه با فیلتر
ناپدید شود و به‌جایش «دوره پیدا نشد» بگیرد.

### قواعد اجراشده
- **دورهٔ باز** → ثبت مجاز. **سالِ `Reopened`** هم مثل `Open` ثبت می‌پذیرد — معنیِ بازگشایی همین
  است (مرحله ۱۵ تعیین می‌کند *چه کسی* می‌تواند سال را به این وضعیت ببرد).
- **`SoftLocked`** → ثبت عادی رد. استثنا فقط با `AppPermissions.PostToSoftLockedPeriod` +
  دلیلِ اجباری + Audit. **نقشِ Admin عمداً کافی نیست** — «فقط با Permission مشخص» یعنی همین.
  بدون سرویسِ Audit اصلاً انجام نمی‌شود (`PERIOD_EXCEPTION_AUDIT_UNAVAILABLE`): استثنای بی‌ردّ با
  «قفل نبودن» فرقی ندارد. **هیچ آداپتری این مسیر را صدا نمی‌زند** — ثبت عادی همیشه رد می‌شود.
- **`HardLocked`** → هیچ استثنایی ندارد، حتی با Permission (تست‌شده).
- **دورهٔ `Closed`ِ قدیمی** از نظر ثبت دقیقاً `HardLocked` است — سخت‌گیرانه‌ترین معنیِ موجود، و
  همان رفتاری که قبلاً هم داشت. فقط Reason Codeاش از `CLOSED_ACCOUNTING_DATE` به
  `PERIOD_HARD_LOCKED` دقیق‌تر شد؛ سه تست موجود به همین کد مهاجرت کردند.
- **Backdating**: قدمتِ تاریخ به‌خودی‌خود ممنوع نیست — آنچه Backdating را می‌بندد قفلِ دوره است.
  ولی **تاریخ آینده هرگز**: سندی که هنوز اتفاق نیفتاده نباید دفتر را تکان بدهد. مرزِ «آینده» روز
  است نه لحظه (امروز قبول، فردا رد).
- **Reversal**: سند اصلی دست‌نخورده می‌ماند (رفتار از قبل موجودِ `ReverseAsync`) و
  `AccountingDate`ِ برگشت خودش از گارد رد می‌شود — یعنی برگشت نمی‌تواند در دورهٔ بستهٔ قبلی بنشیند.

### قفلِ دوره — `FiscalPeriodLockService`
تنها مسیرِ نوشتنِ `FiscalPeriod.Status`. دو قاعده‌ی عمدی:
1. **قفلِ سخت برگشت‌ناپذیر است.** اگر بشود بازش کرد، قفل سخت نیست و همهٔ تضمین‌های این مرحله به یک
   کلیک تبدیل می‌شوند. بازگشایی کارِ مرحله ۱۵ است.
2. **دورهٔ سالِ بسته اصلاً تغییر نمی‌کند.**

هر تغییر وضعیت Audit می‌شود داخل همان تراکنش. تغییر به همان وضعیت Idempotent است و چیزی نمی‌نویسد.
دکمه‌های صفحهٔ جزئیات از همان قاعده ساخته می‌شوند (`BuildLockActions`) تا صفحه هرگز کاری را
پیشنهاد نکند که سرویس ردش می‌کند. مسیر: `POST` + ضدجعل + `AdminOnly`.

### فایل‌های جدید
- `Services/Accounting/FiscalPeriodLockService.cs`
- `tests/PTGOilSystem.Web.Tests/PeriodGuardTests.cs` (۳۰ تست)

### فایل‌های تغییرکرده
- `Services/Accounting/FiscalCalendarService.cs` — `ResolveAsync` که به‌جای «پیدا شد/نشد» **دلیلِ
  دقیق** برمی‌گرداند. `FindOpenPeriodAsync` سرِ جایش ماند.
- `Services/Accounting/PeriodGuard.cs` — Reason Codeهای دقیق + قاعدهٔ تاریخ آینده + مسیر استثنا.
- `Security/RoleAccessRules.cs` — `AppPermissions.PostToSoftLockedPeriod`.
- `Controllers/FiscalYearsController.cs` + `Views/FiscalYears/Details.cshtml` +
  `Models/Accounting/FiscalYearViewModels.cs` + `Services/Accounting/FiscalYearOverviewService.cs`
  — عملیاتِ قفل روی صفحهٔ مرحله ۱۰.
- `Program.cs` — ثبت DI.

### ⚠️ پنج ثابتِ تاریخ در تست‌ها به گذشته منتقل شد
`SalesAccountingAdapterTests`، `Stage8AccountingAdapterTests`،
`InventoryTransferAccountingAdapterTests` (هر سه `2026-07-20`) و `AccountingReversalTests` و
`PurchaseAccountingAdapterTests` (`2026-07-15` + رسیدِ `AddDays(2)`) تاریخِ **آینده** پست می‌کردند
و قاعدهٔ جدید ردشان کرد. هر پنج به `2026-07-05` رفتند — داخل همان دورهٔ مالیِ ژوئیهٔ ۲۰۲۶ که از
قبل seed می‌شد، ولی قطعاً گذشته. این تاریخ‌ها دلخواه بودند و هیچ معنیِ تجاری نداشتند؛ ضمناً
تست‌ها را از وابستگی به تقویمِ ماشین هم آزاد می‌کند.

---

## نقطه‌ی دقیق توقف

آخرین کار انجام‌شده: **مرحله ۸.۵ — بستن بدهی‌ها**. انتقال بین ترمینال‌ها با میانگین موزون
متحرک و Reversal ساخته شد (بدهی ۱ بسته). ریشهٔ بدهی ۲ روی کد پیدا شد. گزارش مقایسهٔ بدهی ۳
کامل شد. Build سبز (۰ خطا). **۱۹ تست انتقال سبز در اولین اجرا.**

**وضعیت تأیید تست:**
- **✅ Full Suite کامل گرفته شد** (اولین بار بعد از مرحله ۶): **۱۱۰۰ پاس / ۱۹ شکست**
  = ۱۸ باقیماندهٔ baseline + ۱ شکستِ ویوی `InventoryTransportLeg_Details_...` که از کار UI
  ناتمامِ Working Tree می‌آید (اثبات در «خلاصه‌ی وضعیت کلی» بالا). **۰ شکست جدید از حسابداری.**
- مرحله ۸: ۳۲ تست سبز. مرحله ۷: ۱۷ تست سبز. انتقال: ۱۹ تست سبز.

چهار Migration اجرانشده روی دیتابیس عملیاتی باقی است:
`20260715181837_AddCompanyOwnershipToPaymentsAndCashAccounts` (مرحله ۳)،
`20260716092121_AddCustomerAdvanceMarkerToPayments` (مرحله ۴)،
`20260716095651_AddExpenseTypePayableAccountKind` (مرحله ۵) و
`20260716114201_AddInventoryAverageCost` (مرحله ۷).
مرحله‌های ۸ و ۸.۵ هیچ Migration جدیدی نساختند.

### جایی که باید ادامه داد → مرحله ۹ (Cutover و AccountingReadiness)
مراحل ۴ تا ۸.۵ همهٔ رویدادهای عملیاتی را پوشش داده‌اند. وضعیت سه بدهی:
1. ✅ **انتقال بین ترمینال‌ها** → بسته شد. Flag `InventoryTransfer` ساخته شد؛ `Cogs` حالا
   پیش‌نیاز فنی‌اش را دارد (هر دو با هم باید روشن شوند).
2. 🔍 **اختلاف مبلغ روبلی مرحله ۶** → ریشه پیدا شد (سطر لجر legacy بعد از بازقیمت‌گذاری کهنه
   می‌ماند + گرد کردن `ToEven`). **رفعش تصمیم کاربر می‌خواهد** چون در `Ledger`/`FX` legacy است.
3. 🔍 **واگرایی سود/زیان صراف** → گزارش مقایسه کامل شد (`JournalGapUsd` +
   `JournalGapAccountKind` حالا در لاگ‌اند). **عدد هنوز روی داده‌ی واقعی تأیید نشده.**

هیچ Backfill حدسی انجام نشده. هیچ داده‌ی عملیاتی حذف نشده. تغییرات UI نامرتبط در Working Tree
(stat-cards `.webp`/`.css`، `docs/ui-references/*.png`) **دست‌نخورده** باقی مانده.

### TODO باز (کوچک، برای مرحله‌ی بعد)
`PaymentsController.Create` هنگام ساخت پرداخت جدید `CompanyId` را **نمی‌نویسد**؛ بنابراین
رکوردهای جدید null می‌مانند و Adapter شرکت را در لحظه از روابط قابل‌اثبات حساب می‌کند
(`PaymentCompanyResolver`). این عمدی است تا نوشتنِ legacy تغییر نکند، ولی یعنی ستون Stage 3
برای رکوردهای جدید پر نمی‌شود. اگر تصمیم گرفته شد که پر شود، همان Resolver قابل استفاده است.

---

## از کجا ادامه بدهم؟

### ✅ Full Suite گرفته شد — ۰ شکست جدید از حسابداری
۱۱۰۰ پاس / ۱۹ شکست = ۱۸ baseline + ۱ از کار UI ناتمامِ Working Tree. جزئیات و اثبات در
«خلاصه‌ی وضعیت کلی» بالا.

### تصمیم ۱ — اختلاف روبلی مرحله ۶ — ✅ بسته شد (گزینهٔ ب + ج، با اجازهٔ صریح کاربر)

ریشه: سطر لجر روبلیِ تأمین‌کننده یک‌بار نوشته می‌شد و بعد از بازقیمت‌گذاری **هرگز به‌روز
نمی‌شد** (گاردِ `alreadyPosted`)، در حالی که سند جدید با Reversal + Revision اصلاح می‌شود.
یعنی **آداپتر درست بود و سطر legacy کهنه می‌ماند.** اختلاف فرعی هم گردکردن بود:
`CalculateLoadingValueUsd` با `ToEven` و آداپتر با `AwayFromZero` (حداکثر ۰٫۰۰۰۱).

اصلاح انجام‌شده (فقط همین رفتار، بدون Refactor عمومی):
- `Helpers/SupplierLoadingLedger.cs` (جدید) — تنها سرچشمهٔ ساخت/هماهنگیِ سطر legacy بارگیری.
  `ApplySnapshot` مبلغ/نرخ سطر موجود را با snapshot فعلی بارگیری هماهنگ می‌کند و اگر چیزی عوض
  نشده باشد `false` برمی‌گرداند (بدون نوشتن بی‌مورد).
- `LoadingController.PostSupplierLoadingLedgerIfReadyAsync` — به‌جای «اگر هست هیچ نکن»، همان
  سطر را به‌روز می‌کند. کلید یکتا همچنان `(SourceType="Loading", SourceId=loading.Id)` است؛
  **سطر دوم ساخته نمی‌شود.**
- `ContractsController.SyncPurchaseLoadingPricesAsync` — بارگیری‌هایی که واقعاً بازقفل شدند
  جمع می‌شوند و سطر legacy همان‌ها هماهنگ می‌شود. بدون `SaveChanges` مستقل و با `_audit.LogAsync`
  (نه `LogAndSaveAsync`) تا **داخل همان تراکنشِ فراخوان** ثبت شود و Rollback کامل بماند.
- `LoadingRubSettlement.RoundAmountUsd` — یگانه قانون گردکردن
  (`decimal.Round(value, 4, MidpointRounding.AwayFromZero)`). هم مسیر legacy و هم
  `PurchaseAccountingAdapter` از همین عبور می‌کنند.

خارج از دامنه (دست‌نخورده): دفتر کل جدید همچنان Reversal + Revision است و رکورد Posted هرگز
ویرایش نمی‌شود؛ مسیر بارگیری USD و بارگیری Pending بدون تغییر؛ هیچ Refactor در Ledger/FX/Loading.

تست: `tests/PTGOilSystem.Web.Tests/SupplierLoadingLegacyLedgerTests.cs` — ۹ تست، همه سبز
(یک سطر در بارگیری اول، به‌روزرسانیِ همان سطر در Repricing و forceRelock، نبودِ سطر دوم،
برابریِ مبلغ legacy با `AmountUsdAtRubLock` جدید، بی‌اثر بودنِ اجرای دوباره، USD و Pending بدون
Regression، و یکی بودن قانون گردکردن).

**تصمیم ۲ — تأیید عدد صراف روی داده‌ی واقعی (مانع Flag `SarrafSettlement`).**
لاگ حالا `LegacyDifferenceUsd` و `JournalGapUsd` را کنار هم چاپ می‌کند. این کار **داده‌ی
عملیاتی می‌خواهد** — در repo هیچ رشتهٔ اتصالی نیست (`DefaultConnection` در هر دو `appsettings`
خالی است). باید روی محیط واقعی با Flag روشن + بررسی لاگ انجام شود.

### مرحله ۹ — Cutover و AccountingReadiness — ✅ زیرساخت ساخته شد

`AccountingReadinessService` (فقط‌خواندنی) آمادگی را **برای هر شرکت جداگانه** گزارش می‌کند، چون
تنظیمات، حساب‌ها، سال مالی و مالکیت رکوردها همه per-company هستند.

چهار وضعیت: `Ready`، `Warning`، `OperationalDataValidationRequired`، `Blocked`. تفاوت دو تای آخر
عمدی است: `Blocked` یعنی چیزی در همین repo اثبات‌پذیر خراب است؛
`OperationalDataValidationRequired` یعنی کد آماده است ولی قضاوت به داده‌ی عملیاتی نیاز دارد که در
repo نیست — **و این هرگز نباید با حدس به Ready تبدیل شود.**

هر Blocker این‌ها را دارد: `Code`، `Title`، `Description`، `Severity`، `CompanyId`،
`RecordCount`، `SampleRecords` (حداکثر ۱۰)، `RequiredAction`، `FeatureFlag`.

بررسی‌های اثبات‌پذیر از دیتابیس: `ACCOUNTING_SETTINGS_MISSING`،
`UNSUPPORTED_FUNCTIONAL_CURRENCY`، `REQUIRED_ACCOUNT_MISSING/INACTIVE/WRONG_COMPANY` (هر ۲۰
حساب)، `NO_OPEN_FISCAL_YEAR`، `NO_OPEN_FISCAL_PERIOD`، `MULTIPLE_CURRENT_FISCAL_YEARS`،
`CASH_ACCOUNT_WITHOUT_COMPANY`، `PAYMENT_WITHOUT_COMPANY`،
`EXPENSE_TYPE_PAYABLE_KIND_MISSING`، `CUSTOMER_ADVANCE_MARKER_UNKNOWN`،
`INVENTORY_POOL_EMPTY`، `INVENTORY_QUANTITY_WITHOUT_VALUE`، `INVENTORY_POOL_NEGATIVE`،
`SALES_COST_NOT_EVALUATED` / `SALE_WITHOUT_COGS_JOURNAL`، `TRANSFER_COST_NOT_MOVED` /
`TRANSFER_LEG_WITHOUT_COST_JOURNAL`، `UNBALANCED_JOURNAL`، `POSTED_JOURNAL_WITHOUT_LINES`،
`POSTED_JOURNAL_WITHOUT_POSTED_AT`، `DRAFT_JOURNAL_WITH_POSTED_AT`،
`DUPLICATE_SOURCE_EVENT_ID`، `MIGRATIONS_PENDING`.

**دو چیزی که عمداً از دیتابیس اثبات نمی‌شوند** و به‌جای ادعای دروغ،
`OperationalDataValidationRequired` می‌گیرند:
- `FULL_SUITE_EXTERNAL_EVIDENCE` — نتیجهٔ تست حالتِ Runtime نیست. معیار عبور: Build بدون خطا و
  همان ۱۸ شکست baseline روی Worktree تمیز.
- `SKIP_COUNTS_REQUIRE_LOG_HARVEST` — **هیچ Adapter دلیل Skip را ذخیره نمی‌کند**؛ فقط لاگ
  می‌کند. پس شمارش دقیق به تفکیک Reason Code فقط از لاگِ یک اجرای واقعی به‌دست می‌آید. آنچه
  اثبات‌پذیر است در فهرست `Adapters` می‌آید: وضعیت Flag، تعداد رکورد نامزد، تعداد سند پست‌شده، و
  دلیلِ Skipِ قطعی وقتی Flag خاموش است (`ACCOUNTING_DISABLED` یا `PILOT_DISABLED` — این حدس
  نیست، خودِ گاردِ Adapter است).

مسیر اجرا: `GET /accounting/readiness` (فقط `AdminOnly`، فقط GET، بدون هیچ مسیر نوشتنی).
برای اجرای روی **Backup** دیتابیس عملیاتی ساخته شده؛ رشتهٔ اتصال را اجراکننده از بیرون می‌دهد و
**در repo هیچ رشتهٔ اتصال عملیاتی ساخته یا فرض نشده است.**

تست: `AccountingReadinessServiceTests` — ۱۶ تست، همه سبز؛ از جمله `Report_Writes_Nothing` که
اثبات می‌کند گزارش هیچ چیزی نمی‌نویسد.

باقی‌مانده برای Cutover:
1. Flagهای `Purchase` و `SarrafSettlement` هنوز اعتبارسنجیِ داده‌ی عملیاتی می‌خواهند.
   `Cogs` و `InventoryTransfer` باید **با هم** روشن شوند.
2. Migrationهای اجرانشده با تصمیم صریح کاربر و روی Backup تأییدشده. **هرگز خودکار.**
3. اگر تصمیم حسابداری مبهم شد، **توقف و سؤال دقیق**.

### مرحله ۱۳ — تسعیر پایان‌دوره (تصمیمِ ثبت‌شدهٔ مرحله ۸)
هیچ مسیر legacy وجود ندارد: نه Entity، نه سرویس، نه Migration، و `4200`/`5300` هرگز پست
نشده‌اند. زیرساختِ موجود: `AccountingSettings.ExchangeGain/LossAccountId`، `FiscalCalendarService`،
`PeriodGuard`، و فیلدهای ارزیِ هر سطر (`LedgerEntry.Currency`, `.SourceAmount`,
`.AppliedFxRateToUsd`, ...) به‌عنوان مبنای تسعیر. **رفتار کاملاً جدید است، نه mirror** — پس
Mapping و مبنای نرخ باید پرسیده شود.

### الگوی موجود برای کپی‌برداری (مرجع دست‌اول)
`PaymentAccountingAdapter` (مراحل ۴ و ۵) کامل‌ترین قالب است: تشخیص نوع رویداد از ماهیت واقعی،
Flag مستقل هر زیرماژول، تعیین شرکتِ قابل‌اثبات، Skip بدون اثر روی legacy، Duplicate، بازتولید
مبلغ Functional از جفت‌ارز، و تست PostgreSQL واقعی از طریق `AccountingPostgreSqlFixture`.
`ExpenseAccountingAdapter` نمونه‌ی خوبِ «فیلد صریح به‌جای استنتاج از داده‌ی آزاد» است.

---

## مرحله ۱۲ — چک‌لیستِ بستنِ سال ✅

سرویس و صفحهٔ **کاملاً فقط‌خواندنی** برای هر (Company, FiscalYear) مستقل. اجرای چک‌لیست هیچ
Entity/Journal/Flag/Migration/Posting را تغییر نمی‌دهد (تست‌شده: `Report_Writes_Nothing`).

### فایل‌های جدید
- `Services/Accounting/ClosingChecklistModels.cs` — `ClosingCheckStatus`
  (Passed/Warning/Blocked/NotApplicable)، `ClosingCheckResult`، `ClosingRevenueExpenseSummary`،
  `ClosingChecklistReport`.
- `Services/Accounting/ClosingChecklistService.cs` — سرویس فقط‌خواندنی.
- `Controllers/ClosingChecklistController.cs` — فقط GET: `Index`, `Json`, `Csv` (بدون Package جدید).
- `Models/Accounting/ClosingChecklistPageViewModel.cs`
- `Views/ClosingChecklist/Index.cshtml` — با کامپوننت‌های مشترک AK.
- `tests/PTGOilSystem.Web.Tests/ClosingChecklistServiceTests.cs` (۱۷ تست، همه سبز).

### فایل‌های تغییرکرده
- `Program.cs` — ثبت DI: `IClosingChecklistService`.
- `docs/accounting/DUAL_ENTRY_PROGRESS.md`.

### کنترل‌های پیاده‌شده (خروجی هرکدام: Code/Status/Title/Description/CompanyId/FiscalYearId/
RecordCount/SampleRecords≤۱۰/RequiredAction/FeatureFlag/Link)
تنظیمات معتبر، ۲۰ حساب اجباری موجود/فعال/هم‌شرکت، سال معتبر و بدون هم‌پوشانی، پوشش کامل دوره‌ها
بدون فاصله/هم‌پوشانی، سند نامتوازن، وضعیت‌های ناسازگارِ سند (Draft/Posted بدون PostedAt/Line،
Draft با PostedAt)، SourceEventId تکراری، فروش بدون COGS/PendingCost، موجودی منفی، Pool ناسازگار،
انتقال ترمینالی ناقص، ماندهٔ ۱۳۱۰ (Warning عملیاتی)، ExpenseType بدون PayableKind، CashAccount/
Payment بدون Company، IsCustomerAdvance نامشخص، وضعیت همهٔ Feature Flagها، Migrationهای اجرانشده،
AccountingReadiness Blocked، دوره‌های باز، برابری Debit/Credit کل سال، مانده درآمد/هزینه برای
Final Close، تسعیر پایان دوره Pending (Warning تا مرحله ۱۳)، Full Suite و شمارش Skip فقط شواهد
بیرونی (NotApplicable — Runtime جعل نمی‌شود).

### دسترسی و ایمنی
`[Authorize(ManageData)]`، فقط GET، بدون هیچ مسیر نوشتنی. Export JSON و CSV ساده و بدون Package
جدید (نقل‌قول‌گذاری استاندارد CSV + BOM). Company isolation تست‌شده (سند نامتوازنِ شرکت دیگر نشت
نمی‌کند؛ سالِ شرکت دیگر با شناسهٔ این شرکت null می‌شود). Idempotency تست‌شده.

---

## مرحله ۱۳ — Trial Close و تسعیرِ پایان دوره ✅

### تصمیم‌های قطعی (تأییدشده از کاربر در این چت)
- **مبنای نرخِ بستن:** `DailyFxRate` با `Base=USD, Quote=ارز`، سطرِ با `RateDate == FiscalYear.EndDate`.
  تبدیل: `usd = SourceAmount / Rate`. **نبودِ نرخِ دقیقِ EndDate یک Blocker است؛ هیچ fallback نرخ
  (قبلی/بعدی/پیش‌فرض) استفاده نمی‌شود.**
- Trial Close سال را نمی‌بندد، دوره‌ای را HardLock نمی‌کند و ClosedAt را تنظیم نمی‌کند.

### تغییرات مدل + Migration (اجرانشده)
- `AccountingEnums.cs` — `MonetaryTreatment {Unspecified, Monetary, NonMonetary}` و
  `FiscalYearCloseRunType {Trial, Final}`.
- `Account.MonetaryTreatment` (پیش‌فرض Unspecified — هیچ حسابی ضمنی تسعیر نمی‌شود).
- `FiscalYearCloseRun` — `RunType`, `Revision`, `FailureCode`, `ChecklistSnapshotJson`,
  `WarningAcknowledgementsJson`, `ClosingRateSnapshotJson`, `RevaluationJournalIdsJson`,
  `JournalCount`, `DebitTotal`, `CreditTotal`, `SourceDataCutoff`, `LastJournalEntryId`,
  `LastJournalPostedAt`, `SnapshotHash`.
- `AccountingChartSeeder` — تعیینِ صریحِ MonetaryTreatment فقط برای حساب‌های استاندارد:
  `1100 Cash/Bank`، `1200 AR`، `2100 AP` = **Monetary**؛ Advanceها و بقیه = **NonMonetary**.
- Migration: `20260716203917_AddMonetaryTreatmentAndCloseRunSnapshot`
  (فقط جدول‌های `Accounts` و `FiscalYearCloseRuns`). **اجرا نشده.**

### فایل‌های جدید
- `Services/Accounting/TrialCloseModels.cs`، `Services/Accounting/TrialCloseService.cs`
- `Controllers/TrialCloseController.cs`، `Views/TrialClose/Index.cshtml`
- `Models/Accounting/TrialClosePageViewModel.cs`
- `tests/PTGOilSystem.Web.Tests/TrialCloseServiceTests.cs` (۱۴ تست، همه سبز)
- `ClosingChecklistService` — افزودن کنترلِ `MONETARY_TREATMENT_UNSPECIFIED` (Blocked).

### Mapping تسعیر (قاعدهٔ یکنواخت برای Asset و Liability)
```
netUsd    = Σ(Debit − Credit)   (تا EndDate، فقط Posted، فقط حساب‌های Monetary، ارز ≠ USD)
netSource = Σ(±TransactionAmount)   (+ برای بدهکار، − برای بستانکار)
closingUsd = round(netSource / closingRate, 4)
difference = round(closingUsd − netUsd, 4)

difference > 0 : Dr <حساب پولی> diff   Cr 4200 Exchange Gain diff
difference < 0 : Dr 5300 Exchange Loss |diff|   Cr <حساب پولی> |diff|
```
ابعادِ Party/Contract/Shipment/CashAccount روی سطرِ حساب پولی حفظ می‌شوند. خطوطِ تسعیر به USD/۱
پست می‌شوند (خودِ اختلاف یک مبلغِ Functional است) تا تلهٔ گردکردنِ جفت‌ارز پیش نیاید.

### نسخه‌بندی و برگشتِ خودکار
- `SourceEventId = FiscalYearRevaluation:{FiscalYearId}:{Currency}:{Revision}`.
- اجرای مجدد بدون تغییرِ نرخ/مانده → Duplicate و بی‌اثر (تست‌شده).
- تغییرِ نرخ/مانده → نسخهٔ قبلی و برگشتِ خودکارش Supersede می‌شوند و `Revision+1` پست می‌شود
  (سندِ Posted هرگز ویرایش/حذف نمی‌شود).
- برگشتِ خودکار در **اولین دورهٔ بازِ سالِ بعد** پست می‌شود (`…:{rev}:Reversal`)، Idempotent.
- نبودِ سالِ بعد یا دورهٔ بازِ آن → Preview نمایش داده می‌شود ولی Apply با
  `NEXT_YEAR_OPEN_PERIOD_MISSING` رد می‌شود.

### State machine (بدون تغییرِ سال/دوره)
Trial Close فقط یک `FiscalYearCloseRun` با `RunType=Trial` و Snapshot/Hash می‌سازد؛ وضعیتِ سال
`Open` می‌ماند، `ClosedAt` خالی می‌ماند و هیچ دوره‌ای HardLock نمی‌شود (تست‌شده).

### مسیرها و دسترسی
`GET preview`، `POST run`، `POST apply-revaluation` — همه AdminOnly؛ POSTها antiforgery؛ هیچ
عملیاتِ تغییردهنده با GET نیست.

### ⚠️ Migration اجرانشده
```
dotnet ef database update --project src/PTGOilSystem.Web   # فقط روی دیتابیس هدف درست
```

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
