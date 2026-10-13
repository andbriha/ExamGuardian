namespace DataAccessLayer.Entities;

public class Question
{
    public int Id { get; set; }
    public int ExamId { get; set; }
    public string Text { get; set; } = null!;
    public int Points { get; set; }
    public int CorrectIndex { get; set; }
    public int OrderIndex { get; set; }

    public virtual Exam Exam { get; set; } = null!;
    public virtual ICollection<QuestionOption> Options { get; set; } = new List<QuestionOption>();
}

