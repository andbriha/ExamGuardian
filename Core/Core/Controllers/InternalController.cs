using System.Text.Json;
using Core.Attributes;
using DataAccessLayer.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Core.Controllers;

[ApiController]
[Route("api/internal")]
[ApiKey]
public class InternalController(AppDbContext db, Core.Security.AttemptTokens tokens) : ControllerBase
{
    [HttpGet("sessions/{id:int}")]
    public async Task<IActionResult> Session(int id, [FromQuery] Guid studentId)
    {
        var session = await db.Sessions.AsNoTracking().Include(s => s.Exam).FirstOrDefaultAsync(s => s.Id == id && s.StudentId == studentId);
        if (session is null || session.IsFinished) return NotFound();
        if (!tokens.Validate(Request.Headers["X-Session-Token"],session,false)) return Forbid();
        if (session.DeadlineUtc <= DateTime.UtcNow) return NotFound();
        using var snapshot = JsonDocument.Parse(session.ExamSnapshotJson ?? "{}");
        var root=snapshot.RootElement;
        bool Policy(string key, bool fallback) => root.ValueKind == JsonValueKind.Object && root.TryGetProperty(key,out var value) ? value.GetBoolean() : fallback;
        var allowed = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("AllowedObjects",out var objects)
            ? JsonSerializer.Deserialize<List<string>>(objects.GetRawText()) : JsonSerializer.Deserialize<List<string>>(session.Exam.AllowedObjectsJson);
        return Ok(new { session.Id, session.StudentId, session.StartTime, session.ExamId, referenceId=session.AdmissionId, allowSpeech=Policy("AllowSpeech",session.Exam.AllowSpeech), flagHeadMovement=Policy("FlagHeadMovement",session.Exam.FlagHeadMovement), allowedObjects=allowed });
    }

    [HttpPost("sessions/{id:int}/observed")]
    public async Task<IActionResult> Observed(int id, ObservationReceipt receipt)
    {
        var session = await db.Sessions.FirstOrDefaultAsync(s => s.Id == id && s.StudentId == receipt.StudentId && !s.IsFinished);
        if (session is null) return NotFound();
        if (receipt.Channel == "camera") session.LastAnalysisAt = DateTime.UtcNow;
        else if (receipt.Channel == "audio") session.LastAudioAt = DateTime.UtcNow;
        else return BadRequest();
        await db.SaveChangesAsync(); return NoContent();
    }
}

public record ObservationReceipt(Guid StudentId, string Channel);
