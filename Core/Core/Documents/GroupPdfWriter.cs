using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;

namespace Core.Documents;

public record CredentialLine(string Name, string Login, string? Password);
public record CategoryMark(string Category, int Earned, int Maximum);
public record StudentResult(string Name, string Status, double? Grade, CategoryMark[] Categories, int? SessionId);
public record GroupResultDocument(string Group, string Exam, int ExamId, StudentResult[] Students);

public sealed class GroupPdfWriter
{
    private static Document Start(string title, string subtitle, bool landscape = false) {
        var doc = new Document(); doc.Info.Title = title; doc.Info.Author = "ExamGuardian";
        var style = doc.Styles[StyleNames.Normal]!; style.Font.Name = "Noto Sans"; style.Font.Size = 9;
        style.ParagraphFormat.SpaceAfter = 4;
        var section = doc.AddSection(); section.PageSetup.PageFormat = PageFormat.A4;
        section.PageSetup.Orientation = landscape ? Orientation.Landscape : Orientation.Portrait;
        section.PageSetup.TopMargin = Unit.FromCentimeter(1.5);
        section.PageSetup.BottomMargin = Unit.FromCentimeter(2.5);
        section.PageSetup.FooterDistance = Unit.FromCentimeter(.8);
        section.PageSetup.LeftMargin = section.PageSetup.RightMargin = Unit.FromCentimeter(1.5);
        var brand = section.AddParagraph("EXAMGUARDIAN"); brand.Format.Font.Color = Colors.Teal; brand.Format.Font.Bold = true;
        var heading = section.AddParagraph(title); heading.Format.Font.Size = 20; heading.Format.Font.Bold = true;
        section.AddParagraph(subtitle).Format.Font.Color = Colors.DimGray;
        var footer = section.Footers.Primary.AddParagraph(); footer.Format.Font.Size = 8; footer.Format.Alignment = ParagraphAlignment.Right;
        footer.AddText($"{DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC  |  "); footer.AddPageField(); footer.AddText(" / "); footer.AddNumPagesField();
        return doc;
    }
    private static Table Table(Section section, string[] headings, double[] widths) {
        var table = section.AddTable(); table.Borders.Color = Colors.LightGray; table.Borders.Width = .4;
        table.TopPadding = table.BottomPadding = Unit.FromMillimeter(1.5);
        table.Format.SpaceAfter = 0;
        foreach (var width in widths) table.AddColumn(Unit.FromCentimeter(width));
        var header = table.AddRow(); header.HeadingFormat = true; header.Shading.Color = Colors.Teal; header.Format.Font.Color = Colors.White; header.Format.Font.Bold = true;
        for (int i = 0; i < headings.Length; i++) header.Cells[i].AddParagraph(headings[i]);
        return table;
    }
    private static void Row(Table table, params string[] values) {
        var row = table.AddRow(); if (table.Rows.Count % 2 == 0) row.Shading.Color = Color.FromRgb(242, 248, 248);
        for (int i = 0; i < values.Length; i++) row.Cells[i].AddParagraph(values[i]);
    }
    private static byte[] Render(Document document) {
        var renderer = new PdfDocumentRenderer { Document = document }; renderer.RenderDocument();
        using var stream = new MemoryStream(); renderer.PdfDocument.Save(stream, false); return stream.ToArray();
    }
    public byte[] Credentials(string group, IReadOnlyList<CredentialLine> students) {
        var doc = Start("Доступи студентів", $"Група: {group} | Студентів: {students.Count}");
        var section = doc.LastSection;
        section.AddParagraph("Примірник для викладача. Передавайте кожному студенту тільки його рядок; не надсилайте цей документ усій групі.");
        section.AddParagraph("Повторне завантаження не змінює паролі. «Не збережено» означає, що потрібен окремий перевипуск доступів.");
        var table = Table(section, ["Студент", "Логін", "Пароль"], [7, 5.5, 5.5]);
        foreach (var student in students) Row(table, student.Name, student.Login, student.Password ?? "Не збережено");
        return Render(doc);
    }
    public byte[] Results(GroupResultDocument result) {
        var doc = Start("Результати групи", $"{result.Exam} | Іспит #{result.ExamId} | Група: {result.Group}", true);
        var section = doc.LastSection;
        section.AddParagraph("Остання спроба кожного студента. Склад групи - поточний. Бали публікуються лише після підтвердження викладачем. Це відомість для групи, не файл імпорту до LMS.");
        var categories = result.Students.SelectMany(s => s.Categories).Select(c => c.Category).Distinct().Order().ToArray();
        var chunks = categories.Length == 0 ? new[] { Array.Empty<string>() } : categories.Chunk(4).ToArray();
        foreach (var chunk in chunks) {
            if (chunks.Length > 1) section.AddParagraph("Категорії: " + string.Join(", ", chunk)).Format.KeepWithNext = true;
            var widths = chunk.Length == 0 ? new[] { 8.0, 4.0, 4.0, 10.7 } : new[] { 6.0, 2.7, 2.7, 4.3 }.Concat(chunk.Select(_ => 11.0 / chunk.Length)).ToArray();
            var detail = Table(section, new[] { "Студент", "Разом / макс.", "Оцінка / 100", "Статус" }.Concat(chunk).ToArray(), widths);
            foreach (var s in result.Students) Row(detail, new[] { s.Name,
                s.Grade.HasValue && s.Categories.Length > 0 ? $"{s.Categories.Sum(c => c.Earned)} / {s.Categories.Sum(c => c.Maximum)}" : "-",
                s.Grade?.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) ?? "-", s.Status }.Concat(chunk.Select(category => {
                var mark = s.Categories.FirstOrDefault(c => c.Category == category);
                return mark is null ? "-" : $"{mark.Earned} / {mark.Maximum}";
            })).ToArray());
            section.AddParagraph();
        }
        if (categories.Length == 0) section.AddParagraph("Розподіл за категоріями з'явиться після підтвердження результатів із збереженим складом завдань.");
        return Render(doc);
    }
}
