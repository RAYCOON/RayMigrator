using System.Reflection;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Raycoon.RayMigrator.Core;
using Raycoon.RayMigrator.Core.Configuration;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Core.Templates;
using Raycoon.RayMigrator.Database.Common;
using Raycoon.RayMigrator.Tests.Unit.Helpers;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1: Tests for the Information line RepositoryCheckCreate logs when Repository_CheckCreate reports that it
/// created the repository in this run (second result code 1, #27). Uses a real TemplateCache from the test output
/// DataAccessLayers/ and a mocked IDal that returns a configurable scalar result.
/// </summary>
public class TemplateExecutorRepositoryCreatedLoggingTests
{
    private const string CreatedResult = "1,1,RayMigrator repository-tables with master data and new VersionId [1] successfully created";
    private const string ExistsResult = "1,0,RayMigrator repository already exists. Using VersionId [1].";
    private const string LegacyResult = "1,RayMigrator repository already exists. Using VersionId [1].";

    [Fact]
    public void RepositoryCheckCreate_SecondCodeIsOne_LogsInformation()
    {
        // Arrange
        var (executor, logger, _) = CreateExecutor(CreatedResult);

        // Act
        executor.RepositoryCheckCreate();

        // Assert
        logger.Entries.Should().ContainSingle(e => e.LogLevel == LogLevel.Information && e.Message.Contains("Repository created"))
            .Which.Message.Should().Contain("DatabaseType [SqlServer]").And.Contain("VersionId [1]");
    }

    [Theory]
    [InlineData(ExistsResult)]
    [InlineData(LegacyResult)]
    public void RepositoryCheckCreate_RepositoryExistedOrLegacyResponse_LogsNoInformation(string scalarResult)
    {
        // Arrange
        var (executor, logger, _) = CreateExecutor(scalarResult);

        // Act
        executor.RepositoryCheckCreate();

        // Assert
        logger.Entries.Should().NotContain(e => e.LogLevel == LogLevel.Information,
            because: "only a repository created in this run is worth an Information line (#27)");
    }

    [Fact]
    public void RepositoryCheckCreate_SetsMigratorMetaIdFromTheFirstCode()
    {
        // Arrange
        var (executor, _, context) = CreateExecutor("7,1,RayMigrator repository-tables with master data and new VersionId [7] successfully created");

        // Act
        executor.RepositoryCheckCreate();

        // Assert
        context.MigrationState.MigratorMetaId.Should().Be(7, because: "the first code stays the VersionId");
    }

    [Fact]
    public void RepositoryCheckCreate_SensitiveDataNotRevealed_MasksTheSchemaName()
    {
        // Arrange
        using var scope = SensitiveDataMasker.BeginScope(revealSensitiveData: false);
        SensitiveDataMasker.RegisterSensitiveValue("ray");
        var (executor, logger, _) = CreateExecutor(CreatedResult);

        // Act
        executor.RepositoryCheckCreate();

        // Assert
        string line = logger.Entries.Single(e => e.LogLevel == LogLevel.Information).Message;
        line.Should().Contain($"schema [{SensitiveDataMasker.MaskString}]").And.NotContain("schema [ray]");
    }

    [Fact]
    public void RepositoryCheckCreate_SensitiveDataRevealed_ShowsTheSchemaName()
    {
        // Arrange
        using var scope = SensitiveDataMasker.BeginScope(revealSensitiveData: true);
        SensitiveDataMasker.RegisterSensitiveValue("ray");
        var (executor, logger, _) = CreateExecutor(CreatedResult);

        // Act
        executor.RepositoryCheckCreate();

        // Assert
        logger.Entries.Single(e => e.LogLevel == LogLevel.Information).Message.Should().Contain("schema [ray]");
    }

    #region Helpers

    /// <summary>
    /// Builds a TemplateExecutor with a real TemplateCache (test output DataAccessLayers/), a capturing logger and
    /// a mocked repository IDal whose ExecuteScalarAsync returns <paramref name="scalarResult"/>.
    /// </summary>
    private static (TemplateExecutor Executor, CapturingLogger<TemplateExecutor> Logger, MigrationContext Context) CreateExecutor(string scalarResult)
    {
        var dal = Substitute.For<IDal>();
        dal.DatabaseType.Returns("SqlServer");
        dal.ExecuteScalarAsync(Arg.Any<string>(), Arg.Any<IDalSettings>(), Arg.Any<DalParameterList>())
           .Returns(Task.FromResult<object?>(scalarResult));

        var context = BuildContext();
        var templateCache = new TemplateCache(Options.Create(context.RayMigratorOptions), false, NullLogger<TemplateCache>.Instance, validateConfiguration: false);
        var logger = new CapturingLogger<TemplateExecutor>();
        var executor = new TemplateExecutor(templateCache, logger, new SingletonMigrationContextAccessor { Current = context });

        // Inject the repository DAL and options directly; DalFactory.TryGetDal would need a real driver.
        typeof(TemplateExecutor).GetField("_repositoryDalBacking", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(executor, dal);
        typeof(TemplateExecutor).GetField("_repositoryBacking", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(executor, context.RayMigratorOptions.Repository!);

        return (executor, logger, context);
    }

    private static MigrationContext BuildContext()
    {
        var rayOptions = new RayMigratorOptions
        {
            Repository = new RepositoryOptions
            {
                DatabaseType = "SqlServer",
                ConnectionString = "Server=test",
                SchemaName = "ray",
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
            Products =
            [
                new ProductOptions("rollback")
                {
                    Alias = "TestProduct",
                    MigrationFilesRootDirectory = "/tmp",
                    MigrationErrorAction = "Terminate",
                    MigrationFilesExtension = "sql",
                    MigrationRollbackFilesPreExtension = "rollback",
                    MigrationFilesEncoding = "UTF-8",
                    RequireRollbackFile = false,
                    TargetGroups =
                    [
                        new TargetGroupOptions
                        {
                            Alias = "Backend",
                            DatabaseType = "SqlServer",
                            TargetMigrationOrder = "FileByFile",
                            HashValidationScope = "File",
                            Targets =
                            [
                                new TargetOptions
                                {
                                    Alias = "MainDB",
                                    ConnectionString = "Server=target",
                                    DbCommandTimeoutInSeconds = 20,
                                    DbCommandMaxRetries = 0,
                                    DbCommandWaitTimeInMsBeforeRetry = 250
                                }
                            ]
                        }
                    ]
                }
            ]
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

        var context = new MigrationContext(rayOptions, consoleOptions, "3.0.0");
        context.MigrationState.EnvironmentId = 7;
        context.MigrationState.ProductId = 3;
        context.MigrationState.MigrationRunId = 1;
        context.MigrationState.MigrationRunResult = MigrationRunResult.Running;
        return context;
    }

    #endregion
}
