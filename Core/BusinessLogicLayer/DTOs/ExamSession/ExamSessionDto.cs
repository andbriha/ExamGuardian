using System.Text.Json.Serialization;
namespace BusinessLogicLayer.DTOs.ExamSession;

public class ExamSessionDto
{
    public int Id { get; set; }
    [JsonIgnore] public string ActiveClientId { get; set; } = "";
    [JsonIgnore] public string? EvidenceClientId { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? AttemptToken { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? EvidenceToken { get; set; }
    public bool IdentityVerificationSkipped { get; set; }
    public Guid StudentId { get; set; }
    public string StudentName { get; set; } = null!;
    public int ExamId { get; set; }
    public string ExamTitle { get; set; } = null!;
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public bool ScoreCalculated { get; set; }
    public double Score { get; set; }
    public int PassingScoreAtStart { get; set; } = 60;
    public bool IsFinished { get; set; }
    public int ViolationCount { get; set; }
    public int PendingObservationCount { get; set; }
    public int ConfirmedObservationCount { get; set; }
    public DateTime? DeadlineUtc { get; set; }
    public DateTime ServerTimeUtc { get; set; }
    public DateTime? LastAnalysisAt { get; set; }
    public DateTime? LastAudioAt { get; set; }
    public DateTime? LastSeenAt { get; set; }
    public string Status { get; set; } = "InProgress";
    public string IntegrityVerdict { get; set; } = "UnderReview";
    public string? IntegrityNotes { get; set; }
    public Guid? IntegrityReviewedBy { get; set; }
    public DateTime? IntegrityReviewedAt { get; set; }
    public string? IntegrityHistoryJson { get; set; }
    public string EvidenceStatus { get; set; } = "Pending";
    public Dictionary<int, int> SavedAnswers { get; set; } = new();

}