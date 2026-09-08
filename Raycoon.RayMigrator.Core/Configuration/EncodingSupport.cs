using System.Text;

namespace Raycoon.RayMigrator.Core.Configuration;

/// <summary>
/// Encoding helpers shared by every place that turns a configured encoding name into an <see cref="Encoding"/>.
/// </summary>
/// <remarks>
/// .NET (Core) only knows the Unicode encodings, ASCII and Latin-1 out of the box; every code-page encoding
/// such as <c>windows-1252</c> requires <see cref="Encoding.RegisterProvider"/> with the
/// <see cref="CodePagesEncodingProvider"/>, which ships in the shared framework. A user of the CLI cannot
/// call that, so the product does it once, at the first use of any encoding name (#4).
/// </remarks>
public static class EncodingSupport
{
    // Lazy<T> blocks concurrent callers until the registration has completed. A set-flag-then-register
    // pattern (Interlocked.Exchange) let a second thread call Encoding.GetEncoding("windows-1252") in the
    // window between the flag and the registration and fail with "not a supported encoding name".
    private static readonly Lazy<bool> CodePagesRegistered = new(() =>
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return true;
    });

    /// <summary>
    /// Registers the <see cref="CodePagesEncodingProvider"/> exactly once. Safe to call from any thread and
    /// from every place that resolves an encoding name, including hosts that never touch a migration file.
    /// </summary>
    public static void EnsureCodePagesRegistered() => _ = CodePagesRegistered.Value;

    /// <summary>
    /// The fixed part of every "not a valid encoding name" message, so that the attribute, the defaults merge,
    /// the engine and the documentation speak with one voice.
    /// </summary>
    public const string ValidNamesHint =
        "Use a .NET encoding name such as 'UTF-8', 'UTF-16', 'UTF-32', 'ASCII', 'iso-8859-1' or 'windows-1252'. " +
        "A byte-order mark (UTF-8, UTF-16, UTF-32) is detected automatically and overrides this setting. " +
        "'ANSI' is not an encoding name; use the concrete code page, e.g. 'windows-1252'.";

    /// <summary>
    /// Resolves an encoding name to a strict <see cref="Encoding"/> whose decoder throws on invalid bytes
    /// instead of replacing them with U+FFFD or '?'. Null or whitespace means UTF-8.
    /// </summary>
    /// <exception cref="ArgumentException">The name is not a known encoding (after code-page registration).</exception>
    public static Encoding GetStrictEncoding(string? encodingName)
    {
        EnsureCodePagesRegistered();

        if (string.IsNullOrWhiteSpace(encodingName))
            return StrictUtf8;

        return Encoding.GetEncoding(encodingName, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
    }

    /// <summary>UTF-8 without BOM emission whose decoder throws on invalid byte sequences.</summary>
    public static readonly Encoding StrictUtf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
}
