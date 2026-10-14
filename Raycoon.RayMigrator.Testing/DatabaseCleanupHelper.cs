using Raycoon.RayMigrator.Database;
using Raycoon.RayMigrator.Database.Common;
using Raycoon.RayMigrator.Repository.ReadModel;

namespace Raycoon.RayMigrator.Testing;

/// <summary>
/// Drops all repository and user-created tables for clean integration test runs.
/// Respects FK constraint ordering. The repository tables are named through <see cref="RepositoryNaming"/>, so a
/// repository with a table prefix (<c>TableBaseName</c>) is dropped when the prefix is passed.
/// </summary>
public static class DatabaseCleanupHelper
{
    private static readonly DalSettings CleanupSettings = new()
    {
        UseTransaction = false,
        DbCommandTimeoutInSeconds = 30
    };

    /// <summary>The repository tables in an order that drops every table before the tables it references.</summary>
    private static readonly RepositoryTable[] RepositoryTablesInDropOrder =
    [
        RepositoryTable.MigrationLog, RepositoryTable.MigrationEvent, RepositoryTable.MigrationRecordHistory,
        RepositoryTable.MigrationRecord, RepositoryTable.MigrationRunMeta, RepositoryTable.MigrationRun,
        RepositoryTable.Environment, RepositoryTable.Product, RepositoryTable.MigratorMeta, RepositoryTable.MigrationStatus,
        RepositoryTable.MigrationRunResult, RepositoryTable.MigrationOperation, RepositoryTable.MigrationRunMode,
        RepositoryTable.MigrationRunSource, RepositoryTable.RepositorySchemaVersion
    ];

    /// <summary>
    /// Cleans the specified database by dropping all test-related objects: the engine tests' user tables and the
    /// repository tables of the given schema and table prefix (<paramref name="tableBaseName"/>, empty for none).
    /// </summary>
    public static void CleanDatabase(string databaseType, string connectionString, string schemaName, string tableBaseName = "")
    {
        if (!DalFactory.TryGetDal(databaseType, connectionString, out IDal? dal))
            return;

        string sql = databaseType switch
        {
            "SqlServer" => GetSqlServerCleanupSql(schemaName, tableBaseName),
            "PostgreSQL" => GetPostgreSqlCleanupSql(schemaName),
            "MariaDb" or "MySql" => GetMySqlFamilyCleanupSql(databaseType, tableBaseName),
            "Sqlite" => GetSqliteCleanupSql(tableBaseName),
            _ => throw new NotSupportedException($"Unsupported database type: {databaseType}")
        };

        try
        {
            dal!.ExecuteNonQuery(sql, CleanupSettings, null);
        }
        catch
        {
            // Tables may not exist on first run - that's fine
        }
    }

    /// <summary>
    /// Cleans all target databases (Backend_1, Backend_2, Frontend) for the specified engine.
    /// </summary>
    public static void CleanAllDatabases(string databaseType, string[] connectionStrings, string schemaName, string tableBaseName = "")
    {
        foreach (string cs in connectionStrings)
        {
            CleanDatabase(databaseType, cs, schemaName, tableBaseName);
        }
    }

    /// <summary>The repository tables of the prefix as the engine names them on this database, in drop order.</summary>
    private static IEnumerable<string> RepositoryTableNames(string databaseType, string schemaName, string tableBaseName)
    {
        var naming = new RepositoryNaming(databaseType, string.IsNullOrEmpty(schemaName) ? null : schemaName, tableBaseName);
        return RepositoryTablesInDropOrder.Select(naming.TableName);
    }

    private static string GetSqlServerCleanupSql(string schemaName, string tableBaseName)
    {
        string repositoryDrops = string.Join(Environment.NewLine, RepositoryTableNames("SqlServer", schemaName, tableBaseName)
            .Select(table => $"IF OBJECT_ID('{schemaName}.{table}', 'U') IS NOT NULL DROP TABLE [{schemaName}].[{table}];"));

        return $@"
-- Drop user tables (FK order)
IF OBJECT_ID('{schemaName}.UserPreferences', 'U') IS NOT NULL DROP TABLE [{schemaName}].[UserPreferences];
IF OBJECT_ID('{schemaName}.UserProfile', 'U') IS NOT NULL DROP TABLE [{schemaName}].[UserProfile];
IF OBJECT_ID('{schemaName}.Person', 'U') IS NOT NULL DROP TABLE [{schemaName}].[Person];
IF OBJECT_ID('{schemaName}.Login', 'U') IS NOT NULL DROP TABLE [{schemaName}].[Login];
IF OBJECT_ID('{schemaName}.Sex', 'U') IS NOT NULL DROP TABLE [{schemaName}].[Sex];

IF OBJECT_ID('dbo.UserPreferences', 'U') IS NOT NULL DROP TABLE [dbo].[UserPreferences];
IF OBJECT_ID('dbo.UserProfile', 'U') IS NOT NULL DROP TABLE [dbo].[UserProfile];
IF OBJECT_ID('dbo.Person', 'U') IS NOT NULL DROP TABLE [dbo].[Person];
IF OBJECT_ID('dbo.Login', 'U') IS NOT NULL DROP TABLE [dbo].[Login];
IF OBJECT_ID('dbo.Sex', 'U') IS NOT NULL DROP TABLE [dbo].[Sex];
IF OBJECT_ID('dbo.MigSettingsMarker', 'U') IS NOT NULL DROP TABLE [dbo].[MigSettingsMarker];
IF OBJECT_ID('dbo.ResumeProbe', 'U') IS NOT NULL DROP TABLE [dbo].[ResumeProbe];

IF OBJECT_ID('dbo.TableY4', 'U') IS NOT NULL DROP TABLE [dbo].[TableY4];
IF OBJECT_ID('dbo.TableX4', 'U') IS NOT NULL DROP TABLE [dbo].[TableX4];
IF OBJECT_ID('dbo.TableY3', 'U') IS NOT NULL DROP TABLE [dbo].[TableY3];
IF OBJECT_ID('dbo.TableX3', 'U') IS NOT NULL DROP TABLE [dbo].[TableX3];
IF OBJECT_ID('dbo.TableY2', 'U') IS NOT NULL DROP TABLE [dbo].[TableY2];
IF OBJECT_ID('dbo.TableX2', 'U') IS NOT NULL DROP TABLE [dbo].[TableX2];
IF OBJECT_ID('dbo.TableY1', 'U') IS NOT NULL DROP TABLE [dbo].[TableY1];
IF OBJECT_ID('dbo.TableX1', 'U') IS NOT NULL DROP TABLE [dbo].[TableX1];

IF OBJECT_ID('dbo.TableH', 'U') IS NOT NULL DROP TABLE [dbo].[TableH];
IF OBJECT_ID('dbo.TableG', 'U') IS NOT NULL DROP TABLE [dbo].[TableG];
IF OBJECT_ID('dbo.TableF', 'U') IS NOT NULL DROP TABLE [dbo].[TableF];
IF OBJECT_ID('dbo.TableE', 'U') IS NOT NULL DROP TABLE [dbo].[TableE];
IF OBJECT_ID('dbo.TableD', 'U') IS NOT NULL DROP TABLE [dbo].[TableD];
IF OBJECT_ID('dbo.TableC', 'U') IS NOT NULL DROP TABLE [dbo].[TableC];
IF OBJECT_ID('dbo.TableB', 'U') IS NOT NULL DROP TABLE [dbo].[TableB];
IF OBJECT_ID('dbo.TableA', 'U') IS NOT NULL DROP TABLE [dbo].[TableA];

-- Drop repository tables (FK order)
{repositoryDrops}

-- Drop whatever else the schema holds (repositories with another prefix): foreign keys first, then the tables, then the schema
IF EXISTS (SELECT 1 FROM sys.schemas WHERE name = '{schemaName}')
BEGIN
    DECLARE @fk NVARCHAR(MAX) = '';
    SELECT @fk = @fk + 'ALTER TABLE [{schemaName}].[' + OBJECT_NAME(parent_object_id) + '] DROP CONSTRAINT [' + name + '];'
    FROM sys.foreign_keys WHERE schema_id = SCHEMA_ID('{schemaName}');
    EXEC sp_executesql @fk;
    DECLARE @sql NVARCHAR(MAX) = '';
    SELECT @sql = @sql + 'DROP TABLE [{schemaName}].[' + name + '];' FROM sys.tables WHERE schema_id = SCHEMA_ID('{schemaName}');
    EXEC sp_executesql @sql;
    EXEC('DROP SCHEMA [{schemaName}]');
END
";
    }

    private static string GetPostgreSqlCleanupSql(string schemaName)
    {
        // User tables are in public schema with lowercase names (created without quoting)
        // Repository tables are in ray schema with PascalCase names (created with quoting)
        return $@"
DROP TABLE IF EXISTS public.userpreferences CASCADE;
DROP TABLE IF EXISTS public.userprofile CASCADE;
DROP TABLE IF EXISTS public.person CASCADE;
DROP TABLE IF EXISTS public.login CASCADE;
DROP TABLE IF EXISTS public.sex CASCADE;
DROP TABLE IF EXISTS public.migsettingsmarker CASCADE;
DROP TABLE IF EXISTS public.inheritancemarker_alpha CASCADE;
DROP TABLE IF EXISTS public.inheritancemarker_beta CASCADE;
DROP TABLE IF EXISTS public.errortest_data CASCADE;
DROP TABLE IF EXISTS public.rbtest_a CASCADE;
DROP TABLE IF EXISTS public.rbtest_b CASCADE;
DROP TABLE IF EXISTS public.rbtest_trigger_error CASCADE;
DROP TABLE IF EXISTS public.tabley4 CASCADE;
DROP TABLE IF EXISTS public.tablex4 CASCADE;
DROP TABLE IF EXISTS public.tabley3 CASCADE;
DROP TABLE IF EXISTS public.tablex3 CASCADE;
DROP TABLE IF EXISTS public.tabley2 CASCADE;
DROP TABLE IF EXISTS public.tablex2 CASCADE;
DROP TABLE IF EXISTS public.tabley1 CASCADE;
DROP TABLE IF EXISTS public.tablex1 CASCADE;
DROP TABLE IF EXISTS public.tableh CASCADE;
DROP TABLE IF EXISTS public.tableg CASCADE;
DROP TABLE IF EXISTS public.tablef CASCADE;
DROP TABLE IF EXISTS public.tablee CASCADE;
DROP TABLE IF EXISTS public.tabled CASCADE;
DROP TABLE IF EXISTS public.tablec CASCADE;
DROP TABLE IF EXISTS public.tableb CASCADE;
DROP TABLE IF EXISTS public.tablea CASCADE;
DROP SCHEMA IF EXISTS {schemaName} CASCADE;
";
    }

    private static string GetMySqlFamilyCleanupSql(string databaseType, string tableBaseName)
    {
        // Schema = database on MySQL and MariaDB: the statements run in the connection's current database.
        string repositoryDrops = string.Join(Environment.NewLine, RepositoryTableNames(databaseType, "", tableBaseName)
            .Select(table => $"DROP TABLE IF EXISTS `{table}`;"));

        return $@"
SET FOREIGN_KEY_CHECKS = 0;

DROP TABLE IF EXISTS `UserPreferences`;
DROP TABLE IF EXISTS `UserProfile`;
DROP TABLE IF EXISTS `Person`;
DROP TABLE IF EXISTS `Login`;
DROP TABLE IF EXISTS `Sex`;
DROP TABLE IF EXISTS `MigSettingsMarker`;
DROP TABLE IF EXISTS `tabley4`;
DROP TABLE IF EXISTS `tablex4`;
DROP TABLE IF EXISTS `tabley3`;
DROP TABLE IF EXISTS `tablex3`;
DROP TABLE IF EXISTS `tabley2`;
DROP TABLE IF EXISTS `tablex2`;
DROP TABLE IF EXISTS `tabley1`;
DROP TABLE IF EXISTS `tablex1`;
DROP TABLE IF EXISTS `tableh`;
DROP TABLE IF EXISTS `tableg`;
DROP TABLE IF EXISTS `tablef`;
DROP TABLE IF EXISTS `tablee`;
DROP TABLE IF EXISTS `tabled`;
DROP TABLE IF EXISTS `tablec`;
DROP TABLE IF EXISTS `tableb`;
DROP TABLE IF EXISTS `tablea`;

-- DAL-018: snake_case repository table names, with the prefix
{repositoryDrops}

SET FOREIGN_KEY_CHECKS = 1;
";
    }

    private static string GetSqliteCleanupSql(string tableBaseName)
    {
        string repositoryDrops = string.Join(Environment.NewLine, RepositoryTableNames("Sqlite", "", tableBaseName)
            .Select(table => $"DROP TABLE IF EXISTS \"{table}\";"));

        return $@"
DROP TABLE IF EXISTS tabley4;
DROP TABLE IF EXISTS tablex4;
DROP TABLE IF EXISTS tabley3;
DROP TABLE IF EXISTS tablex3;
DROP TABLE IF EXISTS tabley2;
DROP TABLE IF EXISTS tablex2;
DROP TABLE IF EXISTS tabley1;
DROP TABLE IF EXISTS tablex1;
DROP TABLE IF EXISTS tableh;
DROP TABLE IF EXISTS tableg;
DROP TABLE IF EXISTS tablef;
DROP TABLE IF EXISTS tablee;
DROP TABLE IF EXISTS tabled;
DROP TABLE IF EXISTS tablec;
DROP TABLE IF EXISTS tableb;
DROP TABLE IF EXISTS tablea;
DROP TABLE IF EXISTS UserPreferences;
DROP TABLE IF EXISTS UserProfile;
DROP TABLE IF EXISTS Person;
DROP TABLE IF EXISTS Login;
DROP TABLE IF EXISTS Sex;
DROP TABLE IF EXISTS MigSettingsMarker;
{repositoryDrops}
";
    }
}
