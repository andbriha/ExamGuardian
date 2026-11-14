using System.Text.Json;
using DataAccessLayer.Data;
using Microsoft.EntityFrameworkCore;

namespace Core.Services;

// Explicit administrator maintenance command. Disabled until a retention period is configured.
public sealed class RetentionMaintenance(AppDbContext db, IWebHostEnvironment env, IConfiguration config)
{
    private static void DeleteIfPresent(string path)
    {
        try { File.Delete(path); }
        catch (DirectoryNotFoundException) { /* An already absent media directory is expired successfully. */ }
        catch (FileNotFoundException) { }
    }

    public async Task RunAsync(bool apply)
    {
        var days=config.GetValue<int>("Retention:Days");
        if (days < 7) throw new InvalidOperationException("Set Retention__Days to at least 7. No retention policy is enabled by default.");
        var cutoff=DateTime.UtcNow.AddDays(-days);
        var candidates=await db.Sessions.AsNoTracking().Where(s => s.IsFinished && s.EndTime < cutoff && s.IntegrityReviewedAt < cutoff && s.IntegrityVerdict != "UnderReview" && s.EvidenceStatus != "Expired").OrderBy(s => s.Id).Select(s => s.Id).ToListAsync();
        Console.WriteLine(JsonSerializer.Serialize(new { mode=apply ? "apply" : "preview", retentionDays=days, sessionIds=candidates }));
        if (!apply) return;
        foreach (var id in candidates)
        {
            await using var tx=await db.Database.BeginTransactionAsync();
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(1,{id})");
            var session=await db.Sessions.Include(s => s.Violations).FirstAsync(s => s.Id == id);
            if (!session.IsFinished || session.EndTime >= cutoff || session.IntegrityReviewedAt >= cutoff || session.IntegrityVerdict == "UnderReview" || session.EvidenceStatus == "Expired") continue;
            var root=Path.GetFullPath(config["EXAM_VIDEO_PATH"] ?? Path.Combine(env.ContentRootPath,"ExamVideos"));
            var directory=Path.GetFullPath(Path.Combine(root,id.ToString()));
            if (!directory.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.Ordinal)) throw new InvalidOperationException("Invalid retention target.");
            // Preserve sidecars containing hashes/timing; remove media only.
            foreach (var folder in new[] {directory,Path.Combine(directory,"screen")})
                if (Directory.Exists(folder)) foreach (var file in Directory.GetFiles(folder,"chunk_*.mp4")) DeleteIfPresent(file);
            var images=config["INFERENCE_IMAGES_PATH"] ?? Environment.GetEnvironmentVariable("INFERENCE_IMAGES_PATH");
            if (!string.IsNullOrWhiteSpace(images))
            {
                DeleteIfPresent(Path.Combine(images,"previews",id+".jpg"));
                if (session.AdmissionId is not null && System.Text.RegularExpressions.Regex.IsMatch(session.AdmissionId,@"^[a-zA-Z0-9_-]{16,128}$")) DeleteIfPresent(Path.Combine(images,"references",session.AdmissionId+".jpg"));
                foreach (var eventId in session.Violations.Select(v => v.EventId)) DeleteIfPresent(Path.Combine(images,"evidence",eventId+".jpg"));
            }
            session.EvidenceStatus="Expired"; session.EvidenceClientId=null;
            await db.SaveChangesAsync(); await tx.CommitAsync();
            Console.WriteLine($"Media expired for session {id}; answers, score and review history retained.");
        }
    }
}
