using DataAccessLayer.Entities;

public class QuestionOption
{
    public int Id { get; set; }
    public int QuestionId { get; set; }
    public string Text { get; set; } = null!;
    public int OrderIndex { get; set; }

    public virtual Question Question { get; set; } = null!;
}