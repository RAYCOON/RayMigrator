using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;

namespace Raycoon.RayMigrator.ConfigWizard.Web.Services;

/// <summary>
/// Provides simple JSON syntax highlighting for the live preview panel.
/// Returns MarkupString for direct rendering in Blazor components.
/// </summary>
public partial class JsonHighlightService
{
    [GeneratedRegex("""("(?:[^"\\]|\\.)*")\s*:""")]
    private static partial Regex KeyPattern();

    [GeneratedRegex(""":\s*("(?:[^"\\]|\\.)*")""")]
    private static partial Regex StringValuePattern();

    [GeneratedRegex("""\b(true|false|null)\b""")]
    private static partial Regex LiteralPattern();

    [GeneratedRegex(""":\s*(-?\d+(?:\.\d+)?)""")]
    private static partial Regex NumberPattern();

    /// <summary>
    /// Highlights JSON with simple CSS color classes.
    /// </summary>
    public MarkupString Highlight(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new MarkupString("<span class=\"json-preview\"></span>");

        // HTML-encode first to prevent XSS
        var encoded = System.Net.WebUtility.HtmlEncode(json);

        // Apply highlighting in a safe order
        encoded = KeyPattern().Replace(encoded, m =>
            $"<span style=\"color:#569CD6\">{m.Groups[1].Value}</span>:");

        encoded = StringValuePattern().Replace(encoded, m =>
            $": <span style=\"color:#CE9178\">{m.Groups[1].Value}</span>");

        encoded = LiteralPattern().Replace(encoded, m =>
            $"<span style=\"color:#569CD6\">{m.Groups[1].Value}</span>");

        encoded = NumberPattern().Replace(encoded, m =>
            $": <span style=\"color:#B5CEA8\">{m.Groups[1].Value}</span>");

        return new MarkupString($"<pre class=\"json-preview\">{encoded}</pre>");
    }
}
