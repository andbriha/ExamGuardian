using System.Security.Claims;
using DataAccessLayer.Data;
using DataAccessLayer.Entities;
using Microsoft.EntityFrameworkCore;
using BusinessLogicLayer.Constants;

namespace Core.Security;

public sealed class SessionAccess(AppDbContext db, AttemptTokens tokens)
{
    public static Guid UserId(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirst("id")?.Value, out var id) ? id : Guid.Empty;

    public IQueryable<ExamSession> VisibleSessions(ClaimsPrincipal user)
    {
        var id = UserId(user);
        if (id == Guid.Empty) return db.Sessions.Where(_ => false);
        return user.IsInRole(SystemRoles.Examiner)
            ? db.Sessions.Where(s => s.Exam.CreatorId == id)
            : db.Sessions.Where(s => s.StudentId == id);
    }

    public Task<bool> CanReadAsync(ClaimsPrincipal user, int sessionId) =>
        VisibleSessions(user).AnyAsync(s => s.Id == sessionId);

    public Task<bool> OwnsExamAsync(ClaimsPrincipal user, int examId) =>
        db.Exams.AnyAsync(e => e.Id == examId && e.CreatorId == UserId(user));

    public Task<bool> OwnsSessionAsync(ClaimsPrincipal user, int sessionId) =>
        db.Sessions.AnyAsync(s => s.Id == sessionId && s.StudentId == UserId(user));

    public async Task<List<int>> AvailableExamsAsync(ClaimsPrincipal user) {
        var id = UserId(user);
        var group = await db.Users.Where(u => u.Id == id).Select(u => u.Group).FirstOrDefaultAsync();
        // Catalog includes upcoming exams; StartAsync still enforces the admission window.
        var now = DateTime.UtcNow;
        return await db.Exams.Where(e => (e.AllowedGroup == null || e.AllowedGroup == "" || e.AllowedGroup == group)
            && (e.ScheduledStartTime ?? e.CreatedAt).AddMinutes(e.DurationMinutes) > now
            && (e.Sessions.Any(s => s.StudentId == id && !s.IsFinished) || e.Sessions.Count(s => s.StudentId == id) < e.MaxAttempts))
            .Select(e => e.Id).ToListAsync();
    }

    public async Task<string?> CommandClientIdAsync(ClaimsPrincipal user,int sessionId,string? token) {
        var session=await db.Sessions.AsNoTracking().FirstOrDefaultAsync(s => s.Id==sessionId && s.StudentId==UserId(user));
        return session is not null && tokens.Validate(token,session,false) ? session.ActiveClientId : null;
    }
    public async Task<bool> CanCommandAsync(ClaimsPrincipal user, int sessionId, string? token, bool evidence = false) {
        var session = await db.Sessions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == sessionId && s.StudentId == UserId(user));
        return session is not null && tokens.Validate(token,session,evidence);
    }
    public async Task<bool> CanReadEvidenceAsync(ClaimsPrincipal user,int sessionId,string? token) =>
        user.IsInRole(SystemRoles.Examiner) ? await CanReadAsync(user,sessionId) : await CanCommandAsync(user,sessionId,token,true);
}
