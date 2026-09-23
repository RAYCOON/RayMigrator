namespace Raycoon.RayMigrator.Shared.Configuration;

/// <summary>
/// The names and the merge order of the appsettings hierarchy, and the inverse mapping from a file name
/// back to its role. The engine builds its file list from <see cref="FileNamesFor"/>, the Config Wizard
/// classifies uploaded files with <see cref="TryClassify"/>, so both agree on names, order and casing (#23).
/// </summary>
public static class ConfigurationFileChain
{
    /// <summary>File name prefix of every file in the hierarchy.</summary>
    public const string Prefix = "appsettings";

    /// <summary>File name extension of every file in the hierarchy.</summary>
    public const string Extension = ".json";

    /// <summary>The base file, <c>appsettings.json</c>.</summary>
    public const string BaseFileName = Prefix + Extension;

    /// <summary>
    /// Returns the files of the hierarchy for a product and an environment in merge order:
    /// base, environment, product, product+environment. A blank environment omits the two environment
    /// files, a blank product omits the two product files.
    /// </summary>
    public static IReadOnlyList<(ConfigFileRole Role, string FileName)> FileNamesFor(string? product, string? environment)
    {
        var files = new List<(ConfigFileRole, string)> { (ConfigFileRole.Base, BaseFileName) };

        bool hasEnvironment = !string.IsNullOrWhiteSpace(environment);
        bool hasProduct = !string.IsNullOrWhiteSpace(product);

        if (hasEnvironment)
            files.Add((ConfigFileRole.Environment, $"{Prefix}.{environment}{Extension}"));

        if (hasProduct)
        {
            files.Add((ConfigFileRole.Product, $"{Prefix}.{product}{Extension}"));
            if (hasEnvironment)
                files.Add((ConfigFileRole.ProductEnvironment, $"{Prefix}.{product}.{environment}{Extension}"));
        }

        return files;
    }

    /// <summary>
    /// Classifies a file name (a path is reduced to its file name) into its role. Returns false for a name
    /// that does not belong to the hierarchy. A name with one middle segment is a product file when the
    /// segment is one of <paramref name="knownProductAliases"/> (alias comparison), otherwise an
    /// environment file. A name with three or more segments is a product+environment file whose product
    /// keeps every segment but the last.
    /// </summary>
    public static bool TryClassify(
        string fileName,
        out ConfigFileRole role,
        out string? product,
        out string? environment,
        IReadOnlyCollection<string>? knownProductAliases = null)
    {
        role = ConfigFileRole.Base;
        product = null;
        environment = null;

        if (!TryGetSegments(fileName, out var segments))
            return false;

        switch (segments.Length)
        {
            case 0:
                return true;

            case 1:
                if (knownProductAliases != null && knownProductAliases.Any(alias => AliasComparer.AliasEquals(alias, segments[0])))
                {
                    role = ConfigFileRole.Product;
                    product = segments[0];
                }
                else
                {
                    role = ConfigFileRole.Environment;
                    environment = segments[0];
                }
                return true;

            default:
                role = ConfigFileRole.ProductEnvironment;
                product = string.Join('.', segments.Take(segments.Length - 1));
                environment = segments[^1];
                return true;
        }
    }

    /// <summary>
    /// Splits the middle part of a hierarchy file name into its dot separated segments
    /// (<c>appsettings.json</c> yields an empty array). Returns false when the name has neither the
    /// prefix nor the extension of the hierarchy.
    /// </summary>
    public static bool TryGetSegments(string fileName, out string[] segments)
    {
        segments = Array.Empty<string>();
        if (string.IsNullOrWhiteSpace(fileName))
            return false;

        // A path from any client (Windows or Unix separators) is reduced to its file name.
        int lastSeparator = Math.Max(fileName.LastIndexOf('/'), fileName.LastIndexOf('\'));
        string name = lastSeparator >= 0 ? fileName[(lastSeparator + 1)..] : fileName;
        if (!name.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase) ||
            !name.EndsWith(Extension, StringComparison.OrdinalIgnoreCase))
            return false;

        string middle = name[Prefix.Length..^Extension.Length];
        if (middle.Length == 0)
            return true;

        if (!middle.StartsWith('.'))
            return false;

        middle = middle[1..];
        if (middle.Length == 0)
            return false;

        segments = middle.Split('.');
        return segments.All(s => s.Length > 0);
    }
}
