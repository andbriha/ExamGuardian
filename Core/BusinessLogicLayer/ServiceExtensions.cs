using BusinessLayer.Interfaces;
using BusinessLogicLayer.Interfaces;
using BusinessLogicLayer.Services;
using Microsoft.Extensions.DependencyInjection;

namespace BusinessLogicLayer;

public static class ServiceExtensions
{
    public static IServiceCollection AddBusinessDependencies(
        this IServiceCollection services)
    {
        return services
            .AddScoped<IAuthService, AuthService>()
            .AddScoped<IUserService, UserService>()

            .AddScoped<IExamService, ExamService>()
            .AddScoped<IExamSessionService, ExamSessionService>()
            .AddScoped<IViolationAlertService, ViolationAlertService>();
    }
}
