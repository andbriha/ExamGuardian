using BusinessLayer.DTOs.Exam;

namespace BusinessLayer.Interfaces;

public interface IExamService
{
    Task<ExamDto?> GetByIdAsync(int id);
    Task<IEnumerable<ExamDto>> GetAllAsync();
    Task<IEnumerable<ExamDto>> GetByCreatorAsync(Guid creatorId);
    Task<ExamDto> CreateAsync(CreateExamDto dto);
    Task DeleteAsync(int id);

    Task<IEnumerable<ExamDto>> GetAvailableAsync();
    Task<ExamDto?> GetForStudentAsync(int id);
    Task<int> CalculateScoreAsync(int examId, Dictionary<int, int> answers);
}