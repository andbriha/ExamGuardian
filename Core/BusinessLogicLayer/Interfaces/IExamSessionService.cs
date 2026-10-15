using BusinessLayer.DTOs.Exam;
using BusinessLogicLayer.DTOs.ExamSession;

namespace BusinessLayer.Interfaces;

public interface IExamSessionService
{
    Task<ExamDto?> GetExamForSessionAsync(Guid studentId, int examId);
    Task<IEnumerable<ExamSessionDto>> GetAllAsync();
    Task<IEnumerable<ExamSessionDto>> GetByIdsAsync(List<int> ids);
    Task<ExamSessionDto?> GetByIdAsync(int id);
    Task<IEnumerable<ExamSessionDto>> GetByStudentAsync(Guid studentId);
    Task<IEnumerable<ExamSessionDto>> GetByExamAsync(int examId);
    Task<ExamSessionDto> StartSessionAsync(StartSessionDto dto);
    Task<ExamSessionDto> SubmitAsync(Guid studentId, int examId, SubmitAnswersDto dto);
    Task<ExamSessionDto> SaveAnswersAsync(Guid studentId, int sessionId, Dictionary<int, int> answers, string clientId);
    Task<ExamSessionDto> HeartbeatAsync(Guid studentId, int sessionId, string clientId);
    Task<ExamSessionDto> TerminateAsync(int sessionId);
    Task<ExamSessionDto> ReviewSessionAsync(int sessionId, Guid reviewerId, string verdict, string notes);

}