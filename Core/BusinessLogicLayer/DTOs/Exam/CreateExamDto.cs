using BusinessLogicLayer.DTOs.Exam;

namespace BusinessLayer.DTOs.Exam;

public class CreateExamDto
{
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public DateTime? ScheduledStartTime { get; set; }
    public int DurationMinutes { get; set; }
    public Guid CreatorId { get; set; }
    public List<CreateQuestionDto> Questions { get; set; } = new();
}