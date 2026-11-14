using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;
namespace Core.Importing.Qti;

public sealed class QtiPackageReader(IEnumerable<IQtiItemParser> parsers)
{
    public object Read(MemoryStream input, string filename)
    {
        var questions = new List<ImportedQuestion>();
        var issues = new List<ImportIssue>();
        try
        {
            input.Position = 0;
            if (Path.GetExtension(filename).Equals(".xml", StringComparison.OrdinalIgnoreCase))
                AddItem(ReadXml(input), filename);
            else if (Path.GetExtension(filename).Equals(".zip", StringComparison.OrdinalIgnoreCase))
            {
                using var zip = new ZipArchive(input, ZipArchiveMode.Read);
                if (zip.Entries.Count > 1000 || zip.Entries.Sum(e => e.Length) > 25L * 1024 * 1024)
                    throw new InvalidDataException("Пакет перевищує ліміт: 1000 файлів / 25 МБ після розпакування.");
                if (zip.Entries.Any(e => !SafePath(e.FullName)) || zip.Entries.Select(e => e.FullName).Distinct().Count() != zip.Entries.Count)
                    throw new InvalidDataException("Пакет містить небезпечні або повторні шляхи.");
                var manifest = zip.GetEntry("imsmanifest.xml") ?? throw new InvalidDataException("У ZIP немає imsmanifest.xml.");
                using var manifestStream = manifest.Open();
                var doc = ReadXml(manifestStream);
                XNamespace cp = "http://www.imsglobal.org/xsd/imscp_v1p1";
                if (doc.Root?.Name != cp + "manifest" || doc.Descendants().Attributes(XNamespace.Xml + "base").Any())
                    throw new InvalidDataException("Непідтримуваний manifest або xml:base.");
                var resources = doc.Descendants(cp + "resource").Where(e => ((string?)e.Attribute("type"))?.StartsWith("imsqti_item_xmlv2p", StringComparison.Ordinal) == true).ToList();
                if (resources.Count is 0 or > 500) throw new InvalidDataException("Пакет має містити від 1 до 500 ресурсів QTI 2.x assessmentItem.");
                foreach (var resource in resources)
                {
                    var href = (string?)resource.Attribute("href") ?? "";
                    if (!SafePath(href)) throw new InvalidDataException("Неприпустиме посилання у manifest.");
                    var entry = zip.GetEntry(href);
                    if (entry is null) { issues.Add(new(href, "Файл із manifest відсутній.")); continue; }
                    using var itemStream = entry.Open();
                    try { AddItem(ReadXml(itemStream), href); }
                    catch (XmlException) { issues.Add(new(href, "Некоректний або небезпечний XML.")); }
                }
            }
            else throw new InvalidDataException("Підтримуються лише .xml та .zip.");
            return new { questions, issues, canImport = questions.Count > 0 && issues.Count == 0,
                notice = "QTI 2.1/2.2: текстові завдання, одна правильна відповідь, match_correct, 1 бал. Імпортується банк завдань у порядку manifest; розклад, секції та правила тесту задайте у конструкторі. Форматування спрощується до тексту, порядок варіантів зберігається." };
        }
        catch (XmlException ex) { throw new InvalidDataException("Некоректний або небезпечний XML.", ex); }

        void AddItem(XDocument doc, string name)
        {
            try { questions.Add((parsers.FirstOrDefault(p => p.CanParse(doc)) ?? throw new InvalidDataException("Непідтримуваний тип завдання QTI.")).Parse(doc)); }
            catch (InvalidDataException ex) { issues.Add(new(name, ex.Message)); }
        }
    }

    private static bool SafePath(string path) => !string.IsNullOrWhiteSpace(path) &&
        !path.StartsWith('/') && !path.Contains('\\') && !path.Contains(':') && !path.Contains('%') &&
        !path.Split('/').Any(s => s is ".." or ".");

    private static XDocument ReadXml(Stream stream)
    {
        using var reader = XmlReader.Create(stream, new XmlReaderSettings {
            DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 2 * 1024 * 1024 });
        return XDocument.Load(reader);
    }

}
