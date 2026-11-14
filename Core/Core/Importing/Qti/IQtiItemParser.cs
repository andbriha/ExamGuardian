using System.Xml.Linq;
namespace Core.Importing.Qti;

public record ImportedQuestion(string Text, int Points, int CorrectIndex, string[] Options);
public record ImportIssue(string File, string Message);
public interface IQtiItemParser
{
    bool CanParse(XDocument document);
    ImportedQuestion Parse(XDocument document);
}
