
using System.Text;

namespace Raycoon.RayMigrator.Tests.Engine.Infrastructure;

/// <summary>
/// Provides database-dialect-specific SQL fragments for engine test scenarios.
/// </summary>
public static class SqlDialect
{
    /// <summary>
    /// Returns SQL that will fail at runtime (INSERT into a nonexistent table).
    /// Used by ScenarioBuilder.InjectError to create a migration file that triggers an error.
    /// </summary>
    public static string GetErrorSql(string databaseType)
    {
        return databaseType switch
        {
            "SqlServer" => "INSERT INTO [dbo].[NonExistentTable_Error] (Id) VALUES (1);",
            "PostgreSQL" => "INSERT INTO public.nonexistenttable_error (id) VALUES (1);",
            "MariaDb" or "MySql" => "INSERT INTO `NonExistentTable_Error` (Id) VALUES (1);",
            "Sqlite" => "INSERT INTO NonExistentTable_Error (Id) VALUES (1);",
            _ => throw new NotSupportedException($"Unsupported database type: {databaseType}")
        };
    }

    /// <summary>
    /// Returns SQL that will fail at runtime (DROP a nonexistent table).
    /// Used by ScenarioBuilder.BreakRollback to create a rollback file that triggers an error.
    /// </summary>
    public static string GetBrokenRollbackSql(string databaseType)
    {
        return databaseType switch
        {
            "SqlServer" => "DROP TABLE [dbo].[NonExistentTable_BrokenRollback];",
            "PostgreSQL" => "DROP TABLE public.nonexistenttable_brokenrollback;",
            "MariaDb" or "MySql" => "DROP TABLE `NonExistentTable_BrokenRollback`;",
            "Sqlite" => "DROP TABLE NonExistentTable_BrokenRollback;",
            _ => throw new NotSupportedException($"Unsupported database type: {databaseType}")
        };
    }

    /// <summary>
    /// Returns CREATE TABLE SQL for a simple test table.
    /// Used by OutOfOrderBlockingTests to add a migration file at runtime.
    /// </summary>
    public static string GetCreateSimpleTableSql(string databaseType, string tableName)
    {
        // Uses DROP IF EXISTS before CREATE to handle leftover tables from previous test runs
        // (DatabaseCleanupHelper uses a hardcoded table list that doesn't include dynamically created tables)
        return databaseType switch
        {
            "SqlServer" => $"IF OBJECT_ID('[dbo].[{tableName}]', 'U') IS NOT NULL DROP TABLE [dbo].[{tableName}];\nCREATE TABLE [dbo].[{tableName}] ([Id] INT IDENTITY(1,1) PRIMARY KEY, [Name] NVARCHAR(100) NOT NULL);",
            "PostgreSQL" => $"DROP TABLE IF EXISTS public.{tableName.ToLower()} CASCADE;\nCREATE TABLE public.{tableName.ToLower()} (id SERIAL PRIMARY KEY, name VARCHAR(100) NOT NULL);",
            "MariaDb" or "MySql" => $"DROP TABLE IF EXISTS `{tableName.ToLower()}`;\nCREATE TABLE `{tableName.ToLower()}` (Id INT AUTO_INCREMENT PRIMARY KEY, Name VARCHAR(100) NOT NULL);",
            "Sqlite" => $"DROP TABLE IF EXISTS {tableName.ToLower()};\nCREATE TABLE {tableName.ToLower()} (Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT NOT NULL);",
            _ => throw new NotSupportedException($"Unsupported database type: {databaseType}")
        };
    }

    /// <summary>
    /// Returns DROP TABLE SQL for a simple test table.
    /// Used by OutOfOrderBlockingTests for rollback files.
    /// </summary>
    public static string GetDropSimpleTableSql(string databaseType, string tableName)
    {
        return databaseType switch
        {
            "SqlServer" => $"DROP TABLE [dbo].[{tableName}];",
            "PostgreSQL" => $"DROP TABLE public.{tableName.ToLower()};",
            "MariaDb" or "MySql" => $"DROP TABLE `{tableName.ToLower()}`;",
            "Sqlite" => $"DROP TABLE {tableName.ToLower()};",
            _ => throw new NotSupportedException($"Unsupported database type: {databaseType}")
        };
    }

    /// <summary>
    /// Generates a TOML metadata header block for migration files.
    /// Only emits non-default fields. Returns empty string if all values are defaults and description is empty.
    /// </summary>
    public static string GetTomlHeader(string description, bool useTransaction = true, bool runAlways = false)
    {
        bool hasDescription = !string.IsNullOrEmpty(description);
        bool hasNonDefaults = !useTransaction || runAlways;

        if (!hasDescription && !hasNonDefaults)
            return string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine("/*");
        sb.AppendLine("[RayMigrator]");
        if (hasDescription)
            sb.AppendLine($"Description = \"{description}\"");
        if (!useTransaction)
            sb.AppendLine("UseTransaction = false");
        if (runAlways)
            sb.AppendLine("RunAlways = true");
        sb.AppendLine("*/");
        return sb.ToString();
    }
}
