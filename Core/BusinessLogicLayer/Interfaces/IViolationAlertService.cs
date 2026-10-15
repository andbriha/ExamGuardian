using BusinessLayer.DTOs.Violation;

namespace BusinessLayer.Interfaces
{
    public interface IViolationAlertService
    {
        Task<ViolationAlertDto?> GetByIdAsync(int violationId);
        Task<IEnumerable<ViolationAlertDto>> GetBySessionAsync(int sessionId);
        Task<IEnumerable<ViolationAlertDto>> GetUnreviewedAsync();
        Task<IEnumerable<ViolationAlertDto>> GetUnreviewedForSessionsAsync(List<int> ids);
        Task<ViolationAlertDto> CreateAsync(CreateViolationDto dto);
        Task ReviewAsync(int violationId, Guid reviewerId, string decision, string? notes);
    }
}