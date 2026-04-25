
using Microsoft.AspNetCore.Components;

namespace Raycoon.RayMigrator.Tests.Unit.ConfigWizard.Web;

/// <summary>
/// Tests for JsonHighlightService — JSON syntax highlighting via regex.
/// </summary>
public class JsonHighlightServiceTests
{
    private readonly JsonHighlightService _svc = new();

    // ── Empty / null input ────────────────────────────────────────

    [Fact]
    public void Highlight_NullInput_ReturnsEmptySpan()
    {
        var result = _svc.Highlight(null!);

        result.Value.Should().Contain("json-preview");
    }

    [Fact]
    public void Highlight_EmptyString_ReturnsEmptySpan()
    {
        var result = _svc.Highlight("");

        result.Value.Should().Contain("json-preview");
    }

    [Fact]
    public void Highlight_WhitespaceOnly_ReturnsEmptySpan()
    {
        var result = _svc.Highlight("   ");

        result.Value.Should().Contain("json-preview");
    }

    // ── Output structure ──────────────────────────────────────────

    [Fact]
    public void Highlight_ValidJson_ReturnsPreWrappedOutput()
    {
        var result = _svc.Highlight("""{"key": "value"}""");

        result.Value.Should().StartWith("<pre class=\"json-preview\">");
        result.Value.Should().EndWith("</pre>");
    }

    [Fact]
    public void Highlight_ValidJson_ReturnsMarkupString()
    {
        var result = _svc.Highlight("""{"key": "value"}""");

        result.Should().BeOfType<MarkupString>();
    }

    // ── Key presence (keys survive HTML-encoding) ─────────────────
    // Note: JsonHighlightService HTML-encodes first, so literal " chars become &quot;
    // The key/string regexes match literal " chars and thus do NOT match HTML-encoded content.
    // Boolean/null literals and numbers match content that doesn't use " and DO get highlighted.

    [Fact]
    public void Highlight_JsonKey_PreservesKeyTextInOutput()
    {
        var result = _svc.Highlight("""{"myKey": "value"}""");

        // Key text is present (HTML-encoded: the " became &quot; around it)
        result.Value.Should().Contain("myKey");
    }

    [Fact]
    public void Highlight_MultipleKeys_PreservesAllKeyTexts()
    {
        var result = _svc.Highlight("""{"firstKey": "v1", "secondKey": "v2"}""");

        result.Value.Should().Contain("firstKey");
        result.Value.Should().Contain("secondKey");
    }

    [Fact]
    public void Highlight_StringValue_PreservesValueTextInOutput()
    {
        var result = _svc.Highlight("""{"key": "hello world"}""");

        result.Value.Should().Contain("hello world");
    }

    // ── Boolean / null literal highlighting ──────────────────────
    // true/false/null are unquoted — they are not HTML-encoded and DO get span-highlighted

    [Fact]
    public void Highlight_TrueLiteral_PresentsInOutput()
    {
        var result = _svc.Highlight("""{"enabled": true}""");

        result.Value.Should().Contain("true");
    }

    [Fact]
    public void Highlight_TrueLiteral_WrapsInColorSpan()
    {
        var result = _svc.Highlight("""{"enabled": true}""");

        // "true" is not HTML-encoded, so LiteralPattern matches and wraps it
        result.Value.Should().Contain("<span style=\"color:#569CD6\">true</span>");
    }

    [Fact]
    public void Highlight_FalseLiteral_WrapsInColorSpan()
    {
        var result = _svc.Highlight("""{"active": false}""");

        result.Value.Should().Contain("<span style=\"color:#569CD6\">false</span>");
    }

    [Fact]
    public void Highlight_NullLiteral_WrapsInColorSpan()
    {
        var result = _svc.Highlight("""{"value": null}""");

        result.Value.Should().Contain("<span style=\"color:#569CD6\">null</span>");
    }

    // ── Numeric value highlighting ────────────────────────────────
    // Numbers after ":" are not HTML-encoded, so NumberPattern matches and wraps them

    [Fact]
    public void Highlight_PositiveInteger_WrapsInGreenSpan()
    {
        var result = _svc.Highlight("""{"timeout": 30}""");

        result.Value.Should().Contain("<span style=\"color:#B5CEA8\">30</span>");
    }

    [Fact]
    public void Highlight_NegativeNumber_WrapsInGreenSpan()
    {
        var result = _svc.Highlight("""{"delta": -5}""");

        result.Value.Should().Contain("<span style=\"color:#B5CEA8\">-5</span>");
    }

    [Fact]
    public void Highlight_DecimalNumber_WrapsInGreenSpan()
    {
        var result = _svc.Highlight("""{"ratio": 0.5}""");

        result.Value.Should().Contain("<span style=\"color:#B5CEA8\">0.5</span>");
    }

    // ── XSS prevention ───────────────────────────────────────────

    [Fact]
    public void Highlight_XssAttemptInValue_HtmlEncodesOutput()
    {
        var result = _svc.Highlight("""{"key": "<script>alert('xss')</script>"}""");

        result.Value.Should().NotContain("<script>");
        result.Value.Should().Contain("&lt;script&gt;");
    }

    [Fact]
    public void Highlight_AmpersandInValue_HtmlEncodes()
    {
        var result = _svc.Highlight("""{"key": "a&b"}""");

        result.Value.Should().Contain("&amp;");
    }

    [Fact]
    public void Highlight_QuoteInKey_HtmlEncodes()
    {
        // A key containing a special char
        var result = _svc.Highlight("{\"ke<y\": \"v\"}");

        result.Value.Should().NotContain("<y");
        result.Value.Should().Contain("&lt;");
    }

    // ── Realistic JSON input ──────────────────────────────────────

    [Fact]
    public void Highlight_RealWorldJson_ProducesNonEmptyOutput()
    {
        const string json = """
            {
                "RayMigrator": {
                    "Repository": {
                        "DatabaseType": "SqlServer",
                        "ConnectionString": "{ENV:REPO_CONNECTION_STRING}",
                        "SchemaName": "migrations",
                        "DbCommandTimeoutInSeconds": 60
                    }
                }
            }
            """;

        var result = _svc.Highlight(json);

        result.Value.Should().Contain("RayMigrator");
        result.Value.Should().Contain("Repository");
        result.Value.Should().Contain("SqlServer");
        result.Value.Should().Contain("60");
    }

    [Fact]
    public void Highlight_EmptyJsonObject_ReturnsPreWithBraces()
    {
        var result = _svc.Highlight("{}");

        result.Value.Should().Contain("{}");
        result.Value.Should().StartWith("<pre");
    }
}
