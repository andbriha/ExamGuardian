using DataAccessLayer.Entities;
using DataAccessLayer.Interfaces;


namespace DataAccessLayer.Interfaces;

public interface IExamRepository : IRepository<Exam>
{
    Task<IEnumerable<Exam>> GetByCreatorAsync(Guid creatorId);
    Task<Exam?> GetWithSessionsAsync(int examId);
    Task<Exam?> GetWithQuestionsAsync(int examId);
}