
namespace DataAccessLayer.Entities;

public class ExamSession
{
    public int Id { get; set; }
    public Guid StudentId { get; set; }
    public int ExamId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public double Score { get; set; }
    public bool IsFinished { get; set; }

    public virtual User Student { get; set; } = null!;
    public virtual Exam Exam { get; set; } = null!;
    public virtual ICollection<ViolationAlert> Violations { get; set; } = new List<ViolationAlert>();
}