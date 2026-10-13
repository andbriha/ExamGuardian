namespace BusinessLogicLayer.DTOs.ExamSession;

public class StartSessionDto
{
    public Guid StudentId { get; set; }
    public int ExamId { get; set; }
}