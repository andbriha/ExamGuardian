using System.Text.Json.Serialization;
namespace BusinessLogicLayer.DTOs.ExamSession;

public class StartSessionDto
{
    public Guid StudentId { get; set; }
    public int ExamId { get; set; }
    public string AdmissionToken { get; set; } = "";
    public string AdmissionId { get; set; } = "";
}