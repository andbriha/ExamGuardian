namespace BusinessLayer.DTOs.Exam;

public class ExamDto
{
    public int Id { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public string RawContent { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime? ScheduledStartTime { get; set; }
    public int DurationMinutes { get; set; }
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