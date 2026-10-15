using BusinessLayer.DTOs.Exam;
using System.Text.Json;
using BusinessLayer.Interfaces;
using BusinessLogicLayer.Constants;
using BusinessLogicLayer.DTOs.ExamSession;
using DataAccessLayer.Data;
using DataAccessLayer.Entities;
using Microsoft.EntityFrameworkCore;

namespace BusinessLogicLayer.Services;

public record SessionQuestion(int Id, int Points, int CorrectIndex, int OptionCount, string? Text = null, string[]? Options = null);

public class ExamSessionService(AppDbContext db) : IExamSessionService
{
    private IQueryable<ExamSession> Details => db.Sessions.Include(s => s.Student).Include(s => s.Exam).Include(s => s.Violations);
    public async Task<IEnumerable<ExamSessionDto>> GetByIdsAsync(List<int> ids) => (await Details.AsNoTracking().AsSplitQuery().Where(s => ids.Contains(s.Id)).OrderByDescending(s => s.StartTime).ToListAsync()).Select(MapToDto);
    public async Task<IEnumerable<ExamSessionDto>> GetAllAsync() => (await Details.OrderByDescending(s => s.StartTime).ToListAsync()).Select(MapToDto);
    public async Task<ExamSessionDto?> GetByIdAsync(int id) => (await Details.FirstOrDefaultAsync(s => s.Id == id)) is { } s ? MapToDto(s) : null;
    public async Task<IEnumerable<ExamSessionDto>> GetByStudentAsync(Guid studentId) => (await Details.Where(s => s.StudentId == studentId).ToListAsync()).Select(MapToDto);
    public async Task<IEnumerable<ExamSessionDto>> GetByExamAsync(int examId) => (await Details.Where(s => s.ExamId == examId).ToListAsync()).Select(MapToDto);

    public async Task<ExamSessionDto> StartSessionAsync(StartSessionDto dto)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var lockName = $"attempt:{dto.StudentId}:{dto.ExamId}";
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(2,hashtext({lockName}))");
        var student = await db.Users.FindAsync(dto.StudentId) ?? throw new KeyNotFoundException("Student not found.");
        var exam = await db.Exams.Include(e => e.Questions).ThenInclude(q => q.Options).FirstOrDefaultAsync(e => e.Id == dto.ExamId)
            ?? throw new KeyNotFoundException("Exam not found.");
        if (student.Role != SystemRoles.Student) throw new InvalidOperationException("Student access required.");
        if (!string.IsNullOrWhiteSpace(exam.AllowedGroup) && exam.AllowedGroup != student.Group)
            throw new InvalidOperationException("This exam is not assigned to your group.");
        var now = DateTime.UtcNow;
        var scheduledStart=exam.ScheduledStartTime ?? exam.CreatedAt;
        var scheduledEnd=scheduledStart.AddMinutes(exam.DurationMinutes);
        if (now >= scheduledEnd) throw new InvalidOperationException("The exam window has ended.");
        if (scheduledStart > now) throw new InvalidOperationException("The exam has not started yet.");
        var session = await Details.FirstOrDefaultAsync(s => s.StudentId == dto.StudentId && s.ExamId == dto.ExamId && !s.IsFinished);
        if (session is not null) {
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(1,{session.Id})");
            await db.Entry(session).ReloadAsync();
            if (session.IsFinished) session=null;
        }
        if (session is null)
        {
            if (await db.Sessions.AnyAsync(s => s.AdmissionId == dto.AdmissionId)) throw new InvalidOperationException("This verification has already been used.");
            if (await db.Sessions.CountAsync(s => s.StudentId == dto.StudentId && s.ExamId == dto.ExamId) >= exam.MaxAttempts)
                throw new InvalidOperationException("The attempt limit has been reached.");
            if (exam.DurationMinutes is < 1 or > 480 || exam.Questions.Count == 0) throw new InvalidOperationException("The exam is not ready.");
            session = new ExamSession { Student = student, StudentId = student.Id, Exam = exam, ExamId = exam.Id,
                PassingScoreAtStart = exam.PassingScore, StartTime = now, DeadlineUtc = scheduledEnd, AdmissionId = dto.AdmissionId,
                ExamSnapshotJson = CreateSnapshot(exam), LastSeenAt = now };
            db.Sessions.Add(session);
        }
        session.ActiveClientId = Guid.NewGuid().ToString("N");
        session.EvidenceClientId ??= Guid.NewGuid().ToString("N");
        session.AdmissionId ??= dto.AdmissionId;
        if(session.DeadlineUtc is null || session.DeadlineUtc > scheduledEnd) session.DeadlineUtc=scheduledEnd;
        session.ExamSnapshotJson ??= CreateSnapshot(exam);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return MapToDto(session);
    }

    private static string CreateSnapshot(Exam exam) => JsonSerializer.Serialize(new { exam.Title, exam.Description, exam.DurationMinutes, exam.PassingScore, exam.AllowSpeech, exam.FlagHeadMovement, AllowedObjects = JsonSerializer.Deserialize<List<string>>(exam.AllowedObjectsJson),
        Questions = exam.Questions.OrderBy(q => q.OrderIndex).Select(q => new SessionQuestion(q.Id,q.Points,q.CorrectIndex,q.Options.Count,q.Text,q.Options.OrderBy(o => o.OrderIndex).Select(o => o.Text).ToArray())) });
    private static List<SessionQuestion> Questions(ExamSession s) {
        using var doc = JsonDocument.Parse(s.ExamSnapshotJson ?? "[]");
        var root = doc.RootElement;
        return JsonSerializer.Deserialize<List<SessionQuestion>>((root.ValueKind == JsonValueKind.Array ? root : root.GetProperty("Questions")).GetRawText()) ?? new();
    }
    public async Task<ExamDto?> GetExamForSessionAsync(Guid studentId, int examId) {
        var session = await Details.Where(s => s.StudentId == studentId && s.ExamId == examId).OrderByDescending(s => s.StartTime).FirstOrDefaultAsync();
        if (session is null) return null;
        var snapshot = Questions(session);
        using var doc = JsonDocument.Parse(session.ExamSnapshotJson ?? "[]");
        return new ExamDto { Id = examId, Title = doc.RootElement.ValueKind == JsonValueKind.Object ? doc.RootElement.GetProperty("Title").GetString()! : session.Exam.Title,
            DurationMinutes = session.Exam.DurationMinutes, PassingScore = session.Exam.PassingScore,
            Questions = snapshot.Select((q,index) => new QuestionDto {Id=q.Id,Text=q.Text ?? "Legacy question",Points=q.Points,OrderIndex=index,
                Options=(q.Options ?? Array.Empty<string>()).Select((text,i) => new QuestionOptionDto{Text=text,OrderIndex=i}).ToList()}).ToList() };
    }
    private static void ValidateAnswers(List<SessionQuestion> questions, Dictionary<int,int> answers)
    {
        foreach (var (id, choice) in answers)
        {
            var q = questions.Find(q => q.Id == id);
            if (q is null || choice < 0 || choice >= q.OptionCount) throw new ArgumentException("Invalid question or answer option.");
        }
    }
    private async Task<ExamSession> OwnSession(Guid studentId, int id) {
        var session = await Details.FirstOrDefaultAsync(s => s.Id == id && s.StudentId == studentId) ?? throw new KeyNotFoundException("Session not found.");
        // An earlier controller read may already be tracked. Reload after the lock before checking lifecycle/lease.
        await db.Entry(session).ReloadAsync();
        return session;
    }

    public async Task<ExamSessionDto> SaveAnswersAsync(Guid studentId, int sessionId, Dictionary<int, int> answers, string clientId)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(1,{sessionId})");
        var s = await OwnSession(studentId, sessionId);
        if (s.ActiveClientId != clientId) throw new InvalidOperationException("The attempt was resumed in another client.");
        if (s.IsFinished || DateTime.UtcNow >= (s.DeadlineUtc ?? s.StartTime.AddMinutes(s.Exam.DurationMinutes)))
            throw new InvalidOperationException("The session no longer accepts answers.");
        ValidateAnswers(Questions(s), answers);
        s.SavedAnswersJson = JsonSerializer.Serialize(answers);
        s.LastSeenAt = DateTime.UtcNow;
        await db.SaveChangesAsync(); await tx.CommitAsync();
        return MapToDto(s);
    }

    public async Task<ExamSessionDto> SubmitAsync(Guid studentId, int examId, SubmitAnswersDto dto)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(1,{dto.SessionId})");
        var s = await OwnSession(studentId, dto.SessionId);
        if (s.ExamId != examId) throw new ArgumentException("Session does not belong to this exam.");
        if (s.IsFinished) { await tx.CommitAsync(); return MapToDto(s); }
        var deadline = s.DeadlineUtc ?? s.StartTime.AddMinutes(s.Exam.DurationMinutes);
        var timedOut = DateTime.UtcNow >= deadline;
        if (dto.DeadlineSubmission && !timedOut) throw new InvalidOperationException("The deadline has not elapsed.");
        if (!dto.DeadlineSubmission && s.ActiveClientId != dto.ClientId) throw new InvalidOperationException("The attempt was resumed in another client.");
        var answers = timedOut ? JsonSerializer.Deserialize<Dictionary<int,int>>(s.SavedAnswersJson) ?? new() : dto.Answers;
        var questions = Questions(s);
        if (questions.Count == 0) throw new InvalidOperationException("This legacy attempt has no grading snapshot. Reopen it before submission.");
        ValidateAnswers(questions, answers);
        var total = questions.Sum(q => q.Points);
        s.Score = total == 0 ? 0 : Math.Round(100.0 * questions.Where(q => answers.TryGetValue(q.Id, out var selected) && selected == q.CorrectIndex).Sum(q => q.Points) / total);
        s.SavedAnswersJson = JsonSerializer.Serialize(answers);
        s.SubmissionId = dto.SubmissionId == Guid.Empty ? Guid.NewGuid() : dto.SubmissionId;
        s.EndTime = timedOut ? deadline : DateTime.UtcNow;
        s.IsFinished = true;
        s.Status = timedOut ? "TimedOut" : "Completed";
        await db.SaveChangesAsync(); await tx.CommitAsync();
        return MapToDto(s);
    }

    public async Task<ExamSessionDto> HeartbeatAsync(Guid studentId, int sessionId, string clientId)
    {
        await using var tx=await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(1,{sessionId})");
        var s = await OwnSession(studentId, sessionId);
        if (s.ActiveClientId != clientId) throw new InvalidOperationException("The attempt was resumed in another client.");
        if (!s.IsFinished) { s.LastSeenAt = DateTime.UtcNow; await db.SaveChangesAsync(); }
        await tx.CommitAsync(); return MapToDto(s);
    }

    public async Task<ExamSessionDto> TerminateAsync(int sessionId)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(1,{sessionId})");
        var s = await Details.FirstOrDefaultAsync(s => s.Id == sessionId) ?? throw new KeyNotFoundException("Session not found.");
        if (!s.IsFinished) { s.IsFinished = true; s.Status = "TerminatedByProctor"; s.EndTime = DateTime.UtcNow; await db.SaveChangesAsync(); }
        await tx.CommitAsync(); return MapToDto(s);
    }

    public async Task<ExamSessionDto> ReviewSessionAsync(int sessionId, Guid reviewerId, string verdict, string notes) {
        if (verdict is not ("Accepted" or "Rejected" or "Inconclusive") || string.IsNullOrWhiteSpace(notes) || notes.Length > 2000) throw new ArgumentException("A verdict and its reason are required.");
        await using var tx = await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(1,{sessionId})");
        var s = await Details.FirstOrDefaultAsync(s => s.Id == sessionId) ?? throw new KeyNotFoundException("Session not found.");
        if (!s.IsFinished) throw new InvalidOperationException("Finish the attempt before reviewing it.");
        if (verdict == "Accepted" && s.Violations.Any(v => v.ReviewDecision != "Dismissed")) throw new InvalidOperationException("Dismiss every observation before accepting the attempt.");
        if (verdict == "Accepted" && s.EvidenceStatus != "Delivered") throw new InvalidOperationException("Complete evidence is required for acceptance. Use Inconclusive for missing recordings.");
        if (verdict == "Rejected" && !s.Violations.Any(v => v.ReviewDecision == "Confirmed")) throw new InvalidOperationException("Rejection requires a confirmed observation. Use Inconclusive for insufficient evidence.");
        var now = DateTime.UtcNow;
        var history = JsonSerializer.Deserialize<List<object>>(s.IntegrityHistoryJson ?? "[]") ?? new();
        history.Add(new { reviewerId, verdict, notes, at = now, evidenceStatus = s.EvidenceStatus });
        s.IntegrityHistoryJson = JsonSerializer.Serialize(history);
        s.IntegrityVerdict = verdict; s.IntegrityReviewedBy = reviewerId; s.IntegrityReviewedAt = now; s.IntegrityNotes = notes;
        await db.SaveChangesAsync(); await tx.CommitAsync(); return MapToDto(s);
    }

    private static ExamSessionDto MapToDto(ExamSession s) => new() {
        Id = s.Id, ActiveClientId = s.ActiveClientId, EvidenceClientId = s.EvidenceClientId, StudentId = s.StudentId, StudentName = s.Student?.FullName ?? "", ExamId = s.ExamId, ExamTitle = s.Exam?.Title ?? "",
        StartTime = s.StartTime, EndTime = s.EndTime, Score = s.Score, ScoreCalculated = s.IsFinished && (s.Status is "Completed" or "TimedOut"), PassingScoreAtStart = s.PassingScoreAtStart, IsFinished = s.IsFinished, ViolationCount = s.Violations.Count,
        PendingObservationCount = s.Violations.Count(v => v.ReviewDecision is "Pending" or "LegacyReviewed"), ConfirmedObservationCount = s.Violations.Count(v => v.ReviewDecision == "Confirmed"),
        DeadlineUtc = s.DeadlineUtc ?? s.StartTime.AddMinutes(s.Exam?.DurationMinutes ?? 0), ServerTimeUtc = DateTime.UtcNow,
        LastSeenAt = s.LastSeenAt, LastAnalysisAt = s.LastAnalysisAt, LastAudioAt = s.LastAudioAt, Status = s.IsFinished && s.Status == "InProgress" ? "Completed" : s.Status,
        IntegrityHistoryJson = s.IntegrityHistoryJson, IntegrityVerdict = s.IntegrityVerdict, IntegrityReviewedBy = s.IntegrityReviewedBy, IntegrityReviewedAt = s.IntegrityReviewedAt, IntegrityNotes = s.IntegrityVerdict == "UnderReview" ? null : s.IntegrityNotes,
        EvidenceStatus = s.EvidenceStatus, SavedAnswers = JsonSerializer.Deserialize<Dictionary<int,int>>(s.SavedAnswersJson) ?? new()
    };
}
