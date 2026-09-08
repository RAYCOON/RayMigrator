using AwesomeAssertions;
using Raycoon.RayMigrator.Core;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Core.Configuration.Options;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1: Regression tests for MigrationContext.Clone.
/// Verifies that Clone correctly propagates EnvironmentId and MigratorMetaId, two fields that were
/// previously omitted from the Clone copy constructor. (MigrationState.MigrationEvent was removed in #12.)
/// </summary>
public class MigrationContextCloneTests
{
    #region Helpers

    private static MigrationContext CreateTestContext()
    {
        var repoOptions = new RepositoryOptions
        {
            DatabaseType = "SqlServer",
            ConnectionString = "Server=test",
            SchemaName = "ray",
            TableBaseName = "",
            DbCommandTimeoutInSeconds = 30,
            DbCommandMaxRetries = 0,
            DbCommandWaitTimeInMsBeforeRetry = 0
        };

        var rayOptions = new RayMigratorOptions
        {
            Repository = repoOptions,
            ProductDefaults = new ProductDefaultOptions("UTF-8")
            {
                MigrationErrorAction = "Terminate",
                MigrationFilesExtension = "sql",
                MigrationRollbackFilesPreExtension = "rollback",
                MigrationFilesEncoding = "UTF-8",
                RequireRollbackFile = false,
                TargetGroupDefaults = new TargetGroupDefaultOptions
                {
                    TargetMigrationOrder = "FileByFile",
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
                    Alias = "TestProduct",
                    MigrationFilesRootDirectory = "/tmp",
                    MigrationErrorAction = "Terminate",
                    MigrationFilesExtension = "sql",
                    MigrationRollbackFilesPreExtension = "rollback",
                    MigrationFilesEncoding = "UTF-8",
                    RequireRollbackFile = false,
                    TargetGroups = new List<TargetGroupOptions>
                    {
                        new()
                        {
                            Alias = "Backend",
                            DatabaseType = "SqlServer",
                            TargetMigrationOrder = "FileByFile",
                            HashValidationScope = "File",
                            Targets = new List<TargetOptions>
                            {
                                new()
                                {
                                    Alias = "MainDB",
                                    ConnectionString = "Server=target",
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

        var consoleOptions = new RayMigratorConsoleOptions
        {
            Command = MigrationCommand.MigrateUp,
            Product = "TestProduct",
            Environment = "Docker",
            RunMode = MigrationRunMode.Migrate,
            ShowStartupInfo = false,
            RevealSensitiveData = false
        };

        return new MigrationContext(rayOptions, consoleOptions, "3.0.0");
    }

    #endregion

    #region EnvironmentId propagation

    [Fact]
    public void Clone_PropagatesEnvironmentId_WhenSet()
    {
        var ctx = CreateTestContext();
        ctx.MigrationState.EnvironmentId = 42;

        var clone = ctx.Clone;

        clone.MigrationState.EnvironmentId.Should().Be(42,
            "Clone must copy EnvironmentId from the source MigrationState");
    }

    [Fact]
    public void Clone_PropagatesEnvironmentId_WhenZero()
    {
        var ctx = CreateTestContext();
        ctx.MigrationState.EnvironmentId = 0;

        var clone = ctx.Clone;

        clone.MigrationState.EnvironmentId.Should().Be(0,
            "Clone must copy EnvironmentId = 0 (default) from the source MigrationState");
    }

    [Fact]
    public void Clone_EnvironmentId_IsIndependentFromOriginal()
    {
        var ctx = CreateTestContext();
        ctx.MigrationState.EnvironmentId = 10;

        var clone = ctx.Clone;

        // Mutate the clone — original must not change
        clone.MigrationState.EnvironmentId = 99;

        ctx.MigrationState.EnvironmentId.Should().Be(10,
            "Mutating Clone.MigrationState.EnvironmentId must not affect the original context");
    }

    #endregion

    #region MigratorMetaId propagation

    [Fact]
    public void Clone_PropagatesMigratorMetaId_WhenSet()
    {
        var ctx = CreateTestContext();
        ctx.MigrationState.MigratorMetaId = 55;

        var clone = ctx.Clone;

        clone.MigrationState.MigratorMetaId.Should().Be(55,
            "Clone must copy MigratorMetaId from the source MigrationState");
    }

    [Fact]
    public void Clone_PropagatesMigratorMetaId_WhenZero()
    {
        var ctx = CreateTestContext();
        ctx.MigrationState.MigratorMetaId = 0;

        var clone = ctx.Clone;

        clone.MigrationState.MigratorMetaId.Should().Be(0,
            "Clone must copy MigratorMetaId = 0 (default) from the source MigrationState");
    }

    [Fact]
    public void Clone_MigratorMetaId_IsIndependentFromOriginal()
    {
        var ctx = CreateTestContext();
        ctx.MigrationState.MigratorMetaId = 5;

        var clone = ctx.Clone;
        clone.MigrationState.MigratorMetaId = 999;

        ctx.MigrationState.MigratorMetaId.Should().Be(5,
            "Mutating Clone.MigrationState.MigratorMetaId must not affect the original context");
    }

    #endregion

    #region Both fields together

    [Fact]
    public void Clone_PropagatesEnvironmentIdAndMigratorMetaId_Together()
    {
        var ctx = CreateTestContext();
        ctx.MigrationState.EnvironmentId = 7;
        ctx.MigrationState.MigratorMetaId = 3;

        var clone = ctx.Clone;

        clone.MigrationState.EnvironmentId.Should().Be(7,
            "EnvironmentId must be cloned");
        clone.MigrationState.MigratorMetaId.Should().Be(3,
            "MigratorMetaId must be cloned");
    }

    [Fact]
    public void Clone_AlsoPreservesExistingFields_ProductId_MigrationRunId()
    {
        // Regression guard: existing fields must still be cloned
        var ctx = CreateTestContext();
        ctx.MigrationState.ProductId = 11;
        ctx.MigrationState.MigrationRunId = 22;
        ctx.MigrationState.EnvironmentId = 7;

        var clone = ctx.Clone;

        clone.MigrationState.ProductId.Should().Be(11);
        clone.MigrationState.MigrationRunId.Should().Be(22);
        clone.MigrationState.EnvironmentId.Should().Be(7);
    }

    #endregion
}
