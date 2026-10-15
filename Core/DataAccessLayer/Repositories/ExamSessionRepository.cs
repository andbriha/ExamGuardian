using DataAccessLayer.Data;
using DataAccessLayer.Entities;
using DataAccessLayer.Interfaces;
using DataAccessLayer.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DataAccessLayer.Repositories;

public class ExamSessionRepository : GenericRepository<ExamSession>, IExamSessionRepository
{
    public ExamSessionRepository(AppDbContext context) : base(context) { }

    public async Task<IEnumerable<ExamSession>> GetAllWithDetailsAsync() =>
        await _dbSet
            .Include(es => es.Student)
            .Include(es => es.Exam)
            .Include(es => es.Violations)
            .OrderByDescending(es => es.StartTime)
            .ToListAsync();

    public async Task<ExamSession?> GetWithDetailsAsync(int sessionId) =>
        await _dbSet
            .Include(es => es.Student)
            .Include(es => es.Exam)
            .Include(es => es.Violations)
            .FirstOrDefaultAsync(es => es.Id == sessionId);

    public async Task<IEnumerable<ExamSession>> GetByStudentAsync(Guid studentId) =>
        await _dbSet
            .Include(es => es.Exam)
            .Include(es => es.Student)
            .Include(es => es.Violations)
            .Where(es => es.StudentId == studentId)
            .OrderByDescending(es => es.StartTime)
            .ToListAsync();

    public async Task<IEnumerable<ExamSession>> GetByExamAsync(int examId) =>
        await _dbSet
            .Include(es => es.Student)
            .Include(es => es.Exam)
            .Include(es => es.Violations)
            .Where(es => es.ExamId == examId)
            .OrderByDescending(es => es.StartTime)
            .ToListAsync();

    public async Task<ExamSession?> GetActiveSessionAsync(Guid studentId, int examId) =>
        await _dbSet
            .Include(es => es.Exam).Include(es => es.Student).Include(es => es.Violations)
            .FirstOrDefaultAsync(es =>
                es.StudentId == studentId &&
                es.ExamId == examId &&
                es.IsFinished == false);
}