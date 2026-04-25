namespace Raycoon.RayMigrator.Tests.Engine.Infrastructure;

/// <summary>
/// Provides pre-built CLI tool configurations per database type for engine tests.
/// Stdin mode uses 'docker exec -i' to pipe content into the container.
/// File mode uses '/bin/bash -c "cat ... | docker exec -i ..."' as a host-to-container bridge.
/// </summary>
public static class CliToolConfigHelper
{
    public record CliToolConfig(
        string Alias,
        string ExecutablePath,
        string ArgumentTemplate,
        string InputMode,
        int TimeoutInSeconds,
        Dictionary<string, string> Parameters);

    /// <summary>
    /// Returns a CLI tool config that uses Stdin mode (CliToolExecutor pipes content to docker exec -i).
    /// </summary>
    public static CliToolConfig GetStdinConfig(string databaseType, string connectionString)
    {
        var conn = ParseConnectionString(connectionString);

        return databaseType switch
        {
            "PostgreSQL" => new CliToolConfig(
                Alias: "psql-stdin",
                ExecutablePath: "docker",
                ArgumentTemplate: "exec -i rm_db_postgresql psql --set ON_ERROR_STOP=1 -U {User} -d {Database}",
                InputMode: "Stdin",
                TimeoutInSeconds: 30,
                Parameters: new Dictionary<string, string>
                {
                    ["User"] = conn.GetValueOrDefault("Username", "postgres"),
                    ["Database"] = conn.GetValueOrDefault("Database", "raydb")
                }),

            "MariaDb" => new CliToolConfig(
                Alias: "mariadb-stdin",
                ExecutablePath: "docker",
                ArgumentTemplate: "exec -i rm_db_mariadb mariadb -u {User} -p{Password} {Database}",
                InputMode: "Stdin",
                TimeoutInSeconds: 30,
                Parameters: new Dictionary<string, string>
                {
                    ["User"] = conn.GetValueOrDefault("User Id", "rayuser"),
                    ["Password"] = conn.GetValueOrDefault("Password", "raypass123"),
                    ["Database"] = conn.GetValueOrDefault("Database", "raydb")
                }),

            "MySql" => new CliToolConfig(
                Alias: "mysql-stdin",
                ExecutablePath: "docker",
                ArgumentTemplate: "exec -i rm_db_mysql mysql -u {User} -p{Password} {Database}",
                InputMode: "Stdin",
                TimeoutInSeconds: 30,
                Parameters: new Dictionary<string, string>
                {
                    ["User"] = conn.GetValueOrDefault("User Id", "rayuser"),
                    ["Password"] = conn.GetValueOrDefault("Password", "raypass123"),
                    ["Database"] = conn.GetValueOrDefault("Database", "raydb")
                }),

            "SqlServer" => new CliToolConfig(
                Alias: "sqlcmd-stdin",
                ExecutablePath: "docker",
                ArgumentTemplate: "exec -i rm_db_sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P {Password} -C -d {Database} -b",
                InputMode: "Stdin",
                TimeoutInSeconds: 30,
                Parameters: new Dictionary<string, string>
                {
                    ["Password"] = conn.GetValueOrDefault("Password", "P@ssw0rd!"),
                    ["Database"] = conn.GetValueOrDefault("Initial Catalog", "Backend_1")
                }),

            _ => throw new ArgumentException($"Unsupported database type: {databaseType}")
        };
    }

    /// <summary>
    /// Returns a CLI tool config that uses File mode (bash wrapper reads host file and pipes into docker exec).
    /// From CliToolExecutor's perspective this IS File mode (no stdin redirect). The bash command handles host-to-container bridging.
    /// </summary>
    public static CliToolConfig GetFileConfig(string databaseType, string connectionString)
    {
        var conn = ParseConnectionString(connectionString);

        return databaseType switch
        {
            "PostgreSQL" => new CliToolConfig(
                Alias: "psql-file",
                ExecutablePath: "/bin/bash",
                ArgumentTemplate: "-c \"cat '{FilePath}' | docker exec -i rm_db_postgresql psql --set ON_ERROR_STOP=1 -U {User} -d {Database}\"",
                InputMode: "File",
                TimeoutInSeconds: 30,
                Parameters: new Dictionary<string, string>
                {
                    ["User"] = conn.GetValueOrDefault("Username", "postgres"),
                    ["Database"] = conn.GetValueOrDefault("Database", "raydb")
                }),

            "MariaDb" => new CliToolConfig(
                Alias: "mariadb-file",
                ExecutablePath: "/bin/bash",
                ArgumentTemplate: "-c \"cat '{FilePath}' | docker exec -i rm_db_mariadb mariadb -u {User} -p{Password} {Database}\"",
                InputMode: "File",
                TimeoutInSeconds: 30,
                Parameters: new Dictionary<string, string>
                {
                    ["User"] = conn.GetValueOrDefault("User Id", "rayuser"),
                    ["Password"] = conn.GetValueOrDefault("Password", "raypass123"),
                    ["Database"] = conn.GetValueOrDefault("Database", "raydb")
                }),

            "MySql" => new CliToolConfig(
                Alias: "mysql-file",
                ExecutablePath: "/bin/bash",
                ArgumentTemplate: "-c \"cat '{FilePath}' | docker exec -i rm_db_mysql mysql -u {User} -p{Password} {Database}\"",
                InputMode: "File",
                TimeoutInSeconds: 30,
                Parameters: new Dictionary<string, string>
                {
                    ["User"] = conn.GetValueOrDefault("User Id", "rayuser"),
                    ["Password"] = conn.GetValueOrDefault("Password", "raypass123"),
                    ["Database"] = conn.GetValueOrDefault("Database", "raydb")
                }),

            "SqlServer" => new CliToolConfig(
                Alias: "sqlcmd-file",
                ExecutablePath: "/bin/bash",
                ArgumentTemplate: "-c \"cat '{FilePath}' | docker exec -i rm_db_sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P {Password} -C -d {Database} -b\"",
                InputMode: "File",
                TimeoutInSeconds: 30,
                Parameters: new Dictionary<string, string>
                {
                    ["Password"] = conn.GetValueOrDefault("Password", "P@ssw0rd!"),
                    ["Database"] = conn.GetValueOrDefault("Initial Catalog", "Backend_1")
                }),

            _ => throw new ArgumentException($"Unsupported database type: {databaseType}")
        };
    }

    private static Dictionary<string, string> ParseConnectionString(string connectionString)
    {
        return connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .Where(kv => kv.Length == 2)
            .ToDictionary(kv => kv[0].Trim(), kv => kv[1].Trim(), StringComparer.OrdinalIgnoreCase);
    }
}
