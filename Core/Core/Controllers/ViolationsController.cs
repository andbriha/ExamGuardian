// Core/Controllers/ViolationsController.cs
using BusinessLayer.DTOs.Violation;
using BusinessLayer.Interfaces;
using BusinessLogicLayer.Constants;
using Core.Attributes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Core.Controllers;   // ← було DataAccessLayer.Models

[ApiController]
[Route("api/[controller]")]
public class ViolationsController : ControllerBase
{
    private readonly IViolationAlertService _violationService;

    public ViolationsController(IViolationAlertService violationService)
    {
        _violationService = violationService;
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
    [AllowAnonymous]
    public async Task<IActionResult> ReportClientViolation([FromBody] CreateViolationDto dto)
    {
        try
        {
            var violation = await _violationService.CreateAsync(dto);
            return Ok(violation);
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

    [HttpGet("by-session/{sessionId:int}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetBySession(int sessionId)
    {
        var violations = await _violationService.GetBySessionAsync(sessionId);
        return Ok(violations);
    }

    [HttpGet("{id:int}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetById(int id)
    {
        var violation = await _violationService.GetByIdAsync(id);
        return violation is null ? NotFound() : Ok(violation);
    }

    [HttpGet("unreviewed")]
    [AllowAnonymous]
    public async Task<IActionResult> GetUnreviewed()
    {
        var violations = await _violationService.GetUnreviewedAsync();
        return Ok(violations);
    }

    [HttpPatch("{id:int}/review")]
    [AllowAnonymous]
    public async Task<IActionResult> MarkAsReviewed(int id)
    {
        await _violationService.MarkAsReviewedAsync(id);
        return NoContent();
    }
}