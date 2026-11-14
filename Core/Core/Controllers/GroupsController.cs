using Core.Security;
using BusinessLogicLayer.Constants;
using DataAccessLayer.Data;
using DataAccessLayer.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Core.Controllers;

[ApiController]
[Route("api/groups")]
[Authorize(Roles = SystemRoles.Examiner)]
public class GroupsController(AppDbContext db, RosterAccess access) : ControllerBase
{
    // Include legacy names so existing students and exams remain selectable.
    private IQueryable<string> Names() => db.StudentGroups.Select(g => g.Name)
        .Union(db.Users.Where(u => u.Role == SystemRoles.Student && u.Group != null && u.Group != "").Select(u => u.Group!))
        .Union(db.Exams.Where(e => e.AllowedGroup != null && e.AllowedGroup != "").Select(e => e.AllowedGroup!));

    [HttpGet]
    public async Task<IActionResult> GetAll() {
        var groups = await access.Groups(SessionAccess.UserId(User));
        return Ok(await Names().Where(n => groups == null || groups.Contains(n)).OrderBy(n => n).ToListAsync());
    }
    [HttpGet("{group}/access")]
    public async Task<IActionResult> Access(string group) {
        if (!await access.IsAdmin(SessionAccess.UserId(User))) return Forbid();
        if (!await Names().AnyAsync(n => n == group)) return NotFound();
        var assigned = await db.ExaminerGroupGrants.Where(g => g.Group == group).Select(g => g.ExaminerId).ToArrayAsync();
        return Ok(await db.Users.Where(u => u.Role == SystemRoles.Examiner).OrderBy(u => u.FullName)
            .Select(u => new {u.Id,u.FullName,administrator=u.CanManageRoster,assigned=assigned.Contains(u.Id)}).ToListAsync());
    }
    [HttpPut("{group}/examiners/{examinerId:guid}")]
    public async Task<IActionResult> Grant(string group, Guid examinerId) {
        if (!await access.IsAdmin(SessionAccess.UserId(User))) return Forbid();
        if (!await Names().AnyAsync(n => n == group) || !await db.Users.AnyAsync(u => u.Id == examinerId && u.Role == SystemRoles.Examiner)) return NotFound();
        if (!await db.ExaminerGroupGrants.AnyAsync(g => g.ExaminerId == examinerId && g.Group == group)) {
            db.ExaminerGroupGrants.Add(new ExaminerGroupGrant {ExaminerId = examinerId, Group = group}); await db.SaveChangesAsync();
        }
        return NoContent();
    }
    [HttpDelete("{group}/examiners/{examinerId:guid}")]
    public async Task<IActionResult> Revoke(string group, Guid examinerId) {
        if (!await access.IsAdmin(SessionAccess.UserId(User))) return Forbid();
        await db.ExaminerGroupGrants.Where(g => g.ExaminerId == examinerId && g.Group == group).ExecuteDeleteAsync(); return NoContent();
    }

    [HttpPost]
    public async Task<IActionResult> Create(GroupName dto)
    {
        var name = dto.Name?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > 100 || name.Any(char.IsControl))
            return BadRequest(new { message = "Назва групи має містити від 1 до 100 символів." });
        if (await Names().AnyAsync(n => n.ToUpper() == name.ToUpper()))
            return Conflict(new { message = "Така група вже існує." });
        db.StudentGroups.Add(new StudentGroup { Name = name });
        await access.GrantCreator(SessionAccess.UserId(User), name);
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: "23505" })
        { return Conflict(new { message = "Така група вже існує." }); }
        return StatusCode(201, new { name });
    }

    [HttpPut("students/{id:guid}")]
    public async Task<IActionResult> Assign(Guid id, GroupName dto)
    {
        var name = dto.Name?.Trim();
        if (string.IsNullOrEmpty(name) || !await Names().AnyAsync(n => n == name))
            return BadRequest(new { message = "Оберіть наявну групу." });
        if (!await access.CanStudent(User, id) || !await access.CanGroup(SessionAccess.UserId(User), name)) return Forbid();
        var student = await db.Users.FirstOrDefaultAsync(u => u.Id == id && u.Role == SystemRoles.Student);
        if (student is null) return NotFound();
        if (await db.Sessions.AnyAsync(s => s.StudentId == id && !s.IsFinished))
            return Conflict(new { message = "Завершіть активну спробу перед зміною групи." });
        student.Group = name;
        await db.SaveChangesAsync();
        return NoContent();
    }
}

public record GroupName(string? Name);
