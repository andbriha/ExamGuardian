using System.Text.Json;
using BusinessLogicLayer.Constants;
using Core.Security;
using DataAccessLayer.Data;
using DataAccessLayer.Entities;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace Core.Importing.Students;

public record RosterPreview(string Group, RosterRow[] Rows, string? Token);
public record RosterPlan(Guid Teacher, string Group, RosterRow[] Rows, DateTime Expires);
public sealed class RosterImportService(AppDbContext db, IDataProtectionProvider protection, IssuedCredentials credentials)
{
    private readonly IDataProtector signer = protection.CreateProtector("student-roster-preview-v1");
    public async Task<RosterPreview> Preview(RosterInput input, Guid teacher)
    {
        var group = input.Group?.Trim() ?? "";
        if (group.Length is < 1 or > 100 || group.Any(char.IsControl)) throw new ArgumentException("Оберіть або введіть групу (до 100 символів).");
        group = await db.StudentGroups.Where(g => g.Name.ToLower() == group.ToLower()).Select(g => g.Name).FirstOrDefaultAsync() ?? group;
        var rows = RosterParser.Parse(input.Text);
        var logins = rows.Where(r => r.Login != "").Select(r => r.Login).ToArray();
        var existing = await db.Users.AsNoTracking().Where(u => u.Group == group || logins.Contains(u.Login.ToLower())).ToListAsync();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var prefix = "s-" + Guid.NewGuid().ToString("N")[..10];
        for (var i = 0; i < rows.Count; i++) {
            var row = rows[i];
            if (row.Error is not null) continue;
            var key = row.Login == "" ? "name:" + row.FullName : "login:" + row.Login;
            if (!seen.Add(key)) { rows[i] = row with { Error = "Повтор у списку. Для тезок вкажіть різні логіни." }; continue; }
            var matches = existing.Where(u => row.Login != "" ? u.Login.Equals(row.Login, StringComparison.OrdinalIgnoreCase) : u.Group == group && u.FullName.Equals(row.FullName, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (matches.Length > 0) {
                var student = matches[0];
                rows[i] = matches.Length == 1 && student.Role == SystemRoles.Student && student.Group == group && student.FullName.Equals(row.FullName, StringComparison.OrdinalIgnoreCase)
                    ? row with { Login = student.Login, Action = "skip", ExistingId = student.Id }
                    : row with { Error = "Акаунт уже існує з іншими даними або знайдено тезок. Уточніть логін; перенесення групи тут не виконується." };
            } else if (row.Login == "") rows[i] = row with { Login = $"{prefix}-{i + 1:000}", Generated = true };
        }
        var duplicates = rows.GroupBy(r => r.Login, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        rows = rows.Select(r => duplicates.Contains(r.Login) ? r with { Error = "Повтор логіна у списку" } : r).ToList();
        var token = rows.Any(r => r.Error is not null) ? null : signer.Protect(JsonSerializer.Serialize(new RosterPlan(teacher, group, rows.ToArray(), DateTime.UtcNow.AddMinutes(15))));
        return new(group, rows.ToArray(), token);
    }
    public async Task<object> Commit(string token, Guid teacher)
    {
        RosterPlan plan;
        try { plan = JsonSerializer.Deserialize<RosterPlan>(signer.Unprotect(token))!; }
        catch (Exception ex) when (ex is System.Security.Cryptography.CryptographicException or JsonException or ArgumentException) { throw new ArgumentException("Попередній перегляд недійсний. Перевірте список ще раз."); }
        if (plan is null || plan.Teacher != teacher || plan.Expires < DateTime.UtcNow) throw new ArgumentException("Попередній перегляд застарів. Перевірте список ще раз.");
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var canonicalGroup = await db.StudentGroups.Where(g => g.Name.ToLower() == plan.Group.ToLower()).Select(g => g.Name).FirstOrDefaultAsync();
        if (canonicalGroup is not null && canonicalGroup != plan.Group) throw new InvalidOperationException("Назву групи уточнено. Повторіть попередній перегляд.");
        var logins = plan.Rows.Select(r => r.Login.ToLowerInvariant()).ToArray();
        var existing = await db.Users.Where(u => u.Group == plan.Group || logins.Contains(u.Login.ToLower())).ToListAsync();
        var created = 0; var skipped = 0;
        foreach (var row in plan.Rows) {
            var user = existing.SingleOrDefault(u => u.Login.Equals(row.Login, StringComparison.OrdinalIgnoreCase));
            if (user is not null) {
                if (user.Role != SystemRoles.Student || user.Group != plan.Group || !user.FullName.Equals(row.FullName, StringComparison.OrdinalIgnoreCase) || (row.ExistingId.HasValue && row.ExistingId != user.Id))
                    throw new InvalidOperationException("Список змінився. Повторіть попередній перегляд.");
                skipped++; continue; // Retrying a successful commit never rotates passwords.
            }
            if (row.Action != "create") throw new InvalidOperationException("Студента зі списку вже видалено. Повторіть перегляд.");
            if (row.Generated && existing.Any(u => u.Group == plan.Group && u.FullName.Equals(row.FullName, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("У групі вже з'явився студент з цим ім'ям. Повторіть перегляд і уточніть логін.");
            user = new User { Id = Guid.NewGuid(), FullName = row.FullName, Login = row.Login, Group = plan.Group, Role = SystemRoles.Student };
            credentials.Issue(user); db.Users.Add(user); created++;
        }
        if (!await db.StudentGroups.AnyAsync(g => g.Name == plan.Group)) db.StudentGroups.Add(new StudentGroup { Name = plan.Group });
        await db.SaveChangesAsync(); await tx.CommitAsync();
        return new { plan.Group, Created = created, Skipped = skipped };
    }
}
