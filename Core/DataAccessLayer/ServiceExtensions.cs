using DataAccessLayer.Data;
using DataAccessLayer.Interfaces;
using DataAccessLayer.Repositories;
using DataAccessLayer.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DataAccessLayer;

public static class ServiceExtensions
{
    public static IServiceCollection AddDataAccessDependencies(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

        return services
            .AddCustomDbContext(connectionString)
            .AddRepositories();
    }

    private static IServiceCollection AddCustomDbContext(
        this IServiceCollection services,
        string connectionString)
    {
        return services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString)
                   .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning)));
    }

    private static IServiceCollection AddRepositories(
        this IServiceCollection services)
    {
        return services
            .AddScoped<IUnitOfWork, UnitOfWork>()
            .AddScoped(typeof(IRepository<>), typeof(GenericRepository<>))
            .AddScoped<IUserRepository, UserRepository>()

            .AddScoped<IExamRepository, ExamRepository>()
            .AddScoped<IExamSessionRepository, ExamSessionRepository>()
            .AddScoped<IViolationAlertRepository, ViolationAlertRepository>();
    }
}