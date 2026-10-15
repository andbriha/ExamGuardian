
namespace DataAccessLayer.Entities;

public class ExamSession
{
    public int Id { get; set; }
    public Guid StudentId { get; set; }
    public int ExamId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public double Score { get; set; }
    public int PassingScoreAtStart { get; set; } = 60;
    public bool IsFinished { get; set; }
    public DateTime? DeadlineUtc { get; set; }
    public DateTime? LastSeenAt { get; set; }
    public string Status { get; set; } = "InProgress";
    public string SavedAnswersJson { get; set; } = "{}";
    public string? ExamSnapshotJson { get; set; }
    public string ActiveClientId { get; set; } = "";
    public string? EvidenceClientId { get; set; }
    public string? AdmissionId { get; set; }
    public Guid? SubmissionId { get; set; }
    public string? EvidenceManifestJson { get; set; }
    public DateTime? LastAnalysisAt { get; set; }
    public DateTime? LastAudioAt { get; set; }
    public string IntegrityVerdict { get; set; } = "UnderReview";
    public string? IntegrityNotes { get; set; }
    public Guid? IntegrityReviewedBy { get; set; }
    public DateTime? IntegrityReviewedAt { get; set; }
    public string? IntegrityHistoryJson { get; set; }
    public string EvidenceStatus { get; set; } = "Pending";


    public virtual User Student { get; set; } = null!;
    public virtual Exam Exam { get; set; } = null!;
    public virtual ICollection<ViolationAlert> Violations { get; set; } = new List<ViolationAlert>();
}