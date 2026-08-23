using QuestPDF.Infrastructure;

namespace DailyGourmet.Api.Services;

/// <summary>Thin facade over QuestPDF — one IDocument implementation per template (see
/// Services/Pdf/*), rendered here rather than through a single generic dispatcher since each
/// template's layout is materially different.</summary>
public interface IPdfService
{
    byte[] Render(IDocument document);
}
