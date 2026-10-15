using BusinessLayer.Interfaces;
using BusinessLogicLayer.DTOs.ExamSession;
using DataAccessLayer.Data;
using Microsoft.EntityFrameworkCore;

namespace Core.Services;

public sealed class DeadlineWorker(IServiceScopeFactory scopes, ILogger<DeadlineWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var sessions = scope.ServiceProvider.GetRequiredService<IExamSessionService>();
                var cutoff = DateTime.UtcNow;
                var expired = await db.Sessions.AsNoTracking().Where(s => !s.IsFinished && s.DeadlineUtc < cutoff && s.ExamSnapshotJson != null)
                    .Select(s => new { s.Id, s.StudentId, s.ExamId }).Take(50).ToListAsync(stoppingToken);
                foreach (var s in expired)
                    await sessions.SubmitAsync(s.StudentId, s.ExamId, new SubmitAnswersDto { SessionId = s.Id, DeadlineSubmission = true });
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Deadline processing failed"); }
        }
    }
}
