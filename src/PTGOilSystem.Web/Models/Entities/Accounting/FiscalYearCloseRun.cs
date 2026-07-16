using System.ComponentModel.DataAnnotations;

namespace PTGOilSystem.Web.Models.Entities;

public class FiscalYearCloseRun : BaseEntity
{
    public int CompanyId { get; set; }
    public Company? Company { get; set; }
    public int FiscalYearId { get; set; }
    public FiscalYear? FiscalYear { get; set; }

    /// <summary>
    /// نوعِ اجرا. مقدارِ پیش‌فرض Trial است؛ رکوردهای قدیمیِ احتمالی (که پیش از این فیلد ساخته
    /// شده‌اند) هم Trial خوانده می‌شوند — چون هیچ Final قدیمی وجود ندارد، این فرضِ امن است نه حدس.
    /// </summary>
    public FiscalYearCloseRunType RunType { get; set; } = FiscalYearCloseRunType.Trial;

    /// <summary>نسخهٔ اجرا. اجرای مجدد بدون تغییر نرخ/مانده همان Revision می‌ماند؛ تغییر → Revision بعدی.</summary>
    public int Revision { get; set; }

    public FiscalYearCloseRunStatus Status { get; set; } = FiscalYearCloseRunStatus.Pending;
    public DateTime StartedAt { get; set; }
    public int? StartedByUserId { get; set; }
    public User? StartedByUser { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int? CompletedByUserId { get; set; }
    public User? CompletedByUser { get; set; }
    public int? ClosingJournalEntryId { get; set; }
    public JournalEntry? ClosingJournalEntry { get; set; }
    public int? OpeningJournalEntryId { get; set; }
    public JournalEntry? OpeningJournalEntry { get; set; }

    [MaxLength(100)]
    public string? FailureCode { get; set; }

    [MaxLength(2000)]
    public string? FailureMessage { get; set; }

    // ---- مرحله ۱۳/۱۴ — Snapshotِ قابل‌حسابرسی. همه فقط ذخیره می‌شوند و پس از آن تغییرناپذیرند. ----

    /// <summary>Snapshotِ چک‌لیستِ مرحله ۱۲ در لحظهٔ اجرا (JSON).</summary>
    public string? ChecklistSnapshotJson { get; set; }

    /// <summary>تأییدِ صریحِ Warningها برای ادامه (JSON).</summary>
    public string? WarningAcknowledgementsJson { get; set; }

    /// <summary>Snapshotِ نرخ‌های بستنِ استفاده‌شده به تفکیک ارز (JSON).</summary>
    public string? ClosingRateSnapshotJson { get; set; }

    /// <summary>شناسه‌های سندهای تسعیرِ ساخته‌شده در این اجرا (JSON).</summary>
    public string? RevaluationJournalIdsJson { get; set; }

    public int JournalCount { get; set; }
    public decimal DebitTotal { get; set; }
    public decimal CreditTotal { get; set; }

    /// <summary>مرزِ داده: هیچ سندِ بعد از این تاریخ در Snapshot نیست (معمولاً FiscalYear.EndDate).</summary>
    public DateTime? SourceDataCutoff { get; set; }

    public int? LastJournalEntryId { get; set; }
    public DateTime? LastJournalPostedAt { get; set; }

    /// <summary>امضای Snapshot — تشخیصِ کهنگی و تغییر بین Trial و Final.</summary>
    [MaxLength(128)]
    public string? SnapshotHash { get; set; }

    public uint RowVersion { get; set; }
}
