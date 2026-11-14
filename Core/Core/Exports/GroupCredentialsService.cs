using System.Security.Cryptography;
using System.Text;
using BusinessLogicLayer.Constants;
using DataAccessLayer.Data;
using Microsoft.EntityFrameworkCore;
using Core.Documents;
using Core.Security;

namespace Core.Exports;

public sealed class GroupCredentialsService(AppDbContext db, IssuedCredentials credentials, GroupPdfWriter pdf)
{
    public async Task<byte[]> Export(string group, Guid[]? confirmedStudents, string format = "csv")
    {
        if (format is not ("csv" or "pdf")) throw new InvalidOperationException("Невідомий формат документа.");
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var students = await db.Users.Where(u => u.Role == SystemRoles.Student && (u.Group ?? "") == group)
            .OrderBy(u => u.FullName).ToListAsync();
        if (students.Count == 0) throw new InvalidOperationException("У групі немає студентів.");
        var reset = confirmedStudents is not null;
        if (reset && !students.Select(s => s.Id).ToHashSet().SetEquals(confirmedStudents!))
            throw new InvalidOperationException("Склад групи змінився. Оновіть список і повторіть підтвердження.");
        if (reset && await db.Sessions.AnyAsync(s => !s.IsFinished && students.Select(u => u.Id).Contains(s.StudentId)))
            throw new InvalidOperationException("У групі триває іспит. Завершіть активні спроби перед перевипуском паролів.");
        var csv = new StringBuilder("group,full_name,username" + (reset ? ",password" : "") + "\r\n");
        var document = new List<CredentialLine>();
        foreach (var student in students)
        {
            csv.Append(string.Join(",", new[] { group, student.FullName, student.Login }.Select(Cell)));
            string? password = null;
            if (reset)
            {
                password = credentials.Issue(student);
                csv.Append(',').Append(Cell(password));
            }
            if (format == "pdf") document.Add(new(student.FullName, student.Login, password ?? credentials.Read(student)));
            csv.Append("\r\n");
        }
        var bytes = format == "pdf" ? pdf.Credentials(group, document) : Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
        if (reset) await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return bytes;
    }

    private static string Cell(string value)
    {
        if (value.TrimStart().FirstOrDefault() is '=' or '+' or '-' or '@' || value.Any(char.IsControl)) value = "'" + value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
