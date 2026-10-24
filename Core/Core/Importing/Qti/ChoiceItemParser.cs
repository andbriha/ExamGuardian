using System.Xml.Linq;
namespace Core.Importing.Qti;

public sealed class ChoiceItemParser : IQtiItemParser
{
    public bool CanParse(XDocument doc) => doc.Descendants().Any(e => e.Name.LocalName == "choiceInteraction");
    public ImportedQuestion Parse(XDocument doc)
    {
        var root = doc.Root ?? throw new InvalidDataException("Порожній XML.");
        var ns = root.Name.Namespace;
        if (root.Name.LocalName != "assessmentItem" || ns.NamespaceName is not
            ("http://www.imsglobal.org/xsd/imsqti_v2p1" or "http://www.imsglobal.org/xsd/imsqti_v2p2"))
            throw new InvalidDataException("Потрібен assessmentItem QTI 2.1 або 2.2; QTI 1.2 та 3.0 не підтримуються.");
        if ((string?)root.Attribute("adaptive") is "true" or "1" || (root.Elements(ns + "templateDeclaration").Any() || root.Elements(ns + "templateProcessing").Any()))
            throw new InvalidDataException("Адаптивні та шаблонні завдання не підтримуються.");
        var body = root.Element(ns + "itemBody") ?? throw new InvalidDataException("Відсутній itemBody.");
        var interactions = body.Descendants().Where(e => e.Name.LocalName.EndsWith("Interaction")).ToList();
        if (interactions.Count != 1 || interactions[0].Name != ns + "choiceInteraction" || (string?)interactions[0].Attribute("maxChoices") != "1")
            throw new InvalidDataException("Підтримується лише один choiceInteraction з maxChoices=1.");
        var choice = interactions[0];
        var allowed = new HashSet<string> { "itemBody", "choiceInteraction", "prompt", "simpleChoice", "p", "div", "span", "br", "strong", "em", "b", "i", "u", "ul", "ol", "li" };
        if (body.DescendantsAndSelf().Any(e => !allowed.Contains(e.Name.LocalName) || e.Name.Namespace != ns || e.Attributes().Any(a => a.Name.LocalName is "templateIdentifier" or "templateId" or "showHide")))
            throw new InvalidDataException("Медіа, формули, посилання або умовний вміст не підтримуються; перетворіть завдання на текст.");
        var declarations = root.Elements(ns + "responseDeclaration").ToList();
        var declaration = declarations.FirstOrDefault();
        if (declarations.Count != 1 || (string?)declaration?.Attribute("identifier") != "RESPONSE" ||
            (string?)choice.Attribute("responseIdentifier") != "RESPONSE" ||
            (string?)declaration?.Attribute("cardinality") != "single" || (string?)declaration?.Attribute("baseType") != "identifier")
            throw new InvalidDataException("Потрібна одна відповідь RESPONSE типу identifier/single.");
        var values = declaration!.Element(ns + "correctResponse")?.Elements(ns + "value").Select(e => e.Value.Trim()).ToArray() ?? [];
        var options = choice.Elements(ns + "simpleChoice").ToList();
        var ids = options.Select(e => (string?)e.Attribute("identifier") ?? "").ToArray();
        if (values.Length != 1 || options.Count is < 2 or > 10 || ids.Any(string.IsNullOrWhiteSpace) || ids.Distinct().Count() != ids.Length || !ids.Contains(values[0]))
            throw new InvalidDataException("Потрібні 2–10 унікальних варіантів та одна правильна відповідь.");
        var processing = root.Element(ns + "responseProcessing");
        var template = (string?)processing?.Attribute("template") ?? "";
        if (processing is null || processing.HasElements || !new[] {
            "http://www.imsglobal.org/question/qti_v2p1/rptemplates/match_correct",
            "http://www.imsglobal.org/question/qti_v2p2/rptemplates/match_correct" }.Contains(template))
            throw new InvalidDataException("Підтримується лише стандартний match_correct (1 бал), без часткових балів та власного responseProcessing.");
        var textBody = new XElement(body);
        textBody.Descendants(ns + "simpleChoice").Remove();
        static string Text(XElement element) => string.Join(" ", element.DescendantNodes().OfType<XText>().Select(t => t.Value.Trim()).Where(t => t.Length > 0));
        var text = Text(textBody); var answers = options.Select(Text).ToArray();
        if (string.IsNullOrWhiteSpace(text) || answers.Any(string.IsNullOrWhiteSpace)) throw new InvalidDataException("Порожній текст запитання або варіанта.");
        return new(text, 1, Array.IndexOf(ids, values[0]), answers);
    }
}
