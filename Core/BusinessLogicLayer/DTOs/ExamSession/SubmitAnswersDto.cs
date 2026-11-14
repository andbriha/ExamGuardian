namespace BusinessLogicLayer.DTOs.ExamSession;

public class SubmitAnswersDto
{
    [System.Text.Json.Serialization.JsonIgnore] public string? ClientId { get; set; }
    [System.Text.Json.Serialization.JsonIgnore] public bool DeadlineSubmission { get; set; }
    public int SessionId { get; set; }
    public Guid SubmissionId { get; set; }
    public Dictionary<int, int> Answers { get; set; } = new();
}