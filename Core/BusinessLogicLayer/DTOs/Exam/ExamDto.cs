namespace BusinessLayer.DTOs.Exam;

public class ExamDto
{
    public int Id { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public string RawContent { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime? ScheduledStartTime { get; set; }
    public DateTime ScheduledEndTime { get; set; }
    public DateTime ServerTimeUtc { get; set; }
    public string TimeZoneId { get; set; } = "UTC";
    public int QuestionCount { get; set; }
    public int DurationMinutes { get; set; }
    public string? AllowedGroup { get; set; }
    public int MaxAttempts { get; set; } = 1;
    public int PassingScore { get; set; } = 60;
    public bool AllowSpeech { get; set; }
    public bool FlagHeadMovement { get; set; } = true;
    public List<string> AllowedObjects { get; set; } = new();

    public Guid CreatorId { get; set; }

    public List<QuestionDto> Questions { get; set; } = new();
}
public class QuestionDto
{
    public int Id { get; set; }
    public string Text { get; set; } = null!;
    public int Points { get; set; }
    public int OrderIndex { get; set; }
    public List<QuestionOptionDto> Options { get; set; } = new();
}

// Додаємо DTO для варіантів відповіді
public class QuestionOptionDto
{
    public int Id { get; set; }
    public string Text { get; set; } = null!;
    public int OrderIndex { get; set; }
}