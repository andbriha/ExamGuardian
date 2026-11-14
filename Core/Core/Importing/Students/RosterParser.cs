using Microsoft.VisualBasic.FileIO;

namespace Core.Importing.Students;

public record RosterInput(string Group, string Text);
public record RosterRow(int Row, string FullName, string Login, string Action, string? Error = null, Guid? ExistingId = null, bool Generated = false);

public static class RosterParser
{
    public static List<RosterRow> Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > 200000)
            throw new ArgumentException("Вставте список або CSV UTF-8 розміром до 200 КБ.");
        var first = text.TrimStart('\uFEFF').Split('\n')[0];
        var delimiter = first.Contains('\t') ? "\t" : first.Contains(';') ? ";" : ",";
        using var parser = new TextFieldParser(new StringReader(text.TrimStart('\uFEFF'))) {
            TextFieldType = FieldType.Delimited, HasFieldsEnclosedInQuotes = true, TrimWhiteSpace = true
        };
        parser.SetDelimiters(delimiter);
        var result = new List<RosterRow>();
        var nameIndex = 0; var loginIndex = 1; var columns = 0; var record = 0;
        try {
            while (!parser.EndOfData) {
                var cells = parser.ReadFields() ?? []; record++;
                if (cells.All(string.IsNullOrWhiteSpace)) continue;
                if (record == 1) {
                    var headers = cells.Select(s => s.ToLowerInvariant().Replace(" ", "").Replace("_", "")).ToArray();
                    var names = new[] { "fullname", "name", "піб", "фио", "ім'я" };
                    var candidate = Array.FindIndex(headers, names.Contains);
                    if (candidate >= 0) {
                        nameIndex = candidate; loginIndex = Array.FindIndex(headers, h => h is "login" or "username" or "логін" or "логин");
                        if (headers.Any(h => !names.Contains(h) && h is not ("login" or "username" or "логін" or "логин")))
                            throw new ArgumentException("Підтримуються колонки full_name і username. Групу оберіть над списком.");
                        columns = cells.Length; continue;
                    }
                }
                var name = nameIndex < cells.Length ? cells[nameIndex].Trim() : "";
                var login = loginIndex >= 0 && loginIndex < cells.Length ? cells[loginIndex].Trim().ToLowerInvariant() : "";
                var error = cells.Length > 2 || (columns > 0 && cells.Length != columns) ? "Неправильна кількість колонок" :
                    name.Length is < 2 or > 150 || name.Any(char.IsControl) ? "ПІБ: від 2 до 150 символів" :
                    login.Length > 100 || login.Any(c => !(char.IsAsciiLetterOrDigit(c) || "._-@".Contains(c))) ? "Логін: латинські літери, цифри, . _ - @; до 100 символів" : null;
                result.Add(new(record, name, login, "create", error));
                if (result.Count > 200) throw new ArgumentException("За один раз можна додати до 200 студентів.");
            }
        } catch (MalformedLineException) { throw new ArgumentException("Некоректний CSV: перевірте лапки та роздільники."); }
        if (result.Count == 0) throw new ArgumentException("Список не містить студентів.");
        return result;
    }
}
