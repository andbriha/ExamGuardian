
namespace DataAccessLayer.Entities;

public class User
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = null!;
    public string Login { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public string Role { get; set; } = null!;
    public string? Group { get; set; }
    public string? BioVector { get; set; }
    public string? PhotoUrl { get; set; }

    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiryTime { get; set; }

    public virtual ICollection<Exam> CreatedExams { get; set; } = new List<Exam>();
    public virtual ICollection<ExamSession> Sessions { get; set; } = new List<ExamSession>();
}