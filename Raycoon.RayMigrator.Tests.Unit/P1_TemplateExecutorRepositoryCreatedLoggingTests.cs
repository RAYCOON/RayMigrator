using System.Reflection;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Raycoon.RayMigrator.Core;
using Raycoon.RayMigrator.Core.Configuration;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Core.Templates;
using Raycoon.RayMigrator.Database.Common;
using Raycoon.RayMigrator.Shared.Exceptions;
using Raycoon.RayMigrator.Tests.Unit.Helpers;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1: Tests for <c>TemplateExecutor.RepositoryCheckCreate</c>, the schema version orchestration of ADR-024:
/// <c>Repository_CheckCreate</c> reports the version, the missing <c>Repository_Upgrade_{n}</c> steps run in order,
/// <c>Repository_MigratorMeta_CheckInsert</c> yields the MigratorMetaId, and a created or upgraded repository is logged
/// at Information level (#27). Uses a real TemplateCache from the test output DataAccessLayers/ and a mocked IDal that
/// answers the scalar calls from a queue of results.
/// </summary>
public class TemplateExecutorRepositoryCreatedLoggingTests
{
    private const string NoRepository = "0,0,RayMigrator repository does not exist yet";
    private const string LegacyRepository = "0,1,RayMigrator repository tables exist without a schema version";
    private const string MetaRegistered = "7,RayMigrator version [3.0.0] registered with new MigratorMetaId [7]";
    private static readonly string CurrentRepository = $"{RepositorySchema.CurrentVersion},1,RayMigrator repository has schema version [{RepositorySchema.CurrentVersion}]";

    /// <summary>The results of the steps <paramref name="from"/> to <see cref="RepositorySchema.CurrentVersion"/>, each returning its own version.</summary>
    private static string[] StepsFrom(int from) =>
        Enumerable.Range(from, RepositorySchema.CurrentVersion - from + 1).Select(v => $"{v},RayMigrator repository schema version [{v}] applied").ToArray();

    private static string[] Results(string first, params string[] rest) => [first, .. rest];

    /// <summary>A result that makes the mocked DAL throw instead of answering, as a failed step does.</summary>
    private const string Throw = "<throw>";

    [Fact]
    public void RepositoryCheckCreate_StepFailsButAParallelStartAppliedIt_SkipsTheStepAndContinues()
    {
        // Arrange: step 1 fails, the re-read shows the repository at the current version (the other process won the race)
        var (executor, logger, context, calls) = CreateExecutor(Results(NoRepository, [Throw, CurrentRepository, .. StepsFrom(2), MetaRegistered]));

        // Act
        executor.RepositoryCheckCreate();

        // Assert
        calls.Count.Should().Be(1 + 1 + 1 + (RepositorySchema.CurrentVersion - 1) + 1,
            because: "the check, the failed step, the re-read, the remaining steps and the MigratorMeta registration each execute one template");
        logger.Entries.Should().ContainSingle(e => e.LogLevel == LogLevel.Debug && e.Message.Contains("applied by a parallel start"))
            .Which.Message.Should().Contain("schema version [1]");
        context.MigrationState.MigratorMetaId.Should().Be(7);
    }

    [Fact]
    public void RepositoryCheckCreate_StepFailsAndTheVersionIsUnchanged_ThrowsTheStepError()
    {
        // Arrange: step 1 fails and the re-read still reports version 0
        var (executor, _, _, calls) = CreateExecutor(NoRepository, Throw, NoRepository);

        // Act
        var act = () => executor.RepositoryCheckCreate();

        // Assert
        act.Should().Throw<TemplateExecutionException>().Which.Message.Should().Contain("Repository_Upgrade_1");
        calls.Count.Should().Be(3, because: "nothing runs after the failed step");
    }

    [Fact]
    public void RepositoryCheckCreate_StepFailsAndTheReReadFails_ThrowsTheStepError()
    {
        // Arrange: step 1 fails and so does the re-read of the version
        var (executor, logger, _, _) = CreateExecutor(NoRepository, Throw, Throw);

        // Act
        var act = () => executor.RepositoryCheckCreate();

        // Assert
        act.Should().Throw<TemplateExecutionException>().Which.Message.Should().Contain("Repository_Upgrade_1",
            because: "the step's own error surfaces, not the failed re-read");
        logger.Entries.Should().Contain(e => e.LogLevel == LogLevel.Debug && e.Message.Contains("Could not re-read"));
    }

    [Fact]
    public void RepositoryCheckCreate_FreshRepository_AppliesEveryStepAndLogsCreated()
    {
        // Arrange
        var (executor, logger, context, calls) = CreateExecutor(Results(NoRepository, [.. StepsFrom(1), MetaRegistered]));

        // Act
        executor.RepositoryCheckCreate();

        // Assert
        calls.Count.Should().Be(2 + RepositorySchema.CurrentVersion, because: "the check, every step and the MigratorMeta registration each execute one template");
        logger.Entries.Should().ContainSingle(e => e.LogLevel == LogLevel.Information && e.Message.Contains("Repository created"))
            .Which.Message.Should().Contain("DatabaseType [SqlServer]").And.Contain($"schema version [{RepositorySchema.CurrentVersion}]");
        context.MigrationState.MigratorMetaId.Should().Be(7);
    }

    [Fact]
    public void RepositoryCheckCreate_CurrentRepository_RegistersTheVersionOnly()
    {
        // Arrange
        var (executor, logger, context, calls) = CreateExecutor(CurrentRepository, MetaRegistered);

        // Act
        executor.RepositoryCheckCreate();

        // Assert
        calls.Count.Should().Be(2, because: "a repository at the target version runs no step");
        logger.Entries.Should().NotContain(e => e.LogLevel == LogLevel.Information,
            because: "only a repository created or upgraded in this run is worth an Information line (#27)");
        context.MigrationState.MigratorMetaId.Should().Be(7);
    }

    [Fact]
    public void RepositoryCheckCreate_OlderRepository_AppliesTheMissingStepsAndLogsUpgraded()
    {
        // Arrange: a repository at version 1 while the engine expects CurrentVersion
        var (executor, logger, context, calls) = CreateExecutor(Results("1,1,RayMigrator repository has schema version [1]", [.. StepsFrom(2), MetaRegistered]));

        // Act
        executor.RepositoryCheckCreate();

        // Assert
        calls.Count.Should().Be(1 + (RepositorySchema.CurrentVersion - 1) + 1, because: "only the steps above version 1 run");
        logger.Entries.Should().ContainSingle(e => e.LogLevel == LogLevel.Information && e.Message.Contains("Repository schema upgraded"))
            .Which.Message.Should().Contain("version [1]").And.Contain($"to [{RepositorySchema.CurrentVersion}]");
        context.MigrationState.MigratorMetaId.Should().Be(7);
    }

    [Fact]
    public void RepositoryCheckCreate_TablesWithoutSchemaVersion_ThrowsBeforeAnyStep()
    {
        // Arrange
        var (executor, _, context, calls) = CreateExecutor(Results(LegacyRepository, [.. StepsFrom(1), MetaRegistered]));

        // Act
        var act = () => executor.RepositoryCheckCreate();

        // Assert
        var ex = act.Should().Throw<RepositorySchemaException>().Which;
        ex.Message.Should().Contain("before 0.17.0").And.Contain("schema [ray]");
        ex.RepositoryVersion.Should().Be(0);
        ex.SupportedVersion.Should().Be(RepositorySchema.CurrentVersion);
        calls.Count.Should().Be(1, because: "nothing is written to a repository the engine refuses");
        context.MigrationState.MigratorMetaId.Should().Be(0);
    }

    [Fact]
    public void RepositoryCheckCreate_RepositoryNewerThanEngine_ThrowsBeforeAnyStep()
    {
        // Arrange
        var (executor, _, _, calls) = CreateExecutor($"{RepositorySchema.CurrentVersion + 3},1,RayMigrator repository has schema version [{RepositorySchema.CurrentVersion + 3}]", MetaRegistered);

        // Act
        var act = () => executor.RepositoryCheckCreate();

        // Assert
        var ex = act.Should().Throw<RepositorySchemaException>().Which;
        ex.Message.Should().Contain($"schema version [{RepositorySchema.CurrentVersion + 3}]").And.Contain($"version [{RepositorySchema.CurrentVersion}] at most").And.Contain("RayMigrator [3.0.0]");
        ex.RepositoryVersion.Should().Be(RepositorySchema.CurrentVersion + 3);
        calls.Count.Should().Be(1);
    }

    [Fact]
    public void RepositoryCheckCreate_StepReturnsAnotherCode_Throws()
    {
        // Arrange
        var (executor, _, _, _) = CreateExecutor(NoRepository, "99,wrong step", MetaRegistered);

        // Act
        var act = () => executor.RepositoryCheckCreate();

        // Assert
        act.Should().Throw<TemplateResultException>().Which.Message.Should().Contain("step [1]").And.Contain("[99]");
    }

    [Fact]
    public void RepositoryCheckCreate_BindsTheVersionAndDatabaseTypeParameters()
    {
        // Arrange
        var (executor, _, _, calls) = CreateExecutor(Results(NoRepository, [.. StepsFrom(1), MetaRegistered]));

        // Act
        executor.RepositoryCheckCreate();

        // Assert
        calls[0].Should().BeNull(because: "Repository_CheckCreate takes no parameters");
        for (int step = 1; step <= RepositorySchema.CurrentVersion; step++)
        {
            calls[step]!.TryGetValue("RayMigratorVersion", out var stepVersion).Should().BeTrue($"step {step} binds the engine version");
            stepVersion!.ParameterValue.Should().Be("3.0.0");
        }
        var meta = calls[^1]!;
        meta.TryGetValue("RayMigratorVersion", out var metaVersion).Should().BeTrue();
        metaVersion!.ParameterValue.Should().Be("3.0.0");
        meta.TryGetValue("RepositoryDatabaseType", out var databaseType).Should().BeTrue();
        databaseType!.ParameterValue.Should().Be("SqlServer");
    }

    [Fact]
    public void RepositoryCheckCreate_SensitiveDataNotRevealed_MasksTheSchemaName()
    {
        // Arrange
        using var scope = SensitiveDataMasker.BeginScope(revealSensitiveData: false);
        SensitiveDataMasker.RegisterSensitiveValue("ray");
        var (executor, logger, _, _) = CreateExecutor(Results(NoRepository, [.. StepsFrom(1), MetaRegistered]));

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
        var (executor, logger, _, _) = CreateExecutor(Results(NoRepository, [.. StepsFrom(1), MetaRegistered]));

        // Act
        executor.RepositoryCheckCreate();

        // Assert
        logger.Entries.Single(e => e.LogLevel == LogLevel.Information).Message.Should().Contain("schema [ray]");
    }

    #region Helpers

    /// <summary>
    /// Builds a TemplateExecutor with a real TemplateCache (test output DataAccessLayers/), a capturing logger and
    /// a mocked repository IDal whose ExecuteScalarAsync answers one call after the other from
    /// <paramref name="scalarResults"/> and records the parameter list of every call.
    /// </summary>
    private static (TemplateExecutor Executor, CapturingLogger<TemplateExecutor> Logger, MigrationContext Context, List<DalParameterList?> Calls) CreateExecutor(params string[] scalarResults)
    {
        var results = new Queue<string>(scalarResults);
        var calls = new List<DalParameterList?>();
        var dal = Substitute.For<IDal>();
        dal.DatabaseType.Returns("SqlServer");
        dal.ExecuteScalarAsync(Arg.Any<string>(), Arg.Any<IDalSettings>(), Arg.Any<DalParameterList?>(), Arg.Any<CancellationToken>())
           .Returns(callInfo =>
           {
               calls.Add(callInfo.ArgAt<DalParameterList?>(2));
               string result = results.Dequeue();
               if (result == Throw)
                   throw new InvalidOperationException("SQLite Error 1: 'duplicate column name: MigrationOperationId'.");
               return Task.FromResult<object?>(result);
           });

        var context = BuildContext();
        var templateCache = new TemplateCache(false, NullLogger<TemplateCache>.Instance);
        var logger = new CapturingLogger<TemplateExecutor>();
        var executor = new TemplateExecutor(templateCache, logger, new SingletonMigrationContextAccessor { Current = context });

        // Inject the repository DAL and options directly; DalFactory.TryGetDal would need a real driver.
        typeof(TemplateExecutor).GetField("_repositoryDalBacking", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(executor, dal);
        typeof(TemplateExecutor).GetField("_repositoryBacking", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(executor, context.RayMigratorOptions.Repository!);

        return (executor, logger, context, calls);
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
