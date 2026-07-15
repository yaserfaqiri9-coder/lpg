# حسابداری دوطرفه و سال مالی — وضعیت پیشرفت و راهنمای ادامه

> آخرین به‌روزرسانی: 2026-07-15
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
| ۴ | دریافت و پرداخت | ⛔ شروع‌نشده |
| ۵ | مصارف، کرایه، کمیسیون | ⛔ شروع‌نشده |
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

**Build فعلی:** ۰ خطا / ۰ هشدار.
**تست‌های Accounting مرتبط:** ۸۱ پاس (Accounting Core + DatabaseSafety + ContractBalanceTransfer + SupplierPaymentAllocation + PaymentCompanyOwnership).
**Full Suite:** ۱۸ شکست قدیمی (پیش‌موجود، غیرمرتبط با این کار) + ۰ شکست جدید.

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

## نقطه‌ی دقیق توقف

آخرین کار انجام‌شده: **مرحله ۳ کامل شد**، Build سبز، ۸۱ تست Accounting سبز، ۰ شکست جدید.
هنوز وارد مرحله ۴ **نشده‌ایم**.

هیچ Migration روی دیتابیس عملیاتی اجرا نشده. هیچ Backfill حدسی انجام نشده. هیچ داده‌ی عملیاتی
حذف نشده. تغییرات UI نامرتبط در Working Tree (stat-cards `.webp`/`.css`، `docs/ui-references/*.png`)
**دست‌نخورده** باقی مانده و در این Commitها وارد نشده.

---

## از کجا ادامه بدهم؟ (مرحله ۴ — دریافت و پرداخت)

قدم‌های بعدی، دقیقاً مطابق پرامپت اصلی:

1. **قبل از هر کد، تصمیم مالکیت Company را در Adapter مرحله ۲ به‌روز کن (TODO):**
   الان `SupplierPaymentAllocationAccountingAdapter` شرکت را از «قرارداد پرداخت» می‌گیرد.
   حالا که `PaymentTransaction.CompanyId` وجود دارد، می‌توان مستقیماً از آن استفاده کرد
   (با Fallback به قرارداد). این یک بهبود کوچک و اختیاری است، نه بازنویسی.

2. Adapterهای مستقل با Feature Flag جدا برای هر زیرماژول:
   `CustomerReceipt`, `SupplierPayment`, `SupplierPrepayment`, `CustomerAdvance`, `SarrafPayment`.
   (به `AccountingPilotOptions` فیلد جدید اضافه کن، پیش‌فرض false، در `appsettings.json` هم.)

3. Mapping پایه (از پرامپت):
   ```
   دریافت از مشتری     : Dr Cash/Bank            Cr Accounts Receivable
   پیش‌دریافت مشتری     : Dr Cash/Bank            Cr Customer Advance
   پرداخت به تأمین‌کننده: Dr Accounts Payable     Cr Cash/Bank
   پیش‌پرداخت تأمین‌کننده: Dr Supplier Prepayment  Cr Cash/Bank
   پرداخت صراف         : Dr Accounts Payable تأمین‌کننده   Cr Accounts Payable صراف
   ```
4. قواعد کلیدی: نوع رویداد را از ماهیت واقعی Payment تعیین کن (نه فقط LedgerSide)؛
   `CashAccountId` روی خط Cash/Bank؛ `PartyType`/`PartyId` روی خط طرف‌حساب؛
   Debit/Credit به ارز Functional (USD)؛ ویرایش Posted فقط با Reversal.
5. مسیرهای مبهم (`ManualPayment`, `ManualReceipt`, `ManualAdjustment`): به GL جدید **متصل نشوند**؛
   فقط گزارش فنی. فرم فعلی تغییر نکند.
6. هر زیرماژول: Build + Test مستقل. اگر تصمیم حسابداری مبهم شد، **توقف و سؤال دقیق**.

### الگوی موجود برای کپی‌برداری (مرجع دست‌اول)
Adapter مرحله ۲ (`SupplierPaymentAllocationAccountingAdapter`) و تست‌هایش بهترین قالب هستند:
Dual-write داخل Transaction موجود، `SourceEventId` پایدار، Duplicate/Skip/Rollback،
Reconciliation با Legacy، تست PostgreSQL واقعی از طریق `AccountingPostgreSqlFixture`.

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
