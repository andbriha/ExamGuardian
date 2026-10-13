namespace BusinessLogicLayer.DTOs.ExamSession;

public class ExamSessionDto
{
    public int Id { get; set; }
    public Guid StudentId { get; set; }
    public string StudentName { get; set; } = null!;
    public int ExamId { get; set; }
    public string ExamTitle { get; set; } = null!;
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public double Score { get; set; }
    public bool IsFinished { get; set; }
    public int ViolationCount { get; set; }
}