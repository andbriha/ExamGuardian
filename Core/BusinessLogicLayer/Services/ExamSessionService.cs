using BusinessLayer.Interfaces;
using BusinessLogicLayer.DTOs.ExamSession;
using DataAccessLayer.Entities;
using DataAccessLayer.Interfaces;

namespace BusinessLogicLayer.Services;

public class ExamSessionService : IExamSessionService
{
    private readonly IUnitOfWork _unitOfWork;

    public ExamSessionService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<IEnumerable<ExamSessionDto>> GetAllAsync()
    {
        var repo = (IExamSessionRepository)_unitOfWork.Repository<ExamSession>();
        var sessions = await repo.GetAllWithDetailsAsync();
        return sessions.Select(MapToDto);
    }

    public async Task<ExamSessionDto?> GetByIdAsync(int id)
    {
        var repo = (IExamSessionRepository)_unitOfWork.Repository<ExamSession>();
        var session = await repo.GetWithDetailsAsync(id);
        return session is null ? null : MapToDto(session);
    }

    public async Task<IEnumerable<ExamSessionDto>> GetByStudentAsync(Guid studentId)
    {
        var repo = (IExamSessionRepository)_unitOfWork.Repository<ExamSession>();
        var sessions = await repo.GetByStudentAsync(studentId);
        return sessions.Select(MapToDto);
    }

    public async Task<IEnumerable<ExamSessionDto>> GetByExamAsync(int examId)
    {
        var repo = (IExamSessionRepository)_unitOfWork.Repository<ExamSession>();
        var sessions = await repo.GetByExamAsync(examId);
        return sessions.Select(MapToDto);
    }

    public async Task<ExamSessionDto> StartSessionAsync(StartSessionDto dto)
    {
        var users = _unitOfWork.Repository<User>();
        var exams = (IExamRepository)_unitOfWork.Repository<Exam>();
        var sessions = (IExamSessionRepository)_unitOfWork.Repository<ExamSession>();

        var student = await users.GetByIdAsync(dto.StudentId)
            ?? throw new KeyNotFoundException($"User {dto.StudentId} not found.");

        var exam = await exams.GetByIdAsync(dto.ExamId)
            ?? throw new KeyNotFoundException($"Exam {dto.ExamId} not found.");

        var active = await sessions.GetActiveSessionAsync(dto.StudentId, dto.ExamId);
        if (active is not null)
            return MapToDto(active);

        var session = new ExamSession
        {
            StudentId = dto.StudentId,
            ExamId = dto.ExamId,
            StartTime = DateTime.UtcNow,
            IsFinished = false,
            Student = student,
            Exam = exam
        };

        await sessions.AddAsync(session);
        await _unitOfWork.CompleteAsync();

        return MapToDto(session);
    }

    public async Task<ExamSessionDto> FinishSessionAsync(int sessionId, double score)
    {
        var sessions = (IExamSessionRepository)_unitOfWork.Repository<ExamSession>();

        var session = await sessions.GetWithDetailsAsync(sessionId)
            ?? throw new KeyNotFoundException($"Session {sessionId} not found.");

        if (session.IsFinished)
        {
            // Якщо сесію вже завершено, повертаємо її без виключення
            return MapToDto(session);
        }

        session.EndTime = DateTime.UtcNow;
        session.Score = score;
        session.IsFinished = true;

        sessions.Update(session);
        await _unitOfWork.CompleteAsync();

        return MapToDto(session);
    }

    private static ExamSessionDto MapToDto(ExamSession es) => new()
    {
        Id = es.Id,
        StudentId = es.StudentId,
        StudentName = es.Student?.FullName ?? "",
        ExamId = es.ExamId,
        ExamTitle = es.Exam?.Title ?? "",
        StartTime = es.StartTime,
        EndTime = es.EndTime,
        Score = es.Score,
        IsFinished = es.IsFinished,
        ViolationCount = es.Violations?.Count ?? 0,
    };
}