
namespace DataAccessLayer.Entities;

public class Exam
{
    public int Id { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public string RawContent { get; set; } = null!;
    public DateTime CreatedAt { get; set; }

    public DateTime? ScheduledStartTime { get; set; }
    public int DurationMinutes { get; set; }
    public string? AllowedGroup { get; set; }
    public int MaxAttempts { get; set; } = 1;
    public int PassingScore { get; set; } = 60;
    public bool AllowSpeech { get; set; }
    public bool FlagHeadMovement { get; set; } = true;
    public string AllowedObjectsJson { get; set; } = "[]";



    public Guid CreatorId { get; set; }
    public virtual User Creator { get; set; } = null!;

    public virtual ICollection<ExamSession> Sessions { get; set; } = new List<ExamSession>();
    public virtual ICollection<Question> Questions { get; set; } = new List<Question>();
}