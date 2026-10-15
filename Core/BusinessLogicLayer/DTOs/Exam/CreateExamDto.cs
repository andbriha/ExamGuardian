using BusinessLogicLayer.DTOs.Exam;

namespace BusinessLayer.DTOs.Exam;

public class CreateExamDto
{
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public DateTime? ScheduledStartTime { get; set; }
    public string? ScheduledStartLocal { get; set; }
    public string? TimeZoneId { get; set; }
    public int DurationMinutes { get; set; }
    public string? AllowedGroup { get; set; }
    public int MaxAttempts { get; set; } = 1;
    public int PassingScore { get; set; } = 60;
    public bool AllowSpeech { get; set; }
    public bool FlagHeadMovement { get; set; } = true;
    public List<string> AllowedObjects { get; set; } = new();

    public Guid CreatorId { get; set; }
    public List<CreateQuestionDto> Questions { get; set; } = new();
}