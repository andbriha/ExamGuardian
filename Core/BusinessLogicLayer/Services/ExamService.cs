using BusinessLayer.DTOs.Exam;
using BusinessLayer.Interfaces;
using DataAccessLayer.Entities;
using DataAccessLayer.Interfaces;

namespace BusinessLogicLayer.Services;

public class ExamService : IExamService
{
    private readonly IUnitOfWork _unitOfWork;

    public ExamService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ExamDto?> GetByIdAsync(int id)
    {
        var repo = (IExamRepository)_unitOfWork.Repository<Exam>();
        var exam = await repo.GetByIdAsync(id);
        return exam is null ? null : MapToDto(exam);
    }

    public async Task<IEnumerable<ExamDto>> GetAllAsync()
    {
        var repo = (IExamRepository)_unitOfWork.Repository<Exam>();
        var exams = await repo.GetAllAsync();
        return exams.Select(MapToDto);
    }

    public async Task<IEnumerable<ExamDto>> GetByCreatorAsync(Guid creatorId)
    {
        var repo = (IExamRepository)_unitOfWork.Repository<Exam>();
        var exams = await repo.GetByCreatorAsync(creatorId);
        return exams.Select(MapToDto);
    }

    public async Task<ExamDto> CreateAsync(CreateExamDto dto)
    {
        var repo = (IExamRepository)_unitOfWork.Repository<Exam>();

        var exam = new Exam
        {
            Title = dto.Title,
            Description = dto.Description,
            RawContent = string.Empty,
            CreatorId = dto.CreatorId,
            CreatedAt = DateTime.UtcNow,
            ScheduledStartTime = dto.ScheduledStartTime,
            DurationMinutes = dto.DurationMinutes,

            Questions = dto.Questions.Select((q, i) => new Question
            {
                Text = q.Text,
                Points = q.Points,
                CorrectIndex = q.CorrectIndex,
                OrderIndex = i,
                Options = q.Options.Select((o, j) => new QuestionOption
                {
                    Text = o,
                    OrderIndex = j,
                }).ToList()
            }).ToList()
        };

        await repo.AddAsync(exam);
        await _unitOfWork.CompleteAsync();

        return MapToDto(exam);
    }

    public async Task DeleteAsync(int id)
    {
        var repo = (IExamRepository)_unitOfWork.Repository<Exam>();
        var exam = await repo.GetByIdAsync(id) ?? throw new KeyNotFoundException($"Exam {id} not found.");

        repo.Delete(exam);
        await _unitOfWork.CompleteAsync();
    }

    public async Task<IEnumerable<ExamDto>> GetAvailableAsync()
    {
        var repo = (IExamRepository)_unitOfWork.Repository<Exam>();
        var exams = await repo.GetAllAsync();
        return exams.Select(MapToDto);
    }

    public async Task<ExamDto?> GetForStudentAsync(int id)
    {
        var repo = (IExamRepository)_unitOfWork.Repository<Exam>();
        var exam = await repo.GetWithQuestionsAsync(id);
        return exam is null ? null : MapToDto(exam);
    }

    public async Task<int> CalculateScoreAsync(int examId, Dictionary<int, int> answers)
    {
        var repo = (IExamRepository)_unitOfWork.Repository<Exam>();
        var exam = await repo.GetWithQuestionsAsync(examId)
            ?? throw new KeyNotFoundException($"Exam {examId} not found");

        int score = 0;
        int maxPoints = 0;
        foreach (var q in exam.Questions)
        {
            maxPoints += q.Points;
            if (answers.TryGetValue(q.Id, out int selectedIdx) && selectedIdx == q.CorrectIndex)
                score += q.Points;
        }

        if (maxPoints > 0)
        {
            return (int)Math.Round((double)score / maxPoints * 100.0);
        }

        return score;
    }
    private static ExamDto MapToDto(Exam e) => new()
    {
        Id = e.Id,
        Title = e.Title,
        Description = e.Description,
        RawContent = e.RawContent,
        CreatedAt = e.CreatedAt,
        ScheduledStartTime = e.ScheduledStartTime,
        DurationMinutes = e.DurationMinutes,
        CreatorId = e.CreatorId,

        // Додаємо мапінг питань, якщо вони підвантажені з бази
        Questions = e.Questions?.Select(q => new QuestionDto
        {
            Id = q.Id,
            Text = q.Text,
            Points = q.Points,
            OrderIndex = q.OrderIndex,
            Options = q.Options?.Select(o => new QuestionOptionDto
            {
                Id = o.Id,
                Text = o.Text,
                OrderIndex = o.OrderIndex
            }).ToList() ?? []
        }).ToList() ?? []
    };
}