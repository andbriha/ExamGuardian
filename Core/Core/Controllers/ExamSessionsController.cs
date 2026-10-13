using BusinessLayer.Interfaces;
using BusinessLogicLayer.DTOs.ExamSession;
using BusinessLogicLayer.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Core.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ExamSessionsController : ControllerBase
{
    private readonly IExamSessionService _sessionService;

    public ExamSessionsController(IExamSessionService sessionService)
    {
        _sessionService = sessionService;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAll()
    {
        var sessions = await _sessionService.GetAllAsync();
        return Ok(sessions);
    }

    [HttpGet("{id:int}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetById(int id)
    {
        try
        {
            var session = await _sessionService.GetByIdAsync(id);
            return session is null ? NotFound() : Ok(session);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpGet("by-student/{studentId:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetByStudent(Guid studentId)
    {
        var sessions = await _sessionService.GetByStudentAsync(studentId);
        return Ok(sessions);
    }

    [HttpGet("by-exam/{examId:int}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetByExam(int examId)
    {
        var sessions = await _sessionService.GetByExamAsync(examId);
        return Ok(sessions);
    }

    [HttpPost("start")]
    [Authorize(Roles = SystemRoles.Student)]
    public async Task<IActionResult> Start([FromBody] StartSessionDto dto)
    {
        if (dto.StudentId == Guid.Empty)
        {
            var userIdStr = User.FindFirst("id")?.Value
                ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;

            if (Guid.TryParse(userIdStr, out var studentGuid))
            {
                dto.StudentId = studentGuid;
            }
        }

        try
        {
            var session = await _sessionService.StartSessionAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = session.Id }, session);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPatch("{id:int}/finish")]
    [Authorize(Roles = SystemRoles.Student)]
    public async Task<IActionResult> Finish(int id, [FromQuery] double score)
    {
        try
        {
            var session = await _sessionService.FinishSessionAsync(id, score);
            return Ok(session);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }
}