using System.Reflection;
using System.Text;

namespace Raycoon.RayMigrator.Shared;

/// <summary>
/// Provides assembly version information. Shared across Console and API projects.
/// </summary>
public static class AssemblyInfoHelper
{
    /// <summary>
    /// Gets the RayMigrator engine version: the InformationalVersion of this (engine) assembly, formatted by
    /// <see cref="FormatVersion"/>. Deliberately not the entry assembly, so that a host such as RayMigrator
    /// Studio reports and records the engine version it embeds, not its own.
    /// </summary>
    public static string GetRayMigratorVersion()
    {
        var versionAttribute = typeof(AssemblyInfoHelper).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        return FormatVersion(versionAttribute?.InformationalVersion ?? "");
    }

    /// <summary>
    /// Formats an InformationalVersion for display and for the repository (MigratorMeta.RayMigratorVersion).
    /// A release build ("0.14.0+&lt;sha&gt;") yields the bare version. A pre-release build ("0.14.0-dev+&lt;sha&gt;",
    /// the VersionSuffix set in Directory.Build.props for every build outside the release workflows) keeps the
    /// first seven characters of the commit hash the SDK appended, because the hash is what identifies such a
    /// build: "0.14.0-dev+39b6fa3". Without hash the pre-release label stands alone ("0.14.0-dev").
    /// </summary>
    public static string FormatVersion(string informationalVersion)
    {
        var plus = informationalVersion.IndexOf('+');
        if (plus < 0)
            return informationalVersion;

        var version = informationalVersion[..plus];
        var metadata = informationalVersion[(plus + 1)..];
        if (!version.Contains('-') || metadata.Length == 0)
            return version;

        return version + "+" + (metadata.Length > 7 ? metadata[..7] : metadata);
    }

    /// <summary>
    /// Returns the ASCII logo lines with version, site URL, tagline, and the pre-1.0 maturity notice.
    /// </summary>
    public static string[] GetAsciiLogoLines(string version)
    {
        return new[]
        {
            @" ______               _______ __                    __",
            @"|   __ \.---.-.--.--.|   |   |__|.-----.----.---.-.|  |_.-----.----.",
            @"|      <|  _  |  |  ||       |  ||  _  |   _|  _  ||   _|  _  |   _|",
            $@"|___|__||___._|___  ||__|_|__|__||___  |__| |___._||____|_____|__|",
            $@"              |_____|            |_____|    Version {version}",
            "",
            " RayMigrator.com",
            " Pro Database Migration Framework",
            " Pre-1.0 - not yet proven in production.",
            " Back up before every run.",
            "",
            " Licensed under BUSL-1.1 - see LICENSE.md.",
            " Using this software constitutes acceptance of its terms."
        };
    }

    /// <summary>
    /// Returns the full ASCII header including logo, description, and copyright from the entry assembly.
    /// </summary>
    public static string GetAsciiHeader()
    {
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var copyrightAttribute = assembly.GetCustomAttribute<AssemblyCopyrightAttribute>();
        var version = GetRayMigratorVersion();

        var sb = new StringBuilder();
        foreach (var line in GetAsciiLogoLines(version))
            sb.AppendLine(line);

        sb.AppendLine($" {copyrightAttribute?.Copyright}");

        return sb.ToString();
    }
}
