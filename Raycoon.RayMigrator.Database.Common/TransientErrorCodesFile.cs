namespace Raycoon.RayMigrator.Database.Common;

/// <summary>
/// The optional <c>TransientErrorCodes.txt</c> next to a DAL in <c>DataAccessLayers/{DatabaseType}/</c>: one error
/// code per line, <c>#</c> starts a comment (whole line or trailing), blank lines are ignored. The file is the complete
/// list of codes the DAL retries; it replaces the built-in list and does not merge with it (ADR-022). A missing file
/// keeps the built-in list, a malformed file aborts the start.
/// </summary>
public static class TransientErrorCodesFile
{
    /// <summary>The file name RayMigrator looks for in a DAL folder.</summary>
    public const string FileName = "TransientErrorCodes.txt";

    /// <summary>The folder below the application base directory that holds one subfolder per DatabaseType.</summary>
    public const string DataAccessLayersDirectory = "DataAccessLayers";

    /// <summary>The path of the file for <paramref name="databaseType"/> below <paramref name="baseDirectory"/>.</summary>
    public static string GetPath(string baseDirectory, string databaseType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseType);
        return Path.Combine(baseDirectory, DataAccessLayersDirectory, databaseType, FileName);
    }

    /// <summary>
    /// Parses the lines of a file into the list of codes: every line is trimmed, a <c>#</c> comment is stripped,
    /// blank lines are skipped. The codes keep their spelling; the DAL compares them case-insensitively.
    /// </summary>
    /// <exception cref="FormatException">A code contains whitespace; the message names the 1-based line number.</exception>
    public static IReadOnlyList<string> Parse(IEnumerable<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        List<string> codes = [];
        int lineNumber = 0;
        foreach (string line in lines)
        {
            lineNumber++;
            string content = line;
            int commentStart = content.IndexOf('#');
            if (commentStart >= 0)
            {
                content = content.Substring(0, commentStart);
            }

            content = content.Trim();
            if (content.Length == 0)
            {
                continue;
            }

            if (content.Any(char.IsWhiteSpace))
            {
                throw new FormatException(
                    $"Line {lineNumber}: a transient error code must not contain whitespace, found [{content}]. " +
                    "Write one code per line; a comment starts with #.");
            }

            codes.Add(content);
        }

        return codes;
    }

    /// <summary>
    /// Loads the file for <paramref name="databaseType"/> when it exists. Returns false, with
    /// <paramref name="path"/> set to the probed location and an empty <paramref name="codes"/> list, when there is
    /// no file; the DAL then keeps its built-in list.
    /// </summary>
    /// <exception cref="FormatException">The file is malformed, see <see cref="Parse"/>.</exception>
    /// <exception cref="IOException">The file exists but cannot be read.</exception>
    public static bool TryLoad(string baseDirectory, string databaseType, out string path, out IReadOnlyList<string> codes)
    {
        path = GetPath(baseDirectory, databaseType);
        if (!File.Exists(path))
        {
            codes = [];
            return false;
        }

        codes = Parse(File.ReadLines(path));
        return true;
    }
}
