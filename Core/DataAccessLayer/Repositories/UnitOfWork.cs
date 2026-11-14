using DataAccessLayer.Data;
using DataAccessLayer.Entities;
using DataAccessLayer.Interfaces;
using DataAccessLayer.Repositories.Interfaces;

namespace DataAccessLayer.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;
    private Dictionary<Type, object>? _repositories;

    public UnitOfWork(AppDbContext context)
    {
        _context = context;
    }

    public IRepository<T> Repository<T>() where T : class
    {
        _repositories ??= new Dictionary<Type, object>();
        var type = typeof(T);

        if (!_repositories.ContainsKey(type))
        {
            object instance = type switch
            {
                _ when type == typeof(User) =>
                    new UserRepository(_context),
                _ when type == typeof(Exam) =>
                    new ExamRepository(_context),
                _ when type == typeof(ExamSession) =>
                    new ExamSessionRepository(_context),
                _ when type == typeof(ViolationAlert) =>
                    new ViolationAlertRepository(_context),
                _ =>
                    Activator.CreateInstance(
                        typeof(GenericRepository<>)
                            .MakeGenericType(type), _context)!,
            };

            _repositories.Add(type, instance);
        }

        return (IRepository<T>)_repositories[type];
    }

    public async Task<int> CompleteAsync()
    {
        return await _context.SaveChangesAsync();
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }
}