using BusinessLayer.Interfaces;
using BusinessLogicLayer.DTOs.ExamSession;
using BusinessLogicLayer.Constants;
using Core.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Core.Controllers;

[ApiController, Route("api/[controller]"), Authorize]
public class ExamSessionsController(IExamSessionService sessions, SessionAccess access, AdmissionValidator admission, AttemptTokens tokens) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var ids = await access.VisibleSessions(User).Select(s => s.Id).ToListAsync();
        return Ok(await sessions.GetByIdsAsync(ids));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        if (!await access.CanReadAsync(User, id)) return NotFound();
        return Ok(await sessions.GetByIdAsync(id));
    }

    [HttpGet("by-student/{studentId:guid}")]
    public async Task<IActionResult> GetByStudent(Guid studentId)
    {
        var ids = await access.VisibleSessions(User).Where(s => s.StudentId == studentId).Select(s => s.Id).ToListAsync();
        return Ok(await sessions.GetByIdsAsync(ids));
    }

    [HttpGet("by-exam/{examId:int}")]
    public async Task<IActionResult> GetByExam(int examId)
    {
        var ids = await access.VisibleSessions(User).Where(s => s.ExamId == examId).Select(s => s.Id).ToListAsync();
        return Ok(await sessions.GetByIdsAsync(ids));
    }

    [HttpPost("start"), Authorize(Roles = SystemRoles.Student)]
    public async Task<IActionResult> Start(StartSessionDto dto)
    {
        dto.StudentId = SessionAccess.UserId(User);
        if (dto.StudentId == Guid.Empty) return Unauthorized();
        var proof = admission.ValidateProof(dto.AdmissionToken, dto.StudentId, dto.ExamId);
        if (proof is null) return BadRequest(new { message = "A fresh identity verification is required for this exam." });
        dto.AdmissionId = proof.Id;
        try { var session = await sessions.StartSessionAsync(dto); session.AttemptToken = tokens.Issue(session.StudentId,session.Id,session.ActiveClientId,false); session.EvidenceToken = tokens.Issue(session.StudentId,session.Id,session.EvidenceClientId!,true); return Ok(session); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    [HttpPut("{id:int}/answers"), Authorize(Roles = SystemRoles.Student)]
    public async Task<IActionResult> SaveAnswers(int id, Dictionary<int,int> answers)
    {
        if (!await access.OwnsSessionAsync(User,id)) return NotFound();
        var clientId=await access.CommandClientIdAsync(User,id,Request.Headers["X-Session-Token"]);
        if (clientId is null) return Forbid();
        try { return Ok(await sessions.SaveAnswersAsync(SessionAccess.UserId(User), id, answers, clientId)); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }
    [HttpPost("{id:int}/heartbeat"), Authorize(Roles = SystemRoles.Student)]
    public async Task<IActionResult> Heartbeat(int id)
    {
        if (!await access.OwnsSessionAsync(User,id)) return NotFound();
        var clientId=await access.CommandClientIdAsync(User,id,Request.Headers["X-Session-Token"]);
        if (clientId is null) return Forbid();
        try { return Ok(await sessions.HeartbeatAsync(SessionAccess.UserId(User), id, clientId)); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return Conflict(new {message=ex.Message}); }
    }
    [HttpPost("{id:int}/terminate"), Authorize(Roles = SystemRoles.Examiner)]
    public async Task<IActionResult> Terminate(int id)
    {
        if (!await access.CanReadAsync(User, id)) return NotFound();
        return Ok(await sessions.TerminateAsync(id));
    }

    [HttpGet("{id:int}/preview")]
    public async Task<IActionResult> Preview(int id) {
        if (!await access.CanReadEvidenceAsync(User,id,Request.Headers["X-Evidence-Token"])) return NotFound();
        var root=Environment.GetEnvironmentVariable("INFERENCE_IMAGES_PATH");
        if (string.IsNullOrWhiteSpace(root)) return NotFound();
        var path=Path.Combine(root,"previews",id+".jpg");
        if (!System.IO.File.Exists(path)) return NotFound();
        Response.Headers.CacheControl="no-store";
        Response.Headers["X-Captured-At"]=System.IO.File.GetLastWriteTimeUtc(path).ToString("O");
        return File(await System.IO.File.ReadAllBytesAsync(path),"image/jpeg");
    }

    [HttpPost("{id:int}/review"), Authorize(Roles = SystemRoles.Examiner)]
    public async Task<IActionResult> Review(int id, SessionReview request) {
        if (!await access.CanReadAsync(User,id)) return NotFound();
        try { return Ok(await sessions.ReviewSessionAsync(id,SessionAccess.UserId(User),request.Verdict,request.Notes)); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }
}

public record SessionReview(string Verdict, string Notes);
