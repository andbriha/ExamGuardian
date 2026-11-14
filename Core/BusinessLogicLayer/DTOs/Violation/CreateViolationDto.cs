namespace BusinessLayer.DTOs.Violation
{
    public class CreateViolationDto
    {
        public int ExamSessionId { get; set; }
        public Guid EventId { get; set; }
        public string ViolationType { get; set; } = null!;
        public double Confidence { get; set; }
        public int RelativeTimestampSeconds { get; set; }
        public string? EvidenceUrl { get; set; }
        // Metadata — JSON рядок з debug-інфо від моделі (yaw, pitch тощо)
        public string? Metadata { get; set; }
    }
}