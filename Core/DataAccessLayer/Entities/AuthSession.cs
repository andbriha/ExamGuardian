namespace DataAccessLayer.Entities;
public class AuthSession {
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public string CredentialStamp { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
}
public class RefreshCredential {
    public string Hash { get; set; } = "";
    public Guid SessionId { get; set; }
    public AuthSession Session { get; set; } = null!;
    public DateTime? ConsumedAt { get; set; }
}
public class ExaminerGroupGrant {
    public Guid ExaminerId { get; set; }
    public string Group { get; set; } = "";
}
