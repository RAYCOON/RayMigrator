using Raycoon.RayMigrator.Database;
using Raycoon.RayMigrator.Database.Common;

namespace Raycoon.RayMigrator.Testing;

/// <summary>
/// Drops all repository and user-created tables for clean integration test runs.
/// Respects FK constraint ordering.
/// </summary>
public static class DatabaseCleanupHelper
{
    private static readonly DalSettings CleanupSettings = new()
    {
        UseTransaction = false,
        DbCommandTimeoutInSeconds = 30
    };

    /// <summary>
    /// Cleans the specified database by dropping all test-related objects.
    /// </summary>
    public static void CleanDatabase(string databaseType, string connectionString, string schemaName)
    {
        if (!DalFactory.TryGetDal(databaseType, connectionString, out IDal? dal))
            return;

        string sql = databaseType switch
        {
            "SqlServer" => GetSqlServerCleanupSql(schemaName),
            "PostgreSQL" => GetPostgreSqlCleanupSql(schemaName),
            "MariaDb" => GetMariaDbCleanupSql(schemaName, connectionString),
            "MySql" => GetMySqlCleanupSql(schemaName, connectionString),
            "Sqlite" => GetSqliteCleanupSql(),
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
    public static void CleanAllDatabases(string databaseType, string[] connectionStrings, string schemaName)
    {
        foreach (string cs in connectionStrings)
        {
            CleanDatabase(databaseType, cs, schemaName);
        }
    }

    private static string GetSqlServerCleanupSql(string schemaName)
    {
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

-- Drop repository tables (FK order) — new names after Migration→MigrationRecord rename
IF OBJECT_ID('{schemaName}.MigrationLog', 'U') IS NOT NULL DROP TABLE [{schemaName}].[MigrationLog];
IF OBJECT_ID('{schemaName}.MigrationEvent', 'U') IS NOT NULL DROP TABLE [{schemaName}].[MigrationEvent];
IF OBJECT_ID('{schemaName}.MigrationRecordHistory', 'U') IS NOT NULL DROP TABLE [{schemaName}].[MigrationRecordHistory];
IF OBJECT_ID('{schemaName}.MigrationRecord', 'U') IS NOT NULL DROP TABLE [{schemaName}].[MigrationRecord];
-- Legacy table names (pre-rename) for cleanup of old test databases
IF OBJECT_ID('{schemaName}.MigrationHistory', 'U') IS NOT NULL DROP TABLE [{schemaName}].[MigrationHistory];
IF OBJECT_ID('{schemaName}.Migration', 'U') IS NOT NULL DROP TABLE [{schemaName}].[Migration];
IF OBJECT_ID('{schemaName}.MigrationRunMeta', 'U') IS NOT NULL DROP TABLE [{schemaName}].[MigrationRunMeta];
IF OBJECT_ID('{schemaName}.MigrationRun', 'U') IS NOT NULL DROP TABLE [{schemaName}].[MigrationRun];
IF OBJECT_ID('{schemaName}.Environment', 'U') IS NOT NULL DROP TABLE [{schemaName}].[Environment];
IF OBJECT_ID('{schemaName}.Product', 'U') IS NOT NULL DROP TABLE [{schemaName}].[Product];
IF OBJECT_ID('{schemaName}.MigratorMeta', 'U') IS NOT NULL DROP TABLE [{schemaName}].[MigratorMeta];
IF OBJECT_ID('{schemaName}.MigrationState', 'U') IS NOT NULL DROP TABLE [{schemaName}].[MigrationState];
IF OBJECT_ID('{schemaName}.MigrationStatus', 'U') IS NOT NULL DROP TABLE [{schemaName}].[MigrationStatus];
IF OBJECT_ID('{schemaName}.MigrationRunResult', 'U') IS NOT NULL DROP TABLE [{schemaName}].[MigrationRunResult];
IF OBJECT_ID('{schemaName}.MigrationOperation', 'U') IS NOT NULL DROP TABLE [{schemaName}].[MigrationOperation];
IF OBJECT_ID('{schemaName}.MigrationRunMode', 'U') IS NOT NULL DROP TABLE [{schemaName}].[MigrationRunMode];
IF OBJECT_ID('{schemaName}.LogLevel', 'U') IS NOT NULL DROP TABLE [{schemaName}].[LogLevel];

-- Drop schema if it exists and is empty
IF EXISTS (SELECT 1 FROM sys.schemas WHERE name = '{schemaName}')
BEGIN
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

    private static string GetMariaDbCleanupSql(string schemaName, string connectionString)
    {
        // For MariaDB, schema = database. Drop all known tables.
        string dbName = ExtractDatabaseName(connectionString);
        return GetMySqlFamilyCleanupSql(dbName);
    }

    private static string GetMySqlCleanupSql(string schemaName, string connectionString)
    {
        string dbName = ExtractDatabaseName(connectionString);
        return GetMySqlFamilyCleanupSql(dbName);
    }

    private static string GetMySqlFamilyCleanupSql(string dbName)
    {
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

-- Legacy PascalCase repository table names (pre-DAL-018) for cleanup of old test databases
DROP TABLE IF EXISTS `MigrationLog`;
DROP TABLE IF EXISTS `MigrationEvent`;
DROP TABLE IF EXISTS `MigrationRecordHistory`;
DROP TABLE IF EXISTS `MigrationRecord`;
DROP TABLE IF EXISTS `MigrationHistory`;
DROP TABLE IF EXISTS `Migration`;
DROP TABLE IF EXISTS `MigrationRunMeta`;
DROP TABLE IF EXISTS `MigrationRun`;
DROP TABLE IF EXISTS `Environment`;
DROP TABLE IF EXISTS `Product`;
DROP TABLE IF EXISTS `MigratorMeta`;
DROP TABLE IF EXISTS `MigrationState`;
DROP TABLE IF EXISTS `MigrationStatus`;
DROP TABLE IF EXISTS `MigrationRunResult`;
DROP TABLE IF EXISTS `MigrationOperation`;
DROP TABLE IF EXISTS `MigrationRunMode`;
DROP TABLE IF EXISTS `LogLevel`;

-- DAL-018: snake_case repository table names
DROP TABLE IF EXISTS migration_log;
DROP TABLE IF EXISTS migration_event;
DROP TABLE IF EXISTS migration_record_history;
DROP TABLE IF EXISTS migration_record;
DROP TABLE IF EXISTS migration_run_meta;
DROP TABLE IF EXISTS migration_run;
DROP TABLE IF EXISTS environment;
DROP TABLE IF EXISTS product;
DROP TABLE IF EXISTS migrator_meta;
DROP TABLE IF EXISTS migration_status;
DROP TABLE IF EXISTS migration_run_result;
DROP TABLE IF EXISTS migration_operation;
DROP TABLE IF EXISTS migration_run_mode;

SET FOREIGN_KEY_CHECKS = 1;
";
    }

    private static string GetSqliteCleanupSql()
    {
        return @"
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
DROP TABLE IF EXISTS MigrationLog;
DROP TABLE IF EXISTS MigrationEvent;
DROP TABLE IF EXISTS MigrationRecordHistory;
DROP TABLE IF EXISTS MigrationRecord;
DROP TABLE IF EXISTS MigrationHistory;
DROP TABLE IF EXISTS Migration;
DROP TABLE IF EXISTS MigrationRunMeta;
DROP TABLE IF EXISTS MigrationRun;
DROP TABLE IF EXISTS Environment;
DROP TABLE IF EXISTS Product;
DROP TABLE IF EXISTS MigratorMeta;
DROP TABLE IF EXISTS MigrationState;
DROP TABLE IF EXISTS MigrationStatus;
DROP TABLE IF EXISTS MigrationRunResult;
DROP TABLE IF EXISTS MigrationOperation;
DROP TABLE IF EXISTS MigrationRunMode;
DROP TABLE IF EXISTS LogLevel;
";
    }

    private static string ExtractDatabaseName(string connectionString)
    {
        var parts = connectionString.Split(';');
        foreach (var part in parts)
        {
            var kv = part.Split('=', 2);
            if (kv.Length == 2 && kv[0].Trim().Equals("Database", StringComparison.OrdinalIgnoreCase))
                return kv[1].Trim();
        }
        return string.Empty;
    }
}
