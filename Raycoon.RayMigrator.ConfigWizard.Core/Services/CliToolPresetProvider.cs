
using Raycoon.RayMigrator.ConfigWizard.Core.Models;

namespace Raycoon.RayMigrator.ConfigWizard.Core.Services;

/// <summary>
/// Provides hardcoded CLI tool presets for all 5 database engines plus Docker variants.
/// </summary>
public static class CliToolPresetProvider
{
    private static readonly List<CliToolPreset> AllPresetsInternal = new()
    {
        // 1. SQL Server native
        new CliToolPreset
        {
            Alias = "sqlcmd",
            DatabaseType = "SqlServer",
            ExecutablePath = "sqlcmd",
            ArgumentTemplate = "-S {Server} -U {User} -P {Password} -d {Database} -i \"{FilePath}\" -b",
            InputMode = "File",
            SuccessExitCodes = new List<string> { "0" },
            CliToolTimeoutInSeconds = 120,
            Description = "SQL Server native CLI tool (sqlcmd) -- File mode",
            IsDockerVariant = false,
            ExpectedParameterKeys = new List<string> { "Server", "User", "Password", "Database" }
        },
        // 2. PostgreSQL native
        new CliToolPreset
        {
            Alias = "psql",
            DatabaseType = "PostgreSQL",
            ExecutablePath = "psql",
            ArgumentTemplate = "-h {Host} -U {User} -d {Database} -f \"{FilePath}\"",
            InputMode = "File",
            SuccessExitCodes = new List<string> { "0" },
            CliToolTimeoutInSeconds = 120,
            Description = "PostgreSQL native CLI tool (psql) -- File mode",
            IsDockerVariant = false,
            ExpectedParameterKeys = new List<string> { "Host", "User", "Database" }
        },
        // 3. MariaDB native
        new CliToolPreset
        {
            Alias = "mariadb",
            DatabaseType = "MariaDb",
            ExecutablePath = "mariadb",
            ArgumentTemplate = "-h {Host} -u {User} -p{Password} {Database}",
            InputMode = "Stdin",
            SuccessExitCodes = new List<string> { "0" },
            CliToolTimeoutInSeconds = 120,
            Description = "MariaDB native CLI tool (mariadb) -- Stdin mode",
            IsDockerVariant = false,
            ExpectedParameterKeys = new List<string> { "Host", "User", "Password", "Database" }
        },
        // 4. MySQL native
        new CliToolPreset
        {
            Alias = "mysql",
            DatabaseType = "MySql",
            ExecutablePath = "mysql",
            ArgumentTemplate = "-h {Host} -u {User} -p{Password} {Database}",
            InputMode = "Stdin",
            SuccessExitCodes = new List<string> { "0" },
            CliToolTimeoutInSeconds = 120,
            Description = "MySQL native CLI tool (mysql) -- Stdin mode",
            IsDockerVariant = false,
            ExpectedParameterKeys = new List<string> { "Host", "User", "Password", "Database" }
        },
        // 5. SQLite native
        new CliToolPreset
        {
            Alias = "sqlite3",
            DatabaseType = "Sqlite",
            ExecutablePath = "sqlite3",
            ArgumentTemplate = "\"{Database}\" < \"{FilePath}\"",
            InputMode = "File",
            SuccessExitCodes = new List<string> { "0" },
            CliToolTimeoutInSeconds = 60,
            Description = "SQLite native CLI tool (sqlite3) -- File mode",
            IsDockerVariant = false,
            ExpectedParameterKeys = new List<string> { "Database" }
        },
        // 6. SQL Server Docker
        new CliToolPreset
        {
            Alias = "sqlcmd-docker",
            DatabaseType = "SqlServer",
            ExecutablePath = "docker",
            ArgumentTemplate = "exec -i {ContainerName} /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P {Password} -C -d {Database} -b",
            InputMode = "Stdin",
            SuccessExitCodes = new List<string> { "0" },
            CliToolTimeoutInSeconds = 30,
            Description = "SQL Server via Docker container (sqlcmd) -- Stdin mode",
            IsDockerVariant = true,
            ExpectedParameterKeys = new List<string> { "ContainerName", "Password", "Database" }
        },
        // 7. PostgreSQL Docker
        new CliToolPreset
        {
            Alias = "psql-docker",
            DatabaseType = "PostgreSQL",
            ExecutablePath = "docker",
            ArgumentTemplate = "exec -i {ContainerName} psql --set ON_ERROR_STOP=1 -U {User} -d {Database}",
            InputMode = "Stdin",
            SuccessExitCodes = new List<string> { "0" },
            CliToolTimeoutInSeconds = 30,
            Description = "PostgreSQL via Docker container (psql) -- Stdin mode",
            IsDockerVariant = true,
            ExpectedParameterKeys = new List<string> { "ContainerName", "User", "Database" }
        },
        // 8. MariaDB Docker
        new CliToolPreset
        {
            Alias = "mariadb-docker",
            DatabaseType = "MariaDb",
            ExecutablePath = "docker",
            ArgumentTemplate = "exec -i {ContainerName} mariadb -u {User} -p{Password} {Database}",
            InputMode = "Stdin",
            SuccessExitCodes = new List<string> { "0" },
            CliToolTimeoutInSeconds = 30,
            Description = "MariaDB via Docker container (mariadb) -- Stdin mode",
            IsDockerVariant = true,
            ExpectedParameterKeys = new List<string> { "ContainerName", "User", "Password", "Database" }
        },
        // 9. MySQL Docker
        new CliToolPreset
        {
            Alias = "mysql-docker",
            DatabaseType = "MySql",
            ExecutablePath = "docker",
            ArgumentTemplate = "exec -i {ContainerName} mysql -u {User} -p{Password} {Database}",
            InputMode = "Stdin",
            SuccessExitCodes = new List<string> { "0" },
            CliToolTimeoutInSeconds = 30,
            Description = "MySQL via Docker container (mysql) -- Stdin mode",
            IsDockerVariant = true,
            ExpectedParameterKeys = new List<string> { "ContainerName", "User", "Password", "Database" }
        },
        // 10. SQLite Docker
        new CliToolPreset
        {
            Alias = "sqlite3-docker",
            DatabaseType = "Sqlite",
            ExecutablePath = "docker",
            ArgumentTemplate = "exec -i {ContainerName} sqlite3 {Database}",
            InputMode = "Stdin",
            SuccessExitCodes = new List<string> { "0" },
            CliToolTimeoutInSeconds = 30,
            Description = "SQLite via Docker container (sqlite3) -- Stdin mode",
            IsDockerVariant = true,
            ExpectedParameterKeys = new List<string> { "ContainerName", "Database" }
        },
    };

    /// <summary>Returns all available presets.</summary>
    public static IReadOnlyList<CliToolPreset> GetAllPresets() => AllPresetsInternal;

    /// <summary>Returns presets for a specific database type.</summary>
    public static IReadOnlyList<CliToolPreset> GetPresetsForDatabaseType(string databaseType) =>
        AllPresetsInternal.Where(p => string.Equals(p.DatabaseType, databaseType, StringComparison.OrdinalIgnoreCase)).ToList();

    /// <summary>Returns Docker-specific presets only.</summary>
    public static IReadOnlyList<CliToolPreset> GetDockerPresets() =>
        AllPresetsInternal.Where(p => p.IsDockerVariant).ToList();

    /// <summary>Returns a preset by alias, or null.</summary>
    public static CliToolPreset? GetPresetByAlias(string alias) =>
        AllPresetsInternal.FirstOrDefault(p => string.Equals(p.Alias, alias, StringComparison.OrdinalIgnoreCase));
}
