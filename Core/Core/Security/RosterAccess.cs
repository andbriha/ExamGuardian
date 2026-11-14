using DataAccessLayer.Data;
using DataAccessLayer.Entities;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
namespace Core.Security;
public sealed class RosterAccess(AppDbContext db) {
    public Task<bool> IsAdmin(Guid id) => db.Users.AnyAsync(u => u.Id == id && u.Role == "Examiner" && u.CanManageRoster);
    public async Task<bool> CanGroup(Guid id, string? group) => await IsAdmin(id) || await db.ExaminerGroupGrants.AnyAsync(g => g.ExaminerId == id && g.Group == (group ?? ""));
    public async Task<bool> CanCreateInGroup(Guid id, string group) => await CanGroup(id, group) ||
        (!await db.StudentGroups.AnyAsync(g => g.Name == group) && !await db.Users.AnyAsync(u => u.Group == group));
    public async Task<bool> CanStudent(ClaimsPrincipal user, Guid student) {
        var row = await db.Users.AsNoTracking().Where(u => u.Id == student && u.Role == "Student").Select(u => new {u.Group}).SingleOrDefaultAsync();
        return row is not null && await CanGroup(SessionAccess.UserId(user), row.Group);
    }
    public async Task<string[]?> Groups(Guid id) => await IsAdmin(id) ? null : await db.ExaminerGroupGrants.Where(g => g.ExaminerId == id).Select(g => g.Group).ToArrayAsync();
    public async Task GrantCreator(Guid id, string group) {
        if (!await IsAdmin(id) && !await db.ExaminerGroupGrants.AnyAsync(g => g.ExaminerId == id && g.Group == group))
            db.ExaminerGroupGrants.Add(new ExaminerGroupGrant { ExaminerId = id, Group = group });
    }
}
