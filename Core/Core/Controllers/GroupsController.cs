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
public class GroupsController(AppDbContext db) : ControllerBase
{
    // Include legacy names so existing students and exams remain selectable.
    private IQueryable<string> Names() => db.StudentGroups.Select(g => g.Name)
        .Union(db.Users.Where(u => u.Role == SystemRoles.Student && u.Group != null && u.Group != "").Select(u => u.Group!))
        .Union(db.Exams.Where(e => e.AllowedGroup != null && e.AllowedGroup != "").Select(e => e.AllowedGroup!));

    [HttpGet]
    public async Task<IActionResult> GetAll() => Ok(await Names().OrderBy(n => n).ToListAsync());

    [HttpPost]
    public async Task<IActionResult> Create(GroupName dto)
    {
        var name = dto.Name?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > 100 || name.Any(char.IsControl))
            return BadRequest(new { message = "Назва групи має містити від 1 до 100 символів." });
        if (await Names().AnyAsync(n => n.ToUpper() == name.ToUpper()))
            return Conflict(new { message = "Така група вже існує." });
        db.StudentGroups.Add(new StudentGroup { Name = name });
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
