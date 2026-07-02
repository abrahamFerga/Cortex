using System.ComponentModel;
using System.Text;
using Cortex.Application.Documents;
using Cortex.Application.Files;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Writer;

namespace Cortex.Infrastructure.Documents;

/// <summary>
/// The platform's document tools, exposed to every module's agent (permission-gated per tool).
/// PDF reading and generation are pure managed code (PdfPig, Apache-2.0) — no native dependencies,
/// no API keys — so they work identically in dev, CI, and production. OCR is a pluggable seam
/// (<see cref="IOcrEngine"/>); the tool only appears when a host registers an engine.
/// </summary>
public sealed class DocumentTools(IFileStore files, IOcrEngine? ocr = null)
{
    /// <summary>Cap tool output so a big document can't blow up the model context.</summary>
    private const int MaxExtractChars = 12_000;

    public bool HasOcrEngine => ocr is not null;

    [Description("Read the text content of a stored file (PDF or plain text). Use the file id from the attachment reference or from list_documents.")]
    public async Task<string> ReadDocument(
        [Description("The stored file id (a GUID).")] string fileId,
        CancellationToken cancellationToken = default)
    {
        var (file, error) = await ResolveAsync(fileId, cancellationToken);
        if (file is null)
        {
            return error!;
        }

        await using var content = await files.OpenReadAsync(file.Id, cancellationToken);
        if (content is null)
        {
            return $"The content of file '{file.FileName}' is missing from storage.";
        }

        if (IsPdf(file.ContentType, file.FileName))
        {
            var text = ExtractPdfText(content);
            if (!string.IsNullOrWhiteSpace(text))
            {
                return Truncate($"Text of '{file.FileName}':\n\n{text}");
            }

            if (ocr is not null)
            {
                content.Position = 0;
                var ocrText = await ocr.ExtractTextAsync(content, file.ContentType, cancellationToken);
                return string.IsNullOrWhiteSpace(ocrText)
                    ? $"'{file.FileName}' has no text layer and the {ocr.Name} OCR engine could not extract text."
                    : Truncate($"OCR text of '{file.FileName}' (via {ocr.Name}):\n\n{ocrText}");
            }

            return $"'{file.FileName}' appears to be a scanned document (no text layer), and no OCR engine is configured on this deployment.";
        }

        if (file.ContentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase) ||
            file.ContentType is "application/json" or "application/xml")
        {
            using var reader = new StreamReader(content);
            return Truncate($"Content of '{file.FileName}':\n\n{await reader.ReadToEndAsync(cancellationToken)}");
        }

        return $"'{file.FileName}' ({file.ContentType}) is not a readable document type. Supported: PDF and plain text.";
    }

    [Description("Run OCR (optical character recognition) on a stored scanned PDF or image and return the recognized text.")]
    public async Task<string> OcrDocument(
        [Description("The stored file id (a GUID).")] string fileId,
        CancellationToken cancellationToken = default)
    {
        if (ocr is null)
        {
            return "No OCR engine is configured on this deployment.";
        }

        var (file, error) = await ResolveAsync(fileId, cancellationToken);
        if (file is null)
        {
            return error!;
        }

        await using var content = await files.OpenReadAsync(file.Id, cancellationToken);
        if (content is null)
        {
            return $"The content of file '{file.FileName}' is missing from storage.";
        }

        var text = await ocr.ExtractTextAsync(content, file.ContentType, cancellationToken);
        return string.IsNullOrWhiteSpace(text)
            ? $"The {ocr.Name} OCR engine could not extract text from '{file.FileName}'."
            : Truncate($"OCR text of '{file.FileName}' (via {ocr.Name}):\n\n{text}");
    }

    [Description("Generate a PDF document from a title and body text, store it, and return its file id and download link.")]
    public async Task<string> GeneratePdf(
        [Description("The document title, shown as a heading.")] string title,
        [Description("The body text. Blank lines separate paragraphs.")] string body,
        [Description("Optional file name (defaults to the title).")] string? fileName = null,
        CancellationToken cancellationToken = default)
    {
        var bytes = BuildPdf(title, body);

        var name = string.IsNullOrWhiteSpace(fileName) ? $"{title}.pdf" : fileName;
        if (!name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            name += ".pdf";
        }

        using var stream = new MemoryStream(bytes);
        var stored = await files.SaveAsync(name, "application/pdf", stream, source: "generate_pdf", cancellationToken);

        return $"Generated '{stored.FileName}' ({stored.SizeBytes:N0} bytes). File id: {stored.Id}. Download: /api/files/{stored.Id}";
    }

    [Description("List the caller's stored files (most recent first) with their ids, names, sizes, and dates.")]
    public async Task<string> ListDocuments(CancellationToken cancellationToken = default)
    {
        var mine = await files.ListMineAsync(20, cancellationToken);
        if (mine.Count == 0)
        {
            return "You have no stored files yet.";
        }

        var sb = new StringBuilder("Your stored files (newest first):\n");
        foreach (var f in mine)
        {
            sb.AppendLine($"- {f.FileName} — id {f.Id}, {f.SizeBytes:N0} bytes, {f.ContentType}, {f.CreatedAt:yyyy-MM-dd HH:mm} UTC, source: {f.Source}");
        }

        return sb.ToString();
    }

    // --- internals -------------------------------------------------------------------------------

    private async Task<(Core.Platform.StoredFile? File, string? Error)> ResolveAsync(string fileId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(fileId, out var id))
        {
            return (null, $"'{fileId}' is not a valid file id. Use list_documents to find the right id.");
        }

        var file = await files.FindAsync(id, cancellationToken);
        return file is null
            ? (null, $"No stored file with id {id} exists (or it belongs to another tenant). Use list_documents to see available files.")
            : (file, null);
    }

    internal static bool IsPdf(string contentType, string fileName) =>
        contentType.Contains("pdf", StringComparison.OrdinalIgnoreCase) ||
        fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);

    internal static string ExtractPdfText(Stream content)
    {
        using var pdf = PdfDocument.Open(content);
        var sb = new StringBuilder();
        foreach (Page page in pdf.GetPages())
        {
            if (sb.Length >= MaxExtractChars)
            {
                break;
            }

            sb.AppendLine(page.Text);
        }

        return sb.ToString().Trim();
    }

    private static string Truncate(string text) =>
        text.Length <= MaxExtractChars ? text : text[..MaxExtractChars] + "\n\n[truncated]";

    /// <summary>
    /// Minimal, dependency-free PDF layout via PdfPig's builder and the standard-14 Helvetica fonts
    /// (built into every PDF reader — nothing to embed, works on any OS): a title line, then
    /// word-wrapped paragraphs, paginating onto new A4 pages as needed.
    /// </summary>
    internal static byte[] BuildPdf(string title, string body)
    {
        var builder = new PdfDocumentBuilder();
        var bold = builder.AddStandard14Font(UglyToad.PdfPig.Fonts.Standard14Fonts.Standard14Font.HelveticaBold);
        var regular = builder.AddStandard14Font(UglyToad.PdfPig.Fonts.Standard14Fonts.Standard14Font.Helvetica);

        const double margin = 50;
        const double titleSize = 18;
        const double bodySize = 11;
        const double lineHeight = bodySize * 1.45;

        var page = builder.AddPage(UglyToad.PdfPig.Content.PageSize.A4);
        var y = page.PageSize.Top - margin;
        var width = page.PageSize.Width - (2 * margin);

        page.AddText(title, titleSize, new PdfPoint(margin, y), bold);
        y -= titleSize * 2;

        foreach (var paragraph in body.Replace("\r\n", "\n").Split("\n\n", StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var line in Wrap(page, paragraph.Replace('\n', ' ').Trim(), bodySize, width, regular))
            {
                if (y < margin + lineHeight)
                {
                    page = builder.AddPage(UglyToad.PdfPig.Content.PageSize.A4);
                    y = page.PageSize.Top - margin;
                }

                page.AddText(line, bodySize, new PdfPoint(margin, y), regular);
                y -= lineHeight;
            }

            y -= lineHeight * 0.6; // paragraph spacing
        }

        return builder.Build();
    }

    private static IEnumerable<string> Wrap(
        PdfPageBuilder page, string text, double fontSize, double maxWidth, PdfDocumentBuilder.AddedFont font)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var line = new StringBuilder();

        foreach (var word in words)
        {
            var candidate = line.Length == 0 ? word : $"{line} {word}";
            var letters = page.MeasureText(candidate, fontSize, new PdfPoint(0, 0), font);
            var candidateWidth = letters.Count == 0 ? 0 : letters[^1].BoundingBox.Right;

            if (candidateWidth > maxWidth && line.Length > 0)
            {
                yield return line.ToString();
                line.Clear().Append(word);
            }
            else
            {
                line.Clear().Append(candidate);
            }
        }

        if (line.Length > 0)
        {
            yield return line.ToString();
        }
    }
}
