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
    /// Returns a DROP of a table that <see cref="GetCreateSimpleTableSql"/> creates, which does nothing when the table
    /// does not exist.
    /// </summary>
    public static string GetDropSimpleTableIfExistsSql(string databaseType, string tableName)
    {
        return databaseType switch
        {
            "SqlServer" => $"IF OBJECT_ID('[dbo].[{tableName}]', 'U') IS NOT NULL DROP TABLE [dbo].[{tableName}];",
            "PostgreSQL" => $"DROP TABLE IF EXISTS public.{tableName.ToLower()} CASCADE;",
            "MariaDb" or "MySql" => $"DROP TABLE IF EXISTS `{tableName.ToLower()}`;",
            "Sqlite" => $"DROP TABLE IF EXISTS {tableName.ToLower()};",
            _ => throw new NotSupportedException($"Unsupported database type: {databaseType}")
        };
    }

    /// <summary>
    /// Returns an INSERT of one row into a table that <see cref="GetCreateSimpleTableSql"/> creates. As a block of a
    /// migration file it fails while the table does not exist and succeeds once a test created it, so the file keeps its
    /// hash between two runs.
    /// </summary>
    public static string GetInsertIntoSimpleTableSql(string databaseType, string tableName, string name)
    {
        return databaseType switch
        {
            "SqlServer" => $"INSERT INTO [dbo].[{tableName}] ([Name]) VALUES ('{name}')",
            "PostgreSQL" => $"INSERT INTO public.{tableName.ToLower()} (name) VALUES ('{name}')",
            "MariaDb" or "MySql" => $"INSERT INTO `{tableName.ToLower()}` (Name) VALUES ('{name}')",
            "Sqlite" => $"INSERT INTO {tableName.ToLower()} (Name) VALUES ('{name}')",
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

    /// <summary>
    /// Joins SQL statements into the body of a migration file whose every statement is a block of its own: the engine
    /// splits a file at a line holding only its block delimiter, GO on SQL Server and a semicolon elsewhere.
    /// </summary>
    public static string JoinBlocks(string databaseType, params string[] blocks)
        => string.Join(databaseType == "SqlServer" ? "\nGO\n" : "\n;\n", blocks);

    /// <summary>Returns an INSERT of one row into the TableA of the fixtures (created by 01_CreateTableA.sql).</summary>
    public static string GetInsertIntoTableASql(string databaseType, string name, int value)
    {
        return databaseType switch
        {
            "SqlServer" => $"INSERT INTO [dbo].[TableA] ([Name], [Value]) VALUES ('{name}', {value})",
            "PostgreSQL" or "MariaDb" or "MySql" or "Sqlite" => $"INSERT INTO tablea (name, value) VALUES ('{name}', {value})",
            _ => throw new NotSupportedException($"Unsupported database type: {databaseType}")
        };
    }

    /// <summary>Returns a DELETE of one row from the TableC of the fixtures (created by Release_2.0/01_CreateTableC.sql).</summary>
    public static string GetDeleteFromTableCSql(string databaseType, string title)
    {
        return databaseType switch
        {
            "SqlServer" => $"DELETE FROM [dbo].[TableC] WHERE [Title] = '{title}'",
            "PostgreSQL" or "MariaDb" or "MySql" or "Sqlite" => $"DELETE FROM tablec WHERE title = '{title}'",
            _ => throw new NotSupportedException($"Unsupported database type: {databaseType}")
        };
    }

    /// <summary>
    /// Returns SQL that keeps a block busy for about <paramref name="seconds"/> seconds, so a run lasts long enough
    /// for its heartbeat to fire. SQLite has no sleep; a recursive CTE burns the time instead.
    /// </summary>
    public static string GetSleepSql(string databaseType, int seconds)
    {
        return databaseType switch
        {
            "SqlServer" => $"WAITFOR DELAY '00:00:{seconds:00}';",
            "PostgreSQL" => $"SELECT pg_sleep({seconds});",
            "MariaDb" or "MySql" => $"DO SLEEP({seconds});",
            "Sqlite" => $"WITH RECURSIVE busy(x) AS (SELECT 1 UNION ALL SELECT x + 1 FROM busy WHERE x < {seconds * 13000000}) SELECT COUNT(*) FROM busy;",
            _ => throw new NotSupportedException($"Unsupported database type: {databaseType}")
        };
    }
}

