using DataAccessLayer.Data;
using DataAccessLayer.Entities;
using DataAccessLayer.Interfaces;
using DataAccessLayer.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DataAccessLayer.Repositories;

public class ViolationAlertRepository : GenericRepository<ViolationAlert>, IViolationAlertRepository
{
    public ViolationAlertRepository(AppDbContext context) : base(context) { }

    public async Task<IEnumerable<ViolationAlert>> GetBySessionAsync(int sessionId) =>
        await _dbSet
            .Where(va => va.ExamSessionId == sessionId)
            .OrderBy(va => va.Timestamp)
            .ToListAsync();

    public async Task<IEnumerable<ViolationAlert>> GetUnreviewedAsync() =>
        await _dbSet
            .Include(va => va.Session)
                .ThenInclude(es => es.Student)
            .Where(va => !va.IsReviewed)
            .OrderBy(va => va.Timestamp)
            .ToListAsync();

    public async Task MarkAsReviewedAsync(int violationId)
    {
        var violation = await _dbSet.FindAsync(violationId);
        if (violation is null) return;
        violation.IsReviewed = true;
    }
}