using System.Text;
using SierraNueva.Core.Abstractions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace SierraNueva.Infrastructure.Documents;

public sealed class PdfPigTextExtractor : IPdfTextExtractor
{
    public string Extract(ReadOnlyMemory<byte> content)
    {
        using PdfDocument document = PdfDocument.Open(content.ToArray());
        StringBuilder text = new();
        foreach (UglyToad.PdfPig.Content.Page page in document.GetPages())
        {
            text.AppendLine(ContentOrderTextExtractor.GetText(page));
        }

        return text.ToString();
    }
}
