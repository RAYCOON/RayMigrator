using System.Reflection;
using System.Text;

namespace Raycoon.RayMigrator.Shared;

/// <summary>
/// Provides assembly version information. Shared across Console and API projects.
/// </summary>
public static class AssemblyInfoHelper
{
    /// <summary>
    /// Gets the RayMigrator version string from the entry assembly's InformationalVersion attribute.
    /// </summary>
    public static string GetRayMigratorVersion()
    {
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var versionAttribute = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        var informationalVersion = versionAttribute?.InformationalVersion ?? "";
        var versionParts = informationalVersion.Split('+');
        return versionParts.Length > 0 ? versionParts[0] : "";
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
            " Back up before every run."
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
