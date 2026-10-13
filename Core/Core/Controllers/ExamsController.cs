using BusinessLayer.DTOs.Exam;
using BusinessLayer.Interfaces;
using BusinessLogicLayer.Constants;
using BusinessLogicLayer.DTOs.ExamSession;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Core.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ExamsController : ControllerBase
{
    private readonly IExamService _examService;
    private readonly IExamSessionService _sessionService;

    public ExamsController(
        IExamService examService,
        IExamSessionService sessionService)
    {
        _examService = examService;
        _sessionService = sessionService;
    }

    // Студент і викладач можуть отримати список доступних іспитів
    [HttpGet]
    [Authorize(Roles = $"{SystemRoles.Examiner},{SystemRoles.Student}")]
    public async Task<IActionResult> GetAll()
    {
        var exams = await _examService.GetAvailableAsync();
        return Ok(exams);
    }

    // Отримати конкретний іспит з питаннями
    [HttpGet("{id:int}")]
    [Authorize(Roles = $"{SystemRoles.Examiner},{SystemRoles.Student}")]
    public async Task<IActionResult> GetById(int id)
    {
        var exam = await _examService.GetForStudentAsync(id);
        return exam is null ? NotFound() : Ok(exam);
    }

    // Створення нового іспиту викладачем
    [HttpPost]
    [Authorize(Roles = SystemRoles.Examiner)]
    public async Task<IActionResult> Create([FromBody] CreateExamDto dto)
    {
        // Якщо CreatorId не вказано або порожній, беремо з клеймів токена
        if (dto.CreatorId == Guid.Empty)
        {
            var userIdStr = User.FindFirst("id")?.Value
                ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;

            if (Guid.TryParse(userIdStr, out var creatorGuid))
            {
                dto.CreatorId = creatorGuid;
            }
        }

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
            await _examService.DeleteAsync(id);
            return NoContent();
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
            if (dto.SessionId <= 0)
            {
                var userIdStr = User.FindFirst("id")?.Value
                    ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                    ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;

                if (Guid.TryParse(userIdStr, out var studentGuid))
                {
                    var sessions = await _sessionService.GetByStudentAsync(studentGuid);
                    var active = sessions.FirstOrDefault(s => s.ExamId == id && !s.IsFinished)
                        ?? sessions.FirstOrDefault(s => s.ExamId == id);
                    if (active != null)
                    {
                        dto.SessionId = active.Id;
                    }
                }
            }

            var score = await _examService
                .CalculateScoreAsync(id, dto.Answers);
            var session = await _sessionService
                .FinishSessionAsync(dto.SessionId, score);
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