using PdfSharp.Fonts;

namespace Core.Documents;
public sealed class DocumentFonts : IFontResolver
{
    public FontResolverInfo ResolveTypeface(string familyName, bool bold, bool italic) => new(bold ? "NotoSans-Bold" : "NotoSans-Regular");
    public byte[] GetFont(string faceName) {
        var assembly = typeof(DocumentFonts).Assembly;
        var name = assembly.GetManifestResourceNames().Single(n => n.EndsWith(faceName + ".ttf"));
        using var stream = assembly.GetManifestResourceStream(name)!;
        using var result = new MemoryStream(); stream.CopyTo(result); return result.ToArray();
    }
}
