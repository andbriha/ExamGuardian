using System.Text.Json;
using DataAccessLayer.Data;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using BusinessLogicLayer.Constants;
using Core.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Core.Controllers;

[ApiController, Route("api/video"), Authorize]
public class VideoUploadController(IWebHostEnvironment env, SessionAccess access, MediaLinkSigner signer, AppDbContext db, Core.Services.VideoInspector inspector) : ControllerBase
{
    private string StoragePath => Environment.GetEnvironmentVariable("EXAM_VIDEO_PATH") ?? Path.Combine(env.ContentRootPath, "ExamVideos");
    private string ChunkPath(int sessionId, int chunkIndex, string stream = "camera") => Path.Combine(StoragePath, sessionId.ToString(), stream == "camera" ? "" : "screen", $"chunk_{chunkIndex:D6}.mp4");

    [HttpPost("upload-chunk"), Authorize(Roles = SystemRoles.Student)]
    [RequestSizeLimit(128 * 1024 * 1024), RequestFormLimits(MultipartBodyLengthLimit = 128 * 1024 * 1024)]
    public async Task<IActionResult> UploadChunk([FromForm] int sessionId, [FromForm] int chunkIndex, IFormFile file, [FromForm] string stream = "camera", [FromForm] double? startOffsetSeconds = null, [FromForm] double? durationSeconds = null)
    {
        if ((startOffsetSeconds is double offset && !double.IsFinite(offset)) || (durationSeconds is double length && !double.IsFinite(length))) return BadRequest();
        if (stream is not ("camera" or "screen") || startOffsetSeconds < 0 || durationSeconds <= 0 || startOffsetSeconds > 86400 || durationSeconds > 180) return BadRequest();
        if (sessionId <= 0 || chunkIndex <= 0 || chunkIndex > 2048 || file is null || file.Length < 12 || file.Length > 128 * 1024 * 1024)
            return BadRequest(new { message = "Invalid video chunk." });
        if (!await access.OwnsSessionAsync(User, sessionId)) return NotFound();
        if (!await access.CanCommandAsync(User,sessionId,Request.Headers["X-Evidence-Token"],true)) return Forbid();
        var path = ChunkPath(sessionId, chunkIndex, stream);
        var legacy = Path.Combine(StoragePath, sessionId.ToString(), $"chunk_{chunkIndex:D3}.mp4");
        if (stream == "camera" && System.IO.File.Exists(legacy)) path = legacy;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".upload";
        try
        {
            await using (var output = System.IO.File.Create(temporary)) await file.CopyToAsync(output, HttpContext.RequestAborted);
            var header = new byte[12];
            await using (var input = System.IO.File.OpenRead(temporary)) await input.ReadExactlyAsync(header, HttpContext.RequestAborted);
            if (System.Text.Encoding.ASCII.GetString(header, 4, 4) != "ftyp") return BadRequest(new { message = "Expected an MP4 container." });
            double? actualDuration;
            try { actualDuration = await inspector.DurationAsync(temporary,HttpContext.RequestAborted); }
            catch (System.ComponentModel.Win32Exception) { return StatusCode(503,new { message = "Video validation is not configured." }); }
            if (actualDuration is null) return BadRequest(new { message = "The file has no readable video track." });
            if (durationSeconds is not null && Math.Abs(durationSeconds.Value-actualDuration.Value) > 1) return BadRequest(new { message = "Duration does not match the video." });
            durationSeconds = actualDuration;
            var hash = await HashAsync(temporary);
            await using var transaction = await db.Database.BeginTransactionAsync();
            var lockKey = $"evidence:{sessionId}:{stream}:{chunkIndex}";
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(3,hashtext({lockKey}))");
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(1,{sessionId})");
            if (!await access.CanCommandAsync(User,sessionId,Request.Headers["X-Evidence-Token"],true)) return Forbid();
            bool newChunk = !System.IO.File.Exists(path);
            try { System.IO.File.Move(temporary, path, overwrite: false); }
            catch (IOException) when (System.IO.File.Exists(path))
            {
                if (hash != await HashAsync(path)) return Conflict(new { message = "A different chunk already has this index." });
            }
            var metadataPath = path + ".json";
            var metadata = new ChunkMetadata(startOffsetSeconds, durationSeconds, stream, hash);
            bool newMetadata = !System.IO.File.Exists(metadataPath);
            if (!newMetadata) {
                var previous = JsonSerializer.Deserialize<ChunkMetadata>(await System.IO.File.ReadAllTextAsync(metadataPath));
                if (previous != metadata) return Conflict(new { message = "Chunk metadata differs from the stored evidence." });
            } else {
                await System.IO.File.WriteAllTextAsync(temporary + ".json", JsonSerializer.Serialize(metadata));
                System.IO.File.Move(temporary + ".json", metadataPath, true);
            }
            if (newChunk || newMetadata) {
                var session = await db.Sessions.FindAsync(sessionId);
                session!.EvidenceStatus = "Pending";
                session.IntegrityVerdict = "UnderReview";
                await db.SaveChangesAsync();
            }
            await transaction.CommitAsync();
            return Ok(new { status = "saved", sessionId, chunkIndex, stream, sha256 = hash, sizeBytes = file.Length });
        }
        finally { if (System.IO.File.Exists(temporary)) System.IO.File.Delete(temporary); if (System.IO.File.Exists(temporary + ".json")) System.IO.File.Delete(temporary + ".json"); }
    }
    private static async Task<string> HashAsync(string path)
    {
        await using var stream = System.IO.File.OpenRead(path);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream));
    }
    [HttpGet("{sessionId:int}/chunks")]
    public async Task<IActionResult> GetChunks(int sessionId, string stream = "camera")
    {
        if (!await access.CanReadEvidenceAsync(User,sessionId,Request.Headers["X-Evidence-Token"])) return NotFound();
        if (stream is not ("camera" or "screen")) return BadRequest();
        var directory = Path.Combine(StoragePath, sessionId.ToString(), stream == "camera" ? "" : "screen");
        if (!Directory.Exists(directory)) return Ok(Array.Empty<object>());
        var expires = DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds();
        var chunks = Directory.GetFiles(directory, "chunk_*.mp4")
            .Select(path => new { path, number = ParseIndex(path) }).Where(x => x.number > 0).OrderBy(x => x.number)
            .Select(x => new { metadata = ReadMetadata(x.path), chunkIndex = x.number, fileName = Path.GetFileName(x.path), sizeBytes = new FileInfo(x.path).Length,
                url = $"/api/video/{sessionId}/{x.number}?expires={expires}&signature={signer.Sign(sessionId, x.number, expires, stream)}&stream={stream}" });
        return Ok(chunks);
    }
    private static int ParseIndex(string path) => int.TryParse(Path.GetFileNameWithoutExtension(path).Replace("chunk_", ""), out var n) ? n : 0;
    [HttpGet("{sessionId:int}/{chunkIndex:int}"), AllowAnonymous]
    public async Task<IActionResult> GetChunk(int sessionId, int chunkIndex, long expires, string? signature, string stream = "camera")
    {
        if (stream is not ("camera" or "screen")) return BadRequest();
        if (!(User.Identity?.IsAuthenticated == true && await access.CanReadEvidenceAsync(User,sessionId,Request.Headers["X-Evidence-Token"])) && !signer.Validate(sessionId, chunkIndex, expires, signature, stream)) return Unauthorized();
        var path = ChunkPath(sessionId, chunkIndex, stream);
        if (!System.IO.File.Exists(path) && stream == "camera") path = Path.Combine(StoragePath, sessionId.ToString(), $"chunk_{chunkIndex:D3}.mp4");
        return System.IO.File.Exists(path) ? PhysicalFile(path, "video/mp4", enableRangeProcessing: true) : NotFound();
    }

    private static ChunkMetadata? ReadMetadata(string path) {
        try { return System.IO.File.Exists(path + ".json") ? JsonSerializer.Deserialize<ChunkMetadata>(System.IO.File.ReadAllText(path + ".json")) : null; }
        catch (JsonException) { return null; }
    }
    [HttpPost("{sessionId:int}/finalize"), Authorize(Roles = SystemRoles.Student)]
    public async Task<IActionResult> FinalizeEvidence(int sessionId, Dictionary<string,int> manifest) {
        if (!await access.OwnsSessionAsync(User, sessionId)) return NotFound();
        if (!await access.CanCommandAsync(User,sessionId,Request.Headers["X-Evidence-Token"],true)) return Forbid();
        if (!manifest.ContainsKey("camera") || !manifest.ContainsKey("screen") || manifest.Any(x => x.Key is not ("camera" or "screen") || x.Value is < 0 or > 2048)) return BadRequest();
        await using var tx = await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(1,{sessionId})");
        if (!await access.CanCommandAsync(User,sessionId,Request.Headers["X-Evidence-Token"],true)) return Forbid();
        var session = await db.Sessions.FindAsync(sessionId);
        if (session is null || !session.IsFinished) return Conflict(new { message = "Submit answers before finalizing evidence." });
        bool complete = true;
        foreach (var (stream,count) in manifest) {
            if (count == 0) complete = false;
            var directory=Path.Combine(StoragePath,sessionId.ToString(),stream == "camera" ? "" : "screen");
            if (Directory.Exists(directory) && Directory.GetFiles(directory,"chunk_*.mp4").Any(p => ParseIndex(p)>count)) complete=false;
            double end = 0;
            for (int n = 1; n <= count; n++) {
                var path = ChunkPath(sessionId,n,stream);
                var metadata = ReadMetadata(path);
                if (!System.IO.File.Exists(path) || metadata?.StartOffsetSeconds is not double start || metadata.DurationSeconds is not double duration) { complete = false; continue; }
                if (start-end > 3 || start < end-3) complete = false;
                end = start+duration;
            }
            if (end < (session.EndTime!.Value-session.StartTime).TotalSeconds-3) complete = false;
        }
        var normalized=JsonSerializer.Serialize(new Dictionary<string,int>{{"camera",manifest["camera"]},{"screen",manifest["screen"]}});
        var status=complete ? "Delivered" : "Partial";
        if (session.EvidenceManifestJson != normalized || session.EvidenceStatus != status) session.IntegrityVerdict="UnderReview";
        session.EvidenceManifestJson = normalized;
        session.EvidenceStatus = status;
        await db.SaveChangesAsync(); await tx.CommitAsync();
        return Ok(new { status = session.EvidenceStatus, sessionId });
    }
}

public record ChunkMetadata(double? StartOffsetSeconds, double? DurationSeconds, string Stream, string Sha256);
