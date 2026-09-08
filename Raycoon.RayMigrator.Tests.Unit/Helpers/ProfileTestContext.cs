using Raycoon.RayMigrator.Core;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Core.Configuration.Options;

namespace Raycoon.RayMigrator.Tests.Unit.Helpers;

/// <summary>
/// Builds minimal <see cref="RayMigratorOptions"/> / <see cref="MigrationContext"/> instances for tests that
/// exercise the command profile (#6): one Sqlite product with one target group and one target.
/// </summary>
internal static class ProfileTestContext
{
    public const string ProductAlias = "TestProduct";
    public const string TargetGroupAlias = "Backend";
    public const string TargetAlias = "MainDB";

    /// <summary>
    /// A Sqlite connection string whose file lives in a directory that does not exist. Opening it fails,
    /// building a <c>SqliteConnection</c> object from it does not.
    /// </summary>
    public static string UnreachableSqliteConnectionString()
        => $"Data Source={Path.Combine(Path.GetTempPath(), "RayMigrator_DoesNotExist_" + Guid.NewGuid().ToString("N"), "unreachable.db")}";

    public static RayMigratorOptions CreateOptions(string targetConnectionString, string? databaseLoggingConnectionString = null)
    {
        var options = new RayMigratorOptions
        {
            Repository = new RepositoryOptions
            {
                DatabaseType = "Sqlite",
                ConnectionString = "Data Source=:memory:",
                SchemaName = "",
                TableBaseName = "",
                DbCommandTimeoutInSeconds = 30,
                DbCommandMaxRetries = 0,
                DbCommandWaitTimeInMsBeforeRetry = 0
            },
            ProductDefaults = new ProductDefaultOptions("UTF-8")
            {
                MigrationErrorAction = "Terminate",
                MigrationFilesExtension = "sql",
                MigrationRollbackFilesPreExtension = "rollback",
                MigrationFilesEncoding = "UTF-8",
                RequireRollbackFile = false,
                TargetGroupDefaults = new TargetGroupDefaultOptions
                {
                    TargetMigrationOrder = "Simultaneously",
                    HashValidationScope = "File",
                    TargetDefaults = new TargetDefaultsOptions
                    {
                        DbCommandTimeoutInSeconds = 20,
                        DbCommandMaxRetries = 0,
                        DbCommandWaitTimeInMsBeforeRetry = 250
                    }
                }
            },
            Products = new List<ProductOptions>
            {
                new("rollback")
                {
                    Alias = ProductAlias,
                    MigrationFilesRootDirectory = Path.GetTempPath(),
                    MigrationErrorAction = "Terminate",
                    MigrationFilesExtension = "sql",
                    MigrationRollbackFilesPreExtension = "rollback",
                    MigrationFilesEncoding = "UTF-8",
                    RequireRollbackFile = false,
                    TargetGroups = new List<TargetGroupOptions>
                    {
                        new()
                        {
                            Alias = TargetGroupAlias,
                            DatabaseType = "Sqlite",
                            TargetMigrationOrder = "Simultaneously",
                            HashValidationScope = "File",
                            Targets = new List<TargetOptions>
                            {
                                new()
                                {
                                    Alias = TargetAlias,
                                    ConnectionString = targetConnectionString,
                                    DbCommandTimeoutInSeconds = 20,
                                    DbCommandMaxRetries = 0,
                                    DbCommandWaitTimeInMsBeforeRetry = 250
                                }
                            }
                        }
                    }
                }
            }
        };

        if (databaseLoggingConnectionString != null)
        {
            options.DatabaseLogging = new DatabaseLoggingOptions
            {
                DatabaseType = "Sqlite",
                ConnectionString = databaseLoggingConnectionString,
                SchemaName = "",
                MinimumLevel = "Information",
                DbCommandTimeoutInSeconds = 20
            };
        }

        return options;
    }

    public static RayMigratorConsoleOptions CreateConsoleOptions(MigrationCommand command, MigrationRunMode runMode, bool? fixDryRun = null)
        => new()
        {
            Command = command,
            Product = ProductAlias,
            Environment = "Docker",
            RunMode = runMode,
            ShowStartupInfo = false,
            RevealSensitiveData = false,
            FixDryRun = fixDryRun
        };

    public static MigrationContext CreateContext(MigrationCommand command, MigrationRunMode runMode, string targetConnectionString, bool? fixDryRun = null)
        => new(CreateOptions(targetConnectionString), CreateConsoleOptions(command, runMode, fixDryRun), "0.0.0-test");
}
