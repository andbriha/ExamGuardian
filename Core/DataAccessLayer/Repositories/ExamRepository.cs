using DataAccessLayer.Data;
using DataAccessLayer.Entities;
using DataAccessLayer.Interfaces;
using DataAccessLayer.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DataAccessLayer.Repositories;

public class ExamRepository : GenericRepository<Exam>, IExamRepository
{
    public ExamRepository(AppDbContext context) : base(context) { }

    public async Task<IEnumerable<Exam>> GetByCreatorAsync(Guid creatorId) =>
        await _dbSet
            .Where(e => e.CreatorId == creatorId)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync();

    public async Task<Exam?> GetWithSessionsAsync(int examId) =>
        await _dbSet
            .Include(e => e.Sessions)
                .ThenInclude(es => es.Student)
            .FirstOrDefaultAsync(e => e.Id == examId);

    public async Task<Exam?> GetWithQuestionsAsync(int examId) =>
    await _dbSet
        .Include(e => e.Questions)
        .ThenInclude(q => q.Options)
        .FirstOrDefaultAsync(e => e.Id == examId);
}