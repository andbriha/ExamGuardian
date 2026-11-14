using DataAccessLayer.Entities;
using DataAccessLayer.Interfaces;

namespace DataAccessLayer.Repositories.Interfaces
{
    public interface IViolationAlertRepository : IRepository<ViolationAlert>
    {
        Task<IEnumerable<ViolationAlert>> GetBySessionAsync(int sessionId);
        Task<IEnumerable<ViolationAlert>> GetUnreviewedAsync();
        Task MarkAsReviewedAsync(int violationId);
    }
}