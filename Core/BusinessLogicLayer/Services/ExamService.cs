using System.Text.Json;
using System.Globalization;
using Microsoft.Extensions.Configuration;
using BusinessLayer.DTOs.Exam;
using BusinessLayer.Interfaces;
using DataAccessLayer.Entities;
using DataAccessLayer.Interfaces;

namespace BusinessLogicLayer.Services;

public class ExamService : IExamService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IConfiguration _config;

    public ExamService(IUnitOfWork unitOfWork, IConfiguration config)
    {
        _unitOfWork = unitOfWork;
        _config = config;
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
        if (string.IsNullOrWhiteSpace(dto.Title) || dto.DurationMinutes is < 1 or > 480 || dto.Questions.Count == 0 || dto.Questions.Count > 500 || dto.MaxAttempts is < 1 or > 10 || dto.PassingScore is < 0 or > 100)
            throw new ArgumentException("Title, duration and at least one question are required.");
        if (dto.Questions.Any(q => string.IsNullOrWhiteSpace(q.Text) || q.Points < 1 || q.Points > 1000 || q.Options.Count < 2 || q.Options.Count > 10 || q.Options.Any(string.IsNullOrWhiteSpace) || q.CorrectIndex < 0 || q.CorrectIndex >= q.Options.Count))
            throw new ArgumentException("Invalid question, points or correct option.");
        if (dto.AllowedObjects.Any(o => o is not ("book" or "laptop" or "cell_phone"))) throw new ArgumentException("Invalid allowed object type.");
        if (dto.ScheduledStartTime?.Kind == DateTimeKind.Unspecified) throw new ArgumentException("Scheduled time must include a UTC or timezone offset.");
        DateTime? scheduled = dto.ScheduledStartTime?.ToUniversalTime();
        if (!string.IsNullOrWhiteSpace(dto.ScheduledStartLocal)) {
            if (dto.ScheduledStartTime is not null) throw new ArgumentException("Use either local time with a timezone or an explicit UTC timestamp.");
            if (!DateTime.TryParseExact(dto.ScheduledStartLocal, new[]{"yyyy-MM-ddTHH:mm","yyyy-MM-ddTHH:mm:ss"}, CultureInfo.InvariantCulture, DateTimeStyles.None, out var local)) throw new ArgumentException("Invalid scheduled local time.");
            TimeZoneInfo zone;
            try { zone=TimeZoneInfo.FindSystemTimeZoneById(dto.TimeZoneId ?? _config["Exam:TimeZone"] ?? "Europe/Chisinau"); }
            catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException) { throw new ArgumentException("Unknown exam timezone."); }
            local=DateTime.SpecifyKind(local,DateTimeKind.Unspecified);
            if(zone.IsInvalidTime(local) || zone.IsAmbiguousTime(local)) throw new ArgumentException("This local time is missing or ambiguous because of daylight saving. Choose another time or send an explicit UTC timestamp.");
            scheduled=TimeZoneInfo.ConvertTimeToUtc(local,zone);
        }
        var now=DateTime.UtcNow;
        var repo = (IExamRepository)_unitOfWork.Repository<Exam>();

        var exam = new Exam
        {
            Title = dto.Title,
            Description = dto.Description,
            RawContent = string.Empty, AllowSpeech = dto.AllowSpeech, FlagHeadMovement = dto.FlagHeadMovement, AllowedObjectsJson = JsonSerializer.Serialize(dto.AllowedObjects.Distinct()),
            CreatorId = dto.CreatorId,
            CreatedAt = now,
            ScheduledStartTime = scheduled ?? now,
            DurationMinutes = dto.DurationMinutes, AllowedGroup = string.IsNullOrWhiteSpace(dto.AllowedGroup) ? null : dto.AllowedGroup.Trim(), MaxAttempts = dto.MaxAttempts, PassingScore = dto.PassingScore,

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
        var exams = await repo.GetCatalogAsync();
        return exams.Select(e => { var dto = MapToDto(e); dto.Questions.Clear(); return dto; });
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
    private ExamDto MapToDto(Exam e) => new()
    {
        Id = e.Id,
        Title = e.Title,
        Description = e.Description,
        RawContent = e.RawContent, AllowSpeech = e.AllowSpeech, FlagHeadMovement = e.FlagHeadMovement, AllowedObjects = JsonSerializer.Deserialize<List<string>>(e.AllowedObjectsJson) ?? new(),
        CreatedAt = e.CreatedAt,
        ScheduledStartTime = e.ScheduledStartTime ?? e.CreatedAt,
        ScheduledEndTime = (e.ScheduledStartTime ?? e.CreatedAt).AddMinutes(e.DurationMinutes),
        ServerTimeUtc = DateTime.UtcNow,
        TimeZoneId = _config["Exam:TimeZone"] ?? "Europe/Chisinau",
        QuestionCount = e.Questions.Count,
        DurationMinutes = e.DurationMinutes, AllowedGroup = e.AllowedGroup, MaxAttempts = e.MaxAttempts, PassingScore = e.PassingScore,
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