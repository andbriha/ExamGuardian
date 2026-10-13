namespace BusinessLogicLayer.DTOs.ExamSession;

public class SubmitAnswersDto
{
    public int SessionId { get; set; }
    public Dictionary<int, int> Answers { get; set; } = new();
}