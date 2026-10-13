using BusinessLayer.DTOs.Violation;

namespace BusinessLayer.Interfaces
{
    public interface IViolationAlertService
    {
        Task<ViolationAlertDto?> GetByIdAsync(int violationId);
        Task<IEnumerable<ViolationAlertDto>> GetBySessionAsync(int sessionId);
        Task<IEnumerable<ViolationAlertDto>> GetUnreviewedAsync();
        Task<ViolationAlertDto> CreateAsync(CreateViolationDto dto);
        Task MarkAsReviewedAsync(int violationId);
    }
}