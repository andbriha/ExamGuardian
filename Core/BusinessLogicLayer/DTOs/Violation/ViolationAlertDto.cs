namespace BusinessLayer.DTOs.Violation
{
    public class ViolationAlertDto
    {
        public int Id { get; set; }
        public int ExamSessionId { get; set; }
        public string ViolationType { get; set; } = null!;
        public double Confidence { get; set; }
        public int RelativeTimestampSeconds { get; set; }
        public string? EvidenceUrl { get; set; }
        public string? Metadata { get; set; }
        public DateTime Timestamp { get; set; }
        public bool IsReviewed { get; set; }
    }
}