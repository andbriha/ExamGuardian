using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using DataAccessLayer.Data;
// BusinessLogicLayer/Services/ViolationAlertService.cs
using BusinessLayer.DTOs.Violation;
using BusinessLayer.Interfaces;
using DataAccessLayer.Entities;
using DataAccessLayer.Interfaces;
using DataAccessLayer.Repositories.Interfaces;

namespace BusinessLogicLayer.Services;

public class ViolationAlertService : IViolationAlertService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly AppDbContext _db;

    public ViolationAlertService(IUnitOfWork unitOfWork, AppDbContext db)
    {
        _unitOfWork = unitOfWork;
        _db = db;
    }

    public async Task<ViolationAlertDto?> GetByIdAsync(int violationId)
    {
        var repo = (IViolationAlertRepository)_unitOfWork.Repository<ViolationAlert>();
        var violation = await repo.GetByIdAsync(violationId);
        return violation is null ? null : MapToDto(violation);
    }

    public async Task<IEnumerable<ViolationAlertDto>> GetBySessionAsync(int sessionId)
    {
        var repo = (IViolationAlertRepository)
            _unitOfWork.Repository<ViolationAlert>();
        var violations = await repo.GetBySessionAsync(sessionId);
        return violations.Select(MapToDto);
    }

    public async Task<IEnumerable<ViolationAlertDto>> GetUnreviewedForSessionsAsync(List<int> ids) => (await _db.Violations.AsNoTracking().Where(v => ids.Contains(v.ExamSessionId) && (v.ReviewDecision == "Pending" || v.ReviewDecision == "LegacyReviewed")).OrderBy(v => v.Timestamp).ToListAsync()).Select(MapToDto);

    public async Task<IEnumerable<ViolationAlertDto>> GetUnreviewedAsync()
    {
        var repo = (IViolationAlertRepository)
            _unitOfWork.Repository<ViolationAlert>();
        var violations = await repo.GetUnreviewedAsync();
        return violations.Select(MapToDto);
    }

    public async Task<ViolationAlertDto> CreateAsync(CreateViolationDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.ViolationType) || dto.ViolationType.Length > 50 || !double.IsFinite(dto.Confidence) || dto.Confidence is < 0 or > 1 || dto.RelativeTimestampSeconds < 0 || dto.RelativeTimestampSeconds > 86400 || dto.Metadata?.Length > 16000)
            throw new ArgumentException("Invalid observation.");
        await using var transaction = await _db.Database.BeginTransactionAsync();
        await _db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(1,{dto.ExamSessionId})");
        if (dto.EventId != Guid.Empty && await _db.Violations.FirstOrDefaultAsync(v => v.EventId == dto.EventId) is { } existing)
        {
            if (existing.ExamSessionId != dto.ExamSessionId) throw new ArgumentException("Event id belongs to another session.");
            await transaction.CommitAsync(); return MapToDto(existing);
        }
        var sessionsRepo = (IExamSessionRepository)
            _unitOfWork.Repository<ExamSession>();
        var violationsRepo = (IViolationAlertRepository)
            _unitOfWork.Repository<ViolationAlert>();

        var session = await sessionsRepo.GetByIdAsync(dto.ExamSessionId)
            ?? throw new KeyNotFoundException(
                $"Сесію {dto.ExamSessionId} не знайдено");

        var violation = new ViolationAlert
        {
            ExamSessionId = dto.ExamSessionId,
            EventId = dto.EventId == Guid.Empty ? Guid.NewGuid() : dto.EventId,
            ViolationType = dto.ViolationType,
            Confidence = dto.Confidence,
            RelativeTimestampSeconds = dto.RelativeTimestampSeconds,
            EvidenceUrl = dto.EvidenceUrl,
            Metadata = dto.Metadata,
            Timestamp = DateTime.UtcNow,
            IsReviewed = false,
        };

        session.IntegrityVerdict = "UnderReview";
        await violationsRepo.AddAsync(violation);
        await _unitOfWork.CompleteAsync();
        await transaction.CommitAsync();

        return MapToDto(violation);
    }

    public async Task ReviewAsync(int violationId, Guid reviewerId, string decision, string? notes)
    {
        if (decision is not ("Confirmed" or "Dismissed") || notes?.Length > 2000) throw new ArgumentException("Invalid review decision.");
        var v = await _db.Violations.FindAsync(violationId) ?? throw new KeyNotFoundException("Observation not found.");
        await using var transaction = await _db.Database.BeginTransactionAsync();
        await _db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(1,{v.ExamSessionId})");
        await _db.Entry(v).ReloadAsync();
        var session = await _db.Sessions.FindAsync(v.ExamSessionId);
        if (session is not null) session.IntegrityVerdict = "UnderReview";
        var history = JsonSerializer.Deserialize<List<object>>(v.ReviewHistoryJson ?? "[]") ?? new();
        var now = DateTime.UtcNow;
        history.Add(new { reviewerId, decision, notes, at = now });
        v.ReviewDecision = decision; v.ReviewedBy = reviewerId; v.ReviewedAt = now; v.ReviewNotes = notes;
        v.ReviewHistoryJson = JsonSerializer.Serialize(history); v.IsReviewed = true;
        await _db.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    private static ViolationAlertDto MapToDto(ViolationAlert va) => new()
    {
        Id = va.Id,
        ExamSessionId = va.ExamSessionId,
        ViolationType = va.ViolationType,
        Confidence = va.Confidence,
        RelativeTimestampSeconds = va.RelativeTimestampSeconds,
        EvidenceUrl = va.EvidenceUrl,
        Metadata = va.Metadata,
        Timestamp = va.Timestamp,
        IsReviewed = va.IsReviewed,
        ReviewDecision = va.ReviewDecision, ReviewedBy = va.ReviewedBy, ReviewedAt = va.ReviewedAt, ReviewNotes = va.ReviewNotes, EventId = va.EventId,
    };
}