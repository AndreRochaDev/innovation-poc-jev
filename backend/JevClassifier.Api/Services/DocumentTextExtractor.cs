using System.Text;
using System.Text.RegularExpressions;

namespace JevClassifier.Api.Services;

public static class DocumentTextExtractor
{
    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    { ".txt", ".md", ".eml", ".json", ".csv", ".log" };

    public static async Task<string> ExtractAsync(IFormFile file, CancellationToken ct)
    {
        var ext = Path.GetExtension(file.FileName ?? "").ToLowerInvariant();

        if (ext == ".pdf")
        {
            throw new InvalidOperationException(
                "PDF sem parser nesta POC (dependência PdfPig indisponível offline). Converta para .txt ou cole o texto diretamente.");
        }

        if (!TextExtensions.Contains(ext))
        {
            // Try to read as text anyway for unknown extensions
            if (file.Length > 10 * 1024 * 1024)
                throw new InvalidOperationException("Ficheiro demasiado grande (máx 10MB).");
        }

        using var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var text = await reader.ReadToEndAsync(ct);
        return Normalize(text);
    }

    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        text = text.Replace("\r\n", "\n").Trim();
        text = Regex.Replace(text, @"[ \t]{2,}", " ");
        text = Regex.Replace(text, @"\n{3,}", "\n\n");
        return text;
    }
}
