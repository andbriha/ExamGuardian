// Core/Controllers/ViolationsController.cs
using BusinessLayer.DTOs.Violation;
using BusinessLayer.Interfaces;
using BusinessLogicLayer.Constants;
using Core.Attributes;
using Core.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Core.Controllers;   // ← було DataAccessLayer.Models

[ApiController]
[Route("api/[controller]")]
public class ViolationsController : ControllerBase
{
    private readonly IViolationAlertService _violationService;
    private readonly SessionAccess _access;

    public ViolationsController(IViolationAlertService violationService, SessionAccess access)
    {
        _violationService = violationService;
        _access = access;
    }

    // FastAPI надсилає алерт — захищено API-ключем (не JWT)
    [HttpPost]
    [ApiKey]
    public async Task<IActionResult> Create([FromBody] CreateViolationDto dto)
    {
        try
        {
            var violation = await _violationService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetBySession),
                new { sessionId = violation.ExamSessionId }, violation);
        }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPost("report")]
    [Authorize]
    public async Task<IActionResult> ReportClientViolation([FromBody] CreateViolationDto dto)
    {
        if (dto.ExamSessionId <= 0 || string.IsNullOrWhiteSpace(dto.ViolationType))
        {
            return BadRequest(new { message = "ExamSessionId and ViolationType are required." });
        }

        if (!User.IsInRole(SystemRoles.Student) || !await _access.OwnsSessionAsync(User, dto.ExamSessionId)) return Forbid();

        if (!await _access.CanCommandAsync(User,dto.ExamSessionId,Request.Headers["X-Session-Token"]) && !await _access.CanCommandAsync(User,dto.ExamSessionId,Request.Headers["X-Evidence-Token"],true)) return Forbid();
        dto.EvidenceUrl = null;
        try
        {
            var violation = await _violationService.CreateAsync(dto);
            return Ok(violation);
        }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("by-session/{sessionId:int}")]
    [Authorize]
    public async Task<IActionResult> GetBySession(int sessionId)
    {
        if (!await _access.CanReadAsync(User, sessionId)) return NotFound();
        var violations = await _violationService.GetBySessionAsync(sessionId);
        return Ok(violations);
    }

    [HttpGet("{id:int}")]
    [Authorize]
    public async Task<IActionResult> GetById(int id)
    {
        var violation = await _violationService.GetByIdAsync(id);
        return violation is null || !await _access.CanReadAsync(User, violation.ExamSessionId) ? NotFound() : Ok(violation);
    }

    [HttpGet("unreviewed")]
    [Authorize]
    public async Task<IActionResult> GetUnreviewed()
    {
        var allowed = await _access.VisibleSessions(User).Select(s => s.Id).ToListAsync();
        return Ok(await _violationService.GetUnreviewedForSessionsAsync(allowed));
    }

    [HttpGet("evidence/{eventId:guid}"), Authorize]
    public async Task<IActionResult> Evidence(Guid eventId, [FromServices] DataAccessLayer.Data.AppDbContext db)
    {
        var v = await db.Violations.FirstOrDefaultAsync(v => v.EventId == eventId);
        if (v is null || !await _access.CanReadEvidenceAsync(User,v.ExamSessionId,Request.Headers["X-Evidence-Token"])) return NotFound();
        var root = Environment.GetEnvironmentVariable("INFERENCE_IMAGES_PATH");
        if (string.IsNullOrWhiteSpace(root)) return NotFound();
        var path = Path.Combine(root, "evidence", eventId + ".jpg");
        return System.IO.File.Exists(path) ? PhysicalFile(path, "image/jpeg") : NotFound();
    }

    [HttpPatch("{id:int}/review")]
    [Authorize]
    public async Task<IActionResult> MarkAsReviewed(int id, ReviewRequest request)
    {
        var violation = await _violationService.GetByIdAsync(id);
        if (!User.IsInRole(SystemRoles.Examiner)) return Forbid();
        if (violation is null || !await _access.CanReadAsync(User, violation.ExamSessionId)) return NotFound();
        try { await _violationService.ReviewAsync(id, SessionAccess.UserId(User), request.Decision, request.Notes); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        return NoContent();
    }
}
public record ReviewRequest(string Decision, string? Notes);
