using DataAccessLayer.Entities;

namespace DataAccessLayer.Interfaces;

public interface IExamSessionRepository : IRepository<ExamSession>
{
    Task<IEnumerable<ExamSession>> GetAllWithDetailsAsync();
    Task<ExamSession?> GetWithDetailsAsync(int sessionId);
    Task<IEnumerable<ExamSession>> GetByStudentAsync(Guid studentId);
    Task<IEnumerable<ExamSession>> GetByExamAsync(int examId);
    Task<ExamSession?> GetActiveSessionAsync(Guid studentId, int examId);
}