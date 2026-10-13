using BusinessLogicLayer.DTOs.ExamSession;

namespace BusinessLayer.Interfaces;

public interface IExamSessionService
{
    Task<IEnumerable<ExamSessionDto>> GetAllAsync();
    Task<ExamSessionDto?> GetByIdAsync(int id);
    Task<IEnumerable<ExamSessionDto>> GetByStudentAsync(Guid studentId);
    Task<IEnumerable<ExamSessionDto>> GetByExamAsync(int examId);
    Task<ExamSessionDto> StartSessionAsync(StartSessionDto dto);
    Task<ExamSessionDto> FinishSessionAsync(int sessionId, double score);
}