using FluentAssertions;
using Microsoft.Extensions.Logging;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Core.Models;
using Raycoon.RayMigrator.Services;
using Raycoon.RayMigrator.Tests.Unit.Helpers;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1: OPT-3 tests for LogMigrationSafetyWarnings.
/// Validates that dangerous configuration combinations are detected and logged
/// as warnings before migration execution begins.
/// Rule IDs tested: 2.1, 2.2, 2.6, 2.7, 2.8, 2.9, 2.10, 2.12.
/// </summary>
public class MigrationSafetyWarningTests
{
    private static (CapturingLogger<MigrationService> Logger, MigrationService Service) CreateServiceWithLogger()
    {
        var (service, logger) = TestFactories.CreateMigrationServiceWithCapturingLogger();
        return (logger, service);
    }

    private static void InvokeLogMigrationSafetyWarnings(
        MigrationService service,
        List<MigrationFileInfo> files,
        ProductOptions productOptions)
    {
        var method = typeof(MigrationService).GetMethod("LogMigrationSafetyWarnings",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
        method!.Invoke(service, new object[] { files, productOptions });
    }

    private static ProductOptions CreateProductOptions(
        string databaseType = "SqlServer",
        int maxRetries = 0,
        string? targetCliAlias = null,
        string migrationErrorAction = "Terminate",
        string targetMigrationOrder = "Successively",
        string hashValidationScope = "Disabled")
    {
        return new ProductOptions("rollback")
        {
            Alias = "TestProduct",
            MigrationFilesRootDirectory = "/tmp/migrations",
            MigrationErrorAction = migrationErrorAction,
            TargetGroups = new List<TargetGroupOptions>
            {
                new()
                {
                    Alias = "Backend",
                    DatabaseType = databaseType,
                    TargetMigrationOrder = targetMigrationOrder,
                    HashValidationScope = hashValidationScope,
                    Targets = new List<TargetOptions>
                    {
                        new()
                        {
                            Alias = "MainDB",
                            ConnectionString = "Server=localhost",
                            DbCommandMaxRetries = maxRetries,
                            UseCliToolAlias = targetCliAlias
                        }
                    }
                }
            }
        };
    }

    // -------------------------------------------------------------------------
    // Rule 2.9 — NO_TRANSACTION_MULTI_BLOCK
    // -------------------------------------------------------------------------

    [Fact]
    public void NoTransaction_MultiBlock_LogsWarning()
    {
        var (logger, service) = CreateServiceWithLogger();
        var files = new List<MigrationFileInfo>
        {
            new()
            {
                Filename = "10_Create.sql",
                UseTransaction = false,
                SqlBlocks = new List<string> { "CREATE TABLE A;", "CREATE TABLE B;", "CREATE TABLE C;" },
                ReleaseVersion = "Release 1.0",
                TargetGroupAlias = "Backend"
            }
        };

        InvokeLogMigrationSafetyWarnings(service, files, CreateProductOptions());

        logger.Entries.Should().Contain(e =>
            e.LogLevel == LogLevel.Warning &&
            e.Message.Contains("[Rule 2.9 NO_TRANSACTION_MULTI_BLOCK]") &&
            e.Message.Contains("UseTransaction=false") &&
            e.Message.Contains("3 SQL blocks"));
    }

    [Fact]
    public void NoTransaction_SingleBlock_NoWarning()
    {
        var (logger, service) = CreateServiceWithLogger();
        var files = new List<MigrationFileInfo>
        {
            new()
            {
                Filename = "10_Create.sql",
                UseTransaction = false,
                SqlBlocks = new List<string> { "CREATE TABLE A;" },
                ReleaseVersion = "Release 1.0",
                TargetGroupAlias = "Backend"
            }
        };

        InvokeLogMigrationSafetyWarnings(service, files, CreateProductOptions());

        logger.Entries.Should().NotContain(e =>
            e.LogLevel == LogLevel.Warning &&
            e.Message.Contains("[Rule 2.9 NO_TRANSACTION_MULTI_BLOCK]"));
    }

    [Fact]
    public void WithTransaction_MultiBlock_NoWarning()
    {
        var (logger, service) = CreateServiceWithLogger();
        var files = new List<MigrationFileInfo>
        {
            new()
            {
                Filename = "10_Create.sql",
                UseTransaction = true,
                SqlBlocks = new List<string> { "CREATE TABLE A;", "CREATE TABLE B;" },
                ReleaseVersion = "Release 1.0",
                TargetGroupAlias = "Backend"
            }
        };

        InvokeLogMigrationSafetyWarnings(service, files, CreateProductOptions());

        logger.Entries.Should().NotContain(e =>
            e.LogLevel == LogLevel.Warning &&
            e.Message.Contains("[Rule 2.9 NO_TRANSACTION_MULTI_BLOCK]"));
    }

    // -------------------------------------------------------------------------
    // Rule 2.10 — NO_TRANSACTION_WITH_RETRIES
    // -------------------------------------------------------------------------

    [Fact]
    public void NoTransaction_WithRetries_LogsWarning()
    {
        var (logger, service) = CreateServiceWithLogger();
        var files = new List<MigrationFileInfo>
        {
            new()
            {
                Filename = "10_Create.sql",
                UseTransaction = false,
                SqlBlocks = new List<string> { "INSERT INTO T VALUES (1)" },
                ReleaseVersion = "Release 1.0",
                TargetGroupAlias = "Backend"
            }
        };

        InvokeLogMigrationSafetyWarnings(service, files, CreateProductOptions(maxRetries: 3));

        logger.Entries.Should().Contain(e =>
            e.LogLevel == LogLevel.Warning &&
            e.Message.Contains("[Rule 2.10 NO_TRANSACTION_WITH_RETRIES]") &&
            e.Message.Contains("UseTransaction=false") &&
            e.Message.Contains("MaxRetries=3"));
    }

    [Fact]
    public void NoTransaction_ZeroRetries_NoRetryWarning()
    {
        var (logger, service) = CreateServiceWithLogger();
        var files = new List<MigrationFileInfo>
        {
            new()
            {
                Filename = "10_Create.sql",
                UseTransaction = false,
                SqlBlocks = new List<string> { "INSERT INTO T VALUES (1)" },
                ReleaseVersion = "Release 1.0",
                TargetGroupAlias = "Backend"
            }
        };

        InvokeLogMigrationSafetyWarnings(service, files, CreateProductOptions(maxRetries: 0));

        logger.Entries.Should().NotContain(e =>
            e.LogLevel == LogLevel.Warning &&
            e.Message.Contains("[Rule 2.10 NO_TRANSACTION_WITH_RETRIES]"));
    }

    // -------------------------------------------------------------------------
    // Rule 2.8 — DDL_ON_NON_TRANSACTIONAL_DB
    // -------------------------------------------------------------------------

    [Fact]
    public void DdlOnMariaDb_WithTransaction_LogsWarning()
    {
        var (logger, service) = CreateServiceWithLogger();
        var files = new List<MigrationFileInfo>
        {
            new()
            {
                Filename = "10_Create.sql",
                UseTransaction = true,
                SqlBlocks = new List<string> { "CREATE TABLE Users (Id INT PRIMARY KEY);" },
                ReleaseVersion = "Release 1.0",
                TargetGroupAlias = "Backend"
            }
        };

        InvokeLogMigrationSafetyWarnings(service, files, CreateProductOptions(databaseType: "MariaDb"));

        logger.Entries.Should().Contain(e =>
            e.LogLevel == LogLevel.Warning &&
            e.Message.Contains("[Rule 2.8 DDL_ON_NON_TRANSACTIONAL_DB]") &&
            e.Message.Contains("DDL") &&
            e.Message.Contains("MariaDb") &&
            e.Message.Contains("implicit COMMIT"));
    }

    [Fact]
    public void DdlOnMySql_WithTransaction_LogsWarning()
    {
        var (logger, service) = CreateServiceWithLogger();
        var files = new List<MigrationFileInfo>
        {
            new()
            {
                Filename = "10_Alter.sql",
                UseTransaction = true,
                SqlBlocks = new List<string> { "ALTER TABLE Users ADD COLUMN Email VARCHAR(255);" },
                ReleaseVersion = "Release 1.0",
                TargetGroupAlias = "Backend"
            }
        };

        InvokeLogMigrationSafetyWarnings(service, files, CreateProductOptions(databaseType: "MySql"));

        logger.Entries.Should().Contain(e =>
            e.LogLevel == LogLevel.Warning &&
            e.Message.Contains("[Rule 2.8 DDL_ON_NON_TRANSACTIONAL_DB]") &&
            e.Message.Contains("DDL") &&
            e.Message.Contains("MySql"));
    }

    [Fact]
    public void DdlOnSqlServer_WithTransaction_NoWarning()
    {
        var (logger, service) = CreateServiceWithLogger();
        var files = new List<MigrationFileInfo>
        {
            new()
            {
                Filename = "10_Create.sql",
                UseTransaction = true,
                SqlBlocks = new List<string> { "CREATE TABLE Users (Id INT PRIMARY KEY);" },
                ReleaseVersion = "Release 1.0",
                TargetGroupAlias = "Backend"
            }
        };

        InvokeLogMigrationSafetyWarnings(service, files, CreateProductOptions(databaseType: "SqlServer"));

        logger.Entries.Should().NotContain(e =>
            e.LogLevel == LogLevel.Warning &&
            e.Message.Contains("[Rule 2.8 DDL_ON_NON_TRANSACTIONAL_DB]"));
    }

    [Fact]
    public void DdlOnPostgreSQL_WithTransaction_NoWarning()
    {
        var (logger, service) = CreateServiceWithLogger();
        var files = new List<MigrationFileInfo>
        {
            new()
            {
                Filename = "10_Create.sql",
                UseTransaction = true,
                SqlBlocks = new List<string> { "CREATE TABLE Users (Id SERIAL PRIMARY KEY);" },
                ReleaseVersion = "Release 1.0",
                TargetGroupAlias = "Backend"
            }
        };

        InvokeLogMigrationSafetyWarnings(service, files, CreateProductOptions(databaseType: "PostgreSQL"));

        logger.Entries.Should().NotContain(e =>
            e.LogLevel == LogLevel.Warning &&
            e.Message.Contains("[Rule 2.8 DDL_ON_NON_TRANSACTIONAL_DB]"));
    }

    [Fact]
    public void DmlOnMariaDb_NoWarning()
    {
        // Pure DML (INSERT/UPDATE/DELETE) should not trigger DDL warning
        var (logger, service) = CreateServiceWithLogger();
        var files = new List<MigrationFileInfo>
        {
            new()
            {
                Filename = "20_InsertData.sql",
                UseTransaction = true,
                SqlBlocks = new List<string> { "INSERT INTO Users (Name) VALUES ('Test');" },
                ReleaseVersion = "Release 1.0",
                TargetGroupAlias = "Backend"
            }
        };

        InvokeLogMigrationSafetyWarnings(service, files, CreateProductOptions(databaseType: "MariaDb"));

        logger.Entries.Should().NotContain(e =>
            e.LogLevel == LogLevel.Warning &&
            e.Message.Contains("[Rule 2.8 DDL_ON_NON_TRANSACTIONAL_DB]"));
    }

    [Fact]
    public void DropStatement_DetectedAsDdl()
    {
        var (logger, service) = CreateServiceWithLogger();
        var files = new List<MigrationFileInfo>
        {
            new()
            {
                Filename = "10_Drop.sql",
                UseTransaction = true,
                SqlBlocks = new List<string> { "DROP TABLE IF EXISTS OldTable;" },
                ReleaseVersion = "Release 1.0",
                TargetGroupAlias = "Backend"
            }
        };

        InvokeLogMigrationSafetyWarnings(service, files, CreateProductOptions(databaseType: "MariaDb"));

        logger.Entries.Should().Contain(e =>
            e.LogLevel == LogLevel.Warning &&
            e.Message.Contains("[Rule 2.8 DDL_ON_NON_TRANSACTIONAL_DB]") &&
            e.Message.Contains("DDL") &&
            e.Message.Contains("implicit COMMIT"));
    }

    [Fact]
    public void TruncateStatement_DetectedAsDdl()
    {
        var (logger, service) = CreateServiceWithLogger();
        var files = new List<MigrationFileInfo>
        {
            new()
            {
                Filename = "10_Truncate.sql",
                UseTransaction = true,
                SqlBlocks = new List<string> { "TRUNCATE TABLE TempData;" },
                ReleaseVersion = "Release 1.0",
                TargetGroupAlias = "Backend"
            }
        };

        InvokeLogMigrationSafetyWarnings(service, files, CreateProductOptions(databaseType: "MySql"));

        logger.Entries.Should().Contain(e =>
            e.LogLevel == LogLevel.Warning &&
            e.Message.Contains("[Rule 2.8 DDL_ON_NON_TRANSACTIONAL_DB]") &&
            e.Message.Contains("DDL") &&
            e.Message.Contains("implicit COMMIT"));
    }

    // -------------------------------------------------------------------------
    // Rule 2.7 — USE_TRANSACTION_IRRELEVANT_WITH_CLI
    // -------------------------------------------------------------------------

    [Fact]
    public void UseTransactionExplicit_WithFileCliAlias_LogsWarning()
    {
        var (logger, service) = CreateServiceWithLogger();
        var files = new List<MigrationFileInfo>
        {
            new()
            {
                Filename = "10_Create.sql",
                UseTransactionExplicitlySet = true,
                UseCliToolAlias = "psql",
                SqlBlocks = new List<string> { "SELECT 1;" },
                ReleaseVersion = "Release 1.0",
                TargetGroupAlias = "Backend"
            }
        };

        InvokeLogMigrationSafetyWarnings(service, files, CreateProductOptions());

        logger.Entries.Should().Contain(e =>
            e.LogLevel == LogLevel.Warning &&
            e.Message.Contains("[Rule 2.7 USE_TRANSACTION_IRRELEVANT_WITH_CLI]") &&
            e.Message.Contains("UseTransaction explicitly set") &&
            e.Message.Contains("psql"));
    }

    [Fact]
    public void UseTransactionExplicit_WithTargetCliAlias_LogsWarning()
    {
        var (logger, service) = CreateServiceWithLogger();
        var files = new List<MigrationFileInfo>
        {
            new()
            {
                Filename = "10_Create.sql",
                UseTransactionExplicitlySet = true,
                SqlBlocks = new List<string> { "SELECT 1;" },
                ReleaseVersion = "Release 1.0",
                TargetGroupAlias = "Backend"
            }
        };

        InvokeLogMigrationSafetyWarnings(service, files, CreateProductOptions(targetCliAlias: "sqlcmd"));

        logger.Entries.Should().Contain(e =>
            e.LogLevel == LogLevel.Warning &&
            e.Message.Contains("[Rule 2.7 USE_TRANSACTION_IRRELEVANT_WITH_CLI]") &&
            e.Message.Contains("UseTransaction explicitly set") &&
            e.Message.Contains("sqlcmd") &&
            e.Message.Contains("MainDB"));
    }

    [Fact]
    public void UseTransactionDefault_WithFileCliAlias_NoWarning()
    {
        var (logger, service) = CreateServiceWithLogger();
        var files = new List<MigrationFileInfo>
        {
            new()
            {
                Filename = "10_Create.sql",
                UseTransactionExplicitlySet = false,
                UseCliToolAlias = "psql",
                SqlBlocks = new List<string> { "SELECT 1;" },
                ReleaseVersion = "Release 1.0",
                TargetGroupAlias = "Backend"
            }
        };

        InvokeLogMigrationSafetyWarnings(service, files, CreateProductOptions());

        logger.Entries.Should().NotContain(e =>
            e.LogLevel == LogLevel.Warning &&
            e.Message.Contains("[Rule 2.7 USE_TRANSACTION_IRRELEVANT_WITH_CLI]"));
    }

    [Fact]
    public void UseTransactionExplicit_NoCliAlias_NoWarning()
    {
        var (logger, service) = CreateServiceWithLogger();
        var files = new List<MigrationFileInfo>
        {
            new()
            {
                Filename = "10_Create.sql",
                UseTransactionExplicitlySet = true,
                SqlBlocks = new List<string> { "SELECT 1;" },
                ReleaseVersion = "Release 1.0",
                TargetGroupAlias = "Backend"
            }
        };

        InvokeLogMigrationSafetyWarnings(service, files, CreateProductOptions());

        logger.Entries.Should().NotContain(e =>
            e.LogLevel == LogLevel.Warning &&
            e.Message.Contains("[Rule 2.7 USE_TRANSACTION_IRRELEVANT_WITH_CLI]"));
    }

    [Fact]
    public void UseTransactionExplicit_CliOnDifferentTargetGroup_NoWarning()
    {
        var (logger, service) = CreateServiceWithLogger();
        var files = new List<MigrationFileInfo>
        {
            new()
            {
                Filename = "10_Create.sql",
                UseTransactionExplicitlySet = true,
                SqlBlocks = new List<string> { "SELECT 1;" },
                ReleaseVersion = "Release 1.0",
                TargetGroupAlias = "Backend"
            }
        };

        var productOptions = new ProductOptions("rollback")
        {
            Alias = "TestProduct",
            MigrationFilesRootDirectory = "/tmp/migrations",
            MigrationErrorAction = "Terminate",
            TargetGroups = new List<TargetGroupOptions>
            {
                new()
                {
                    Alias = "Backend",
                    DatabaseType = "SqlServer",
                    TargetMigrationOrder = "Successively",
                    HashValidationScope = "Disabled",
                    Targets = new List<TargetOptions>
                    {
                        new()
                        {
                            Alias = "MainDB",
                            ConnectionString = "Server=localhost"
                        }
                    }
                },
                new()
                {
                    Alias = "Frontend",
                    DatabaseType = "PostgreSQL",
                    TargetMigrationOrder = "Successively",
                    HashValidationScope = "Disabled",
                    Targets = new List<TargetOptions>
                    {
                        new()
                        {
                            Alias = "FrontDB",
                            ConnectionString = "Host=localhost",
                            UseCliToolAlias = "psql"
                        }
                    }
                }
            }
        };

        InvokeLogMigrationSafetyWarnings(service, files, productOptions);

        logger.Entries.Should().NotContain(e =>
            e.LogLevel == LogLevel.Warning &&
            e.Message.Contains("[Rule 2.7 USE_TRANSACTION_IRRELEVANT_WITH_CLI]"));
    }

    // -------------------------------------------------------------------------
    // Summary and no-warnings baseline
    // -------------------------------------------------------------------------

    [Fact]
    public void NoFiles_NoWarnings()
    {
        var (logger, service) = CreateServiceWithLogger();
        var files = new List<MigrationFileInfo>();

        InvokeLogMigrationSafetyWarnings(service, files, CreateProductOptions());

        logger.Entries.Should().NotContain(e =>
            e.LogLevel == LogLevel.Warning);
    }

    [Fact]
    public void MultipleWarnings_LogsSummary()
    {
        var (logger, service) = CreateServiceWithLogger();
        var files = new List<MigrationFileInfo>
        {
            new()
            {
                Filename = "10_Create.sql",
                UseTransaction = false,
                SqlBlocks = new List<string> { "CREATE TABLE A;", "CREATE TABLE B;" },
                ReleaseVersion = "Release 1.0",
                TargetGroupAlias = "Backend"
            }
        };

        InvokeLogMigrationSafetyWarnings(service, files, CreateProductOptions(maxRetries: 3));

        // Should have individual warnings plus a summary
        logger.Entries.Should().Contain(e =>
            e.LogLevel == LogLevel.Warning &&
            e.Message.Contains("warning(s) detected"));
    }

    // -------------------------------------------------------------------------
    // Rule 2.1 — ROLLBACK_ACTION_WITHOUT_TRANSACTION
    // -------------------------------------------------------------------------

    [Fact]
    public void RollbackActionWithoutTransaction_LogsWarning()
    {
        var (logger, service) = CreateServiceWithLogger();
        var files = new List<MigrationFileInfo>
        {
            new()
            {
                Filename = "10_Create.sql",
                UseTransaction = false,
                SqlBlocks = new List<string> { "CREATE TABLE A;" },
                ReleaseVersion = "Release 1.0",
                TargetGroupAlias = "Backend"
            }
        };

        InvokeLogMigrationSafetyWarnings(service, files,
            CreateProductOptions(migrationErrorAction: "Rollback"));

        logger.Entries.Should().Contain(e =>
            e.LogLevel == LogLevel.Warning &&
            e.Message.Contains("[Rule 2.1 ROLLBACK_ACTION_WITHOUT_TRANSACTION]") &&
            e.Message.Contains("UseTransaction=false"));
    }

    [Fact]
    public void RollbackActionWithTransaction_NoRule21Warning()
    {
        var (logger, service) = CreateServiceWithLogger();
        var files = new List<MigrationFileInfo>
        {
            new()
            {
                Filename = "10_Create.sql",
                UseTransaction = true,
                SqlBlocks = new List<string> { "CREATE TABLE A;" },
                ReleaseVersion = "Release 1.0",
                TargetGroupAlias = "Backend"
            }
        };

        InvokeLogMigrationSafetyWarnings(service, files,
            CreateProductOptions(migrationErrorAction: "Rollback"));

        logger.Entries.Should().NotContain(e =>
            e.LogLevel == LogLevel.Warning &&
            e.Message.Contains("[Rule 2.1 ROLLBACK_ACTION_WITHOUT_TRANSACTION]"));
    }

    [Fact]
    public void TerminateActionWithoutTransaction_NoRule21Warning()
    {
        var (logger, service) = CreateServiceWithLogger();
        var files = new List<MigrationFileInfo>
        {
            new()
            {
                Filename = "10_Create.sql",
                UseTransaction = false,
                SqlBlocks = new List<string> { "CREATE TABLE A;" },
                ReleaseVersion = "Release 1.0",
                TargetGroupAlias = "Backend"
            }
        };

        InvokeLogMigrationSafetyWarnings(service, files,
            CreateProductOptions(migrationErrorAction: "Terminate"));

        logger.Entries.Should().NotContain(e =>
            e.LogLevel == LogLevel.Warning &&
            e.Message.Contains("[Rule 2.1 ROLLBACK_ACTION_WITHOUT_TRANSACTION]"));
    }

    [Fact]
    public void FileOverrideRollbackReleaseWithoutTransaction_LogsRule21Warning()
    {
        var (logger, service) = CreateServiceWithLogger();
        var files = new List<MigrationFileInfo>
        {
            new()
            {
                Filename = "10_Create.sql",
                UseTransaction = false,
                SqlBlocks = new List<string> { "CREATE TABLE A;" },
                ReleaseVersion = "Release 1.0",
                TargetGroupAlias = "Backend",
                MigrationErrorActionOverride = MigrationErrorAction.RollbackRelease
            }
        };

        // Product-level is Terminate — only the file-level override triggers the warning
        InvokeLogMigrationSafetyWarnings(service, files,
            CreateProductOptions(migrationErrorAction: "Terminate"));

        logger.Entries.Should().Contain(e =>
            e.LogLevel == LogLevel.Warning &&
            e.Message.Contains("[Rule 2.1 ROLLBACK_ACTION_WITHOUT_TRANSACTION]") &&
            e.Message.Contains("UseTransaction=false"));
    }

    // -------------------------------------------------------------------------
    // Rule 2.2 — ROLLBACK_ACTION_WITHOUT_ROLLBACK_FILE
    // -------------------------------------------------------------------------

    [Fact]
    public void RollbackActionNoRollbackFile_LogsRule22Warning()
    {
        var (logger, service) = CreateServiceWithLogger();
        var files = new List<MigrationFileInfo>
        {
            new()
            {
                Filename = "10_Create.sql",
                UseTransaction = true,
                SqlBlocks = new List<string> { "CREATE TABLE A;" },
                ReleaseVersion = "Release 1.0",
                TargetGroupAlias = "Backend",
                RequireRollbackFile = false,
                MigrateDownFileExists = false
            }
        };

        InvokeLogMigrationSafetyWarnings(service, files,
            CreateProductOptions(migrationErrorAction: "Rollback"));

        logger.Entries.Should().Contain(e =>
            e.LogLevel == LogLevel.Warning &&
            e.Message.Contains("[Rule 2.2 ROLLBACK_ACTION_WITHOUT_ROLLBACK_FILE]") &&
            e.Message.Contains("RequireRollbackFile=false"));
    }

    [Fact]
    public void RollbackActionRollbackFileExists_NoRule22Warning()
    {
        var (logger, service) = CreateServiceWithLogger();
        var files = new List<MigrationFileInfo>
        {
            new()
            {
                Filename = "10_Create.sql",
                UseTransaction = true,
                SqlBlocks = new List<string> { "CREATE TABLE A;" },
                ReleaseVersion = "Release 1.0",
                TargetGroupAlias = "Backend",
                RequireRollbackFile = false,
                MigrateDownFileExists = true
            }
        };

        InvokeLogMigrationSafetyWarnings(service, files,
            CreateProductOptions(migrationErrorAction: "Rollback"));

        logger.Entries.Should().NotContain(e =>
            e.LogLevel == LogLevel.Warning &&
            e.Message.Contains("[Rule 2.2 ROLLBACK_ACTION_WITHOUT_ROLLBACK_FILE]"));
    }

    [Fact]
    public void RollbackActionRequireRollbackFileTrue_NoRule22Warning()
    {
        var (logger, service) = CreateServiceWithLogger();
        var files = new List<MigrationFileInfo>
        {
            new()
            {
                Filename = "10_Create.sql",
                UseTransaction = true,
                SqlBlocks = new List<string> { "CREATE TABLE A;" },
                ReleaseVersion = "Release 1.0",
                TargetGroupAlias = "Backend",
                RequireRollbackFile = true,
                MigrateDownFileExists = false
            }
        };

        InvokeLogMigrationSafetyWarnings(service, files,
            CreateProductOptions(migrationErrorAction: "Rollback"));

        logger.Entries.Should().NotContain(e =>
            e.LogLevel == LogLevel.Warning &&
            e.Message.Contains("[Rule 2.2 ROLLBACK_ACTION_WITHOUT_ROLLBACK_FILE]"));
    }

    // -------------------------------------------------------------------------
    // Rule 2.6 — RUN_ALWAYS_WITH_HASH_VALIDATION
    // -------------------------------------------------------------------------

    [Fact]
    public void RunAlwaysWithHashValidationFile_LogsRule26Warning()
    {
        var (logger, service) = CreateServiceWithLogger();
        var files = new List<MigrationFileInfo>
        {
            new()
            {
                Filename = "10_Seed.sql",
                UseTransaction = true,
                RunAlways = true,
                SqlBlocks = new List<string> { "INSERT INTO Config VALUES (1);" },
                ReleaseVersion = "Release 1.0",
                TargetGroupAlias = "Backend"
            }
        };

        InvokeLogMigrationSafetyWarnings(service, files,
            CreateProductOptions(hashValidationScope: "File"));

        logger.Entries.Should().Contain(e =>
            e.LogLevel == LogLevel.Warning &&
            e.Message.Contains("[Rule 2.6 RUN_ALWAYS_WITH_HASH_VALIDATION]") &&
            e.Message.Contains("RunAlways=true"));
    }

    [Fact]
    public void RunAlwaysWithHashValidationSqlBlocks_LogsRule26Warning()
    {
        var (logger, service) = CreateServiceWithLogger();
        var files = new List<MigrationFileInfo>
        {
            new()
            {
                Filename = "10_Seed.sql",
                UseTransaction = true,
                RunAlways = true,
                SqlBlocks = new List<string> { "INSERT INTO Config VALUES (1);" },
                ReleaseVersion = "Release 1.0",
                TargetGroupAlias = "Backend"
            }
        };

        InvokeLogMigrationSafetyWarnings(service, files,
            CreateProductOptions(hashValidationScope: "SqlBlocks"));

        logger.Entries.Should().Contain(e =>
            e.LogLevel == LogLevel.Warning &&
            e.Message.Contains("[Rule 2.6 RUN_ALWAYS_WITH_HASH_VALIDATION]") &&
            e.Message.Contains("RunAlways=true"));
    }

    [Fact]
    public void RunAlwaysHashDisabled_NoRule26Warning()
    {
        var (logger, service) = CreateServiceWithLogger();
        var files = new List<MigrationFileInfo>
        {
            new()
            {
                Filename = "10_Seed.sql",
                UseTransaction = true,
                RunAlways = true,
                SqlBlocks = new List<string> { "INSERT INTO Config VALUES (1);" },
                ReleaseVersion = "Release 1.0",
                TargetGroupAlias = "Backend"
            }
        };

        InvokeLogMigrationSafetyWarnings(service, files,
            CreateProductOptions(hashValidationScope: "Disabled"));

        logger.Entries.Should().NotContain(e =>
            e.LogLevel == LogLevel.Warning &&
            e.Message.Contains("[Rule 2.6 RUN_ALWAYS_WITH_HASH_VALIDATION]"));
    }

    [Fact]
    public void NotRunAlwaysWithHashValidation_NoRule26Warning()
    {
        var (logger, service) = CreateServiceWithLogger();
        var files = new List<MigrationFileInfo>
        {
            new()
            {
                Filename = "10_Create.sql",
                UseTransaction = true,
                RunAlways = false,
                SqlBlocks = new List<string> { "CREATE TABLE A;" },
                ReleaseVersion = "Release 1.0",
                TargetGroupAlias = "Backend"
            }
        };

        InvokeLogMigrationSafetyWarnings(service, files,
            CreateProductOptions(hashValidationScope: "File"));

        logger.Entries.Should().NotContain(e =>
            e.LogLevel == LogLevel.Warning &&
            e.Message.Contains("[Rule 2.6 RUN_ALWAYS_WITH_HASH_VALIDATION]"));
    }

    // -------------------------------------------------------------------------
    // Rule 2.12 — SIMULTANEOUSLY_WITH_ROLLBACK
    // -------------------------------------------------------------------------

    [Fact]
    public void SimultaneouslyWithRollback_LogsRule212Warning()
    {
        var (logger, service) = CreateServiceWithLogger();
        var files = new List<MigrationFileInfo>
        {
            new()
            {
                Filename = "10_Create.sql",
                UseTransaction = true,
                SqlBlocks = new List<string> { "CREATE TABLE A;" },
                ReleaseVersion = "Release 1.0",
                TargetGroupAlias = "Backend"
            }
        };

        InvokeLogMigrationSafetyWarnings(service, files,
            CreateProductOptions(migrationErrorAction: "Rollback", targetMigrationOrder: "Simultaneously"));

        logger.Entries.Should().Contain(e =>
            e.LogLevel == LogLevel.Warning &&
            e.Message.Contains("[Rule 2.12 SIMULTANEOUSLY_WITH_ROLLBACK]") &&
            e.Message.Contains("Simultaneously"));
    }

    [Fact]
    public void SimultaneouslyWithTerminate_NoRule212Warning()
    {
        var (logger, service) = CreateServiceWithLogger();
        var files = new List<MigrationFileInfo>
        {
            new()
            {
                Filename = "10_Create.sql",
                UseTransaction = true,
                SqlBlocks = new List<string> { "CREATE TABLE A;" },
                ReleaseVersion = "Release 1.0",
                TargetGroupAlias = "Backend"
            }
        };

        InvokeLogMigrationSafetyWarnings(service, files,
            CreateProductOptions(migrationErrorAction: "Terminate", targetMigrationOrder: "Simultaneously"));

        logger.Entries.Should().NotContain(e =>
            e.LogLevel == LogLevel.Warning &&
            e.Message.Contains("[Rule 2.12 SIMULTANEOUSLY_WITH_ROLLBACK]"));
    }

    [Fact]
    public void SuccessivelyWithRollback_NoRule212Warning()
    {
        var (logger, service) = CreateServiceWithLogger();
        var files = new List<MigrationFileInfo>
        {
            new()
            {
                Filename = "10_Create.sql",
                UseTransaction = true,
                SqlBlocks = new List<string> { "CREATE TABLE A;" },
                ReleaseVersion = "Release 1.0",
                TargetGroupAlias = "Backend"
            }
        };

        InvokeLogMigrationSafetyWarnings(service, files,
            CreateProductOptions(migrationErrorAction: "Rollback", targetMigrationOrder: "Successively"));

        logger.Entries.Should().NotContain(e =>
            e.LogLevel == LogLevel.Warning &&
            e.Message.Contains("[Rule 2.12 SIMULTANEOUSLY_WITH_ROLLBACK]"));
    }
}
