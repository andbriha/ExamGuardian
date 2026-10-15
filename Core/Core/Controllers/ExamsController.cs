using BusinessLayer.DTOs.Exam;
using BusinessLayer.Interfaces;
using BusinessLogicLayer.Constants;
using BusinessLogicLayer.DTOs.ExamSession;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Core.Security;

namespace Core.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ExamsController : ControllerBase
{
    private readonly IExamService _examService;
    private readonly SessionAccess _access;
    private readonly IExamSessionService _sessionService;

    public ExamsController(
        IExamService examService,
        IExamSessionService sessionService, SessionAccess access)
    {
        _examService = examService;
        _sessionService = sessionService;
        _access = access;
    }

    // Студент і викладач можуть отримати список доступних іспитів
    [HttpGet]
    [Authorize(Roles = $"{SystemRoles.Examiner},{SystemRoles.Student}")]
    public async Task<IActionResult> GetAll()
    {
        var exams = await _examService.GetAvailableAsync();
        if (User.IsInRole(SystemRoles.Examiner)) return Ok(exams.Where(e => e.CreatorId == SessionAccess.UserId(User)));
        var available = await _access.AvailableExamsAsync(User);
        return Ok(exams.Where(e => available.Contains(e.Id)));
    }

    // Отримати конкретний іспит з питаннями
    [HttpGet("{id:int}")]
    [Authorize(Roles = $"{SystemRoles.Examiner},{SystemRoles.Student}")]
    public async Task<IActionResult> GetById(int id)
    {
        if (User.IsInRole(SystemRoles.Examiner) && !await _access.OwnsExamAsync(User, id)) return NotFound();
        if (User.IsInRole(SystemRoles.Student)) {
            var own = (await _sessionService.GetByStudentAsync(SessionAccess.UserId(User))).Where(s => s.ExamId == id).OrderByDescending(s => s.StartTime).FirstOrDefault();
            if (own is null || !await _access.CanCommandAsync(User,own.Id,Request.Headers["X-Session-Token"])) return NotFound();
        }
        var exam = User.IsInRole(SystemRoles.Student) ? await _sessionService.GetExamForSessionAsync(SessionAccess.UserId(User),id) : await _examService.GetForStudentAsync(id);
        return exam is null ? NotFound() : Ok(exam);
    }

    // Створення нового іспиту викладачем
    [HttpPost]
    [Authorize(Roles = SystemRoles.Examiner)]
    public async Task<IActionResult> Create([FromBody] CreateExamDto dto)
    {
        dto.CreatorId = SessionAccess.UserId(User);

        try
        {
            var created = await _examService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // Видалення іспиту викладачем
    [HttpDelete("{id:int}")]
    [Authorize(Roles = SystemRoles.Examiner)]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            if (!await _access.OwnsExamAsync(User, id)) return NotFound();
            await _examService.DeleteAsync(id);
            return NoContent();
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException) {
            return Conflict(new { message = "Історія спроб зберігається. Запис із пов'язаними спробами не можна видалити." });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    // Студент здає відповіді — рахуємо бал і завершуємо сесію
    [HttpPost("{id:int}/submit")]
    [Authorize(Roles = SystemRoles.Student)]
    public async Task<IActionResult> Submit(
        int id, [FromBody] SubmitAnswersDto dto)
    {
        try
        {
            if (!await _access.OwnsSessionAsync(User, dto.SessionId)) return NotFound();
            dto.ClientId=await _access.CommandClientIdAsync(User,dto.SessionId,Request.Headers["X-Session-Token"]);
            if (dto.ClientId is null) return Forbid();
            var existing = await _sessionService.GetByIdAsync(dto.SessionId);
            if (existing is null || existing.ExamId != id) return BadRequest(new { message = "Session does not belong to this exam." });
            if (existing.IsFinished) return Ok(new { score = existing.Score, session = existing });
            var session = await _sessionService.SubmitAsync(SessionAccess.UserId(User), id, dto);
            var score = session.Score;
            return Ok(new { score, session });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}