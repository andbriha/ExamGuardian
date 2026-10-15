namespace BusinessLogicLayer.DTOs.Exam;

public class CreateQuestionDto
{
    public string Text { get; set; } = null!;
    public int Points { get; set; } = 1;
    public int CorrectIndex { get; set; }
    public int OrderIndex { get; set; }
    public List<string> Options { get; set; } = new();
}