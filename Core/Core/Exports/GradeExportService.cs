using System.Globalization;
using System.Security.Claims;
using System.Text;
using Core.Security;
using Microsoft.EntityFrameworkCore;
namespace Core.Exports;

public record GradeRow(int Id, int ExamId, string ExamTitle, DateTime StartTime, Guid StudentId,
    string Login, string Group, bool IsFinished, string Status, double Score, string Verdict, string Evidence, bool Unresolved, bool IdentitySkipped);
public record ExportReason(string Code, string Message, int Count);
public record ExportExam(int ExamId, string Title, DateTime Date, int EligibleCount, int ExcludedCount);
public record ExportGroup(string Group, ExportExam[] Exams, ExportReason[] Exclusions);

public sealed class GradeExportService(SessionAccess access)
{
    public async Task<List<GradeRow>> Latest(ClaimsPrincipal user, int? examId = null)
    {
        var query = access.VisibleSessions(user).AsNoTracking();
        if (examId.HasValue) query = query.Where(s => s.ExamId == examId);
        var rows = await query.Select(s => new GradeRow(s.Id, s.ExamId, s.Exam.Title, s.StartTime,
            s.StudentId, s.Student.Login, s.Student.Group ?? "", s.IsFinished, s.Status, s.Score,
            s.IntegrityVerdict, s.EvidenceStatus, s.Violations.Any(v => v.ReviewDecision != "Dismissed"), s.AdmissionId != null && s.AdmissionId.StartsWith("dev_"))).ToListAsync();
        return rows.GroupBy(s => new { s.ExamId, s.StudentId }).Select(g => g.OrderByDescending(s => s.StartTime).ThenByDescending(s => s.Id).First()).ToList();
    }
    public static string? BlockReason(GradeRow row)
    {
        if (row.IdentitySkipped) return "identitySkipped";
        if (!row.IsFinished) return "unfinished";
        if (row.Status is not ("Completed" or "TimedOut")) return "ungraded";
        if (row.Verdict != "Accepted") return "unapproved";
        if (row.Evidence is not ("Delivered" or "Expired")) return "evidence";
        if (row.Unresolved) return "observations";
        if (string.IsNullOrWhiteSpace(row.Login) || "=+-@".Contains(row.Login.TrimStart()[0]) || row.Login.Any(char.IsControl)) return "identifier";
        return null;
    }
    public static string ReasonText(string code) => code switch {
        "identitySkipped" => "DEV: перевірку особи пропущено",
        "unfinished" => "Спроба ще не завершена", "ungraded" => "Немає розрахованої оцінки",
        "unapproved" => "Немає рішення викладача «Прийнято»", "evidence" => "Докази ще не доставлено повністю",
        "observations" => "Є невідхилені сигнали", "identifier" => "Логін несумісний з CSV",
        _ => "Немає доступних оцінок"
    };
    public async Task<ExportGroup[]> Catalog(ClaimsPrincipal user)
    {
        var rows = await Latest(user);
        return rows.GroupBy(r => r.Group).OrderBy(g => g.Key).Select(group => new ExportGroup(group.Key,
            group.GroupBy(r => r.ExamId).Where(exam => exam.Any(r => BlockReason(r) is null))
                .Select(exam => new ExportExam(exam.Key, exam.First().ExamTitle, exam.Min(r => r.StartTime),
                    exam.Count(r => BlockReason(r) is null), exam.Count(r => BlockReason(r) is not null)))
                .OrderByDescending(e => e.Date).ToArray(),
            group.Where(r => BlockReason(r) is not null).GroupBy(r => BlockReason(r)!)
                .Select(g => new ExportReason(g.Key, ReasonText(g.Key), g.Count())).ToArray())).ToArray();
    }
    public static byte[] Csv(IEnumerable<GradeRow> rows)
    {
        static string Cell(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
        return Encoding.UTF8.GetBytes("username,grade\r\n" + string.Concat(rows.OrderBy(r => r.Login, StringComparer.Ordinal)
            .Select(r => Cell(r.Login) + "," + r.Score.ToString("0.########", CultureInfo.InvariantCulture) + "\r\n")));
    }
}
