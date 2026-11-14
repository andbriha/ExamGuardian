using DataAccessLayer.Interfaces;
using DataAccessLayer.Repositories.Interfaces;

namespace DataAccessLayer.Interfaces;

public interface IUnitOfWork : IDisposable
{
    IRepository<T> Repository<T>() where T : class;
    Task<int> CompleteAsync();
}