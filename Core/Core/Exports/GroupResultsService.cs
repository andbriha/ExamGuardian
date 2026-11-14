using System.Security.Claims;
using System.Text.Json;
using Core.Documents;
using Core.Security;
using DataAccessLayer.Data;
using DataAccessLayer.Entities;
using Microsoft.EntityFrameworkCore;

namespace Core.Exports;
public sealed class GroupResultsService(AppDbContext db, SessionAccess access)
{
    public static CategoryMark[] Breakdown(ExamSession session) {
        if (string.IsNullOrWhiteSpace(session.ExamSnapshotJson)) return [];
        try {
            using var snapshot = JsonDocument.Parse(session.ExamSnapshotJson);
            var root = snapshot.RootElement;
            var questions = root.ValueKind == JsonValueKind.Array ? root : root.GetProperty("Questions");
            // New graders only need to persist awarded points per question. Reports
            // do not need to know whether the task was listening, matching, etc.
            var scores = session.QuestionScoresJson is null ? null : JsonSerializer.Deserialize<Dictionary<int, int>>(session.QuestionScoresJson);
            var answers = scores is null ? JsonSerializer.Deserialize<Dictionary<int, int>>(session.SavedAnswersJson) ?? [] : new Dictionary<int, int>();
            return questions.EnumerateArray().Select(q => {
                var category = q.TryGetProperty("ReportCategory", out var c) && !string.IsNullOrWhiteSpace(c.GetString()) ? c.GetString()! : "Тестові завдання";
                var points = q.GetProperty("Points").GetInt32();
                var correct = scores is null && answers.TryGetValue(q.GetProperty("Id").GetInt32(), out var answer) && answer == q.GetProperty("CorrectIndex").GetInt32();
                var earned = scores is null ? (correct ? points : 0) : scores.GetValueOrDefault(q.GetProperty("Id").GetInt32());
                return new CategoryMark(category, Math.Clamp(earned, 0, points), points);
            }).GroupBy(c => c.Category).Select(g => new CategoryMark(g.Key, g.Sum(c => c.Earned), g.Sum(c => c.Maximum))).ToArray();
        } catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException) { return []; }
    }
    public async Task<GroupResultDocument?> Build(ClaimsPrincipal user, int examId, string group) {
        if (!await access.OwnsExamAsync(user, examId)) return null;
        var exam = await db.Exams.AsNoTracking().SingleAsync(e => e.Id == examId);
        var students = await db.Users.AsNoTracking().Where(u => u.Role == "Student" && (u.Group ?? "") == group).OrderBy(u => u.FullName).ToListAsync();
        if (students.Count == 0) throw new InvalidOperationException("У вибраній групі немає студентів.");
        var ids = students.Select(u => u.Id).ToArray();
        var sessions = await access.VisibleSessions(user).AsNoTracking().Where(s => s.ExamId == examId && ids.Contains(s.StudentId)).ToListAsync();
        if (!string.IsNullOrEmpty(exam.AllowedGroup) && exam.AllowedGroup != group && sessions.Count == 0)
            throw new InvalidOperationException("Цей іспит не призначений вибраній групі.");
        var latest = sessions.GroupBy(s => s.StudentId).ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.StartTime).ThenByDescending(s => s.Id).First());
        var rows = students.Select(student => {
            latest.TryGetValue(student.Id, out var session);
            var approved = session is { IsFinished: true, IntegrityVerdict: "Accepted", Status: "Completed" or "TimedOut" } && !(session.AdmissionId?.StartsWith("dev_") ?? false);
            var status = session is null ? "Не розпочато" : !session.IsFinished ? "Триває" : session.AdmissionId?.StartsWith("dev_") == true ? "Тестова спроба" :
                approved ? "Підтверджено" : session.IntegrityVerdict == "Rejected" ? "Відхилено" : "На перевірці";
            var categories = approved ? Breakdown(session!) : [];
            if (approved && categories.Length == 0) status += " (деталізація недоступна)";
            return new StudentResult(student.FullName, status, approved ? session!.Score : null, categories, session?.Id);
        }).ToArray();
        return new(group == "" ? "Без групи" : group, exam.Title, examId, rows);
    }
}
