using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace DailyGourmet.Api.Services;

public class PdfService : IPdfService
{
    public byte[] Render(IDocument document) => document.GeneratePdf();
}
