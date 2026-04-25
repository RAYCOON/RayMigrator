namespace Raycoon.RayMigrator.Core.Configuration.Enums;

/// <summary>
/// Determines how the migration SQL file is passed to the external CLI tool.
/// </summary>
public enum CliToolInputMode : byte
{
    Undefined = 0,

    /// <summary>
    /// The file path is passed as a command-line argument via the {FilePath} placeholder in ArgumentTemplate.
    /// Used by tools like sqlcmd (-i), psql (-f), sqlite3 (-init).
    /// </summary>
    File = 1,

    /// <summary>
    /// The file content is piped to the process via standard input (Process.StandardInput).
    /// Used by tools like mysql and mariadb that read SQL from stdin.
    /// </summary>
    Stdin = 2,
}
