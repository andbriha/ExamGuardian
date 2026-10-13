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

    public ViolationAlertService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
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

    public async Task<IEnumerable<ViolationAlertDto>> GetUnreviewedAsync()
    {
        var repo = (IViolationAlertRepository)
            _unitOfWork.Repository<ViolationAlert>();
        var violations = await repo.GetUnreviewedAsync();
        return violations.Select(MapToDto);
    }

    public async Task<ViolationAlertDto> CreateAsync(CreateViolationDto dto)
    {
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
            ViolationType = dto.ViolationType,
            Confidence = dto.Confidence,
            RelativeTimestampSeconds = dto.RelativeTimestampSeconds,
            EvidenceUrl = dto.EvidenceUrl,
            Metadata = dto.Metadata,
            Timestamp = DateTime.UtcNow,
            IsReviewed = false,
        };

        await violationsRepo.AddAsync(violation);
        await _unitOfWork.CompleteAsync();

        return MapToDto(violation);
    }

    public async Task MarkAsReviewedAsync(int violationId)
    {
        var repo = (IViolationAlertRepository)
            _unitOfWork.Repository<ViolationAlert>();
        await repo.MarkAsReviewedAsync(violationId);
        await _unitOfWork.CompleteAsync();
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
    };
}