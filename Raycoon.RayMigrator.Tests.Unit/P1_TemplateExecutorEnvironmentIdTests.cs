using System.Reflection;
using System.Runtime.CompilerServices;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Raycoon.RayMigrator.Core;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Core.Templates;
using Raycoon.RayMigrator.Database.Common;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1: Tests for TemplateExecutor parameter binding after the EnvironmentId FK feature.
/// Verifies that the five flipped methods pass @EnvironmentId (int) and NOT a text @Environment
/// parameter to IDal when called.
/// Uses a real TemplateCache (loaded from test output DataAccessLayers/) and a mocked IDal.
/// </summary>
public class TemplateExecutorEnvironmentIdTests
{
    #region Constants

    private const int TestEnvironmentId = 7;
    private const int TestProductId = 3;

    #endregion

    #region Helpers

    /// <summary>
    /// Creates a fully wired TemplateExecutor using a real TemplateCache (from test output DataAccessLayers/)
    /// and a mocked IDal. The IDal mock captures DalParameterList on every call.
    /// Returns the executor, the dal mock, and a getter that returns the last captured params.
    /// </summary>
    private static (TemplateExecutor executor, IDal dal, Func<DalParameterList?> getCapture) CreateExecutor(
        int environmentId = TestEnvironmentId,
        int productId = TestProductId)
    {
        var dal = Substitute.For<IDal>();
        DalParameterList? captured = null;

        dal.ExecuteScalarAsync(Arg.Any<string>(), Arg.Any<IDalSettings>(), Arg.Any<DalParameterList>())
           .Returns(callInfo =>
           {
               captured = callInfo.ArgAt<DalParameterList>(2);
               return Task.FromResult<object?>("1,ok");
           });

        dal.ExecuteReaderAsync(Arg.Any<string>(), Arg.Any<IDalSettings>(), Arg.Any<DalParameterList>())
           .Returns(callInfo =>
           {
               captured = callInfo.ArgAt<DalParameterList>(2);
               return Task.FromResult(new List<Dictionary<string, object?>>());
           });

        var ctx = BuildContext(environmentId, productId);
        var templateCache = BuildRealTemplateCache(ctx.RayMigratorOptions);

        // TemplateExecutor has a public constructor
        var accessor = new SingletonMigrationContextAccessor { Current = ctx };
        var executor = new TemplateExecutor(
            templateCache,
            NullLogger<TemplateExecutor>.Instance,
            accessor);

        // Inject the repository DAL directly (bypasses DalFactory.TryGetDal which requires real driver)
        var repoField = typeof(TemplateExecutor).GetField("_repositoryDalBacking",
            BindingFlags.NonPublic | BindingFlags.Instance);
        repoField?.SetValue(executor, dal);

        // Also inject the _repositoryBacking so the lazy init doesn't run
        var repoBacking = typeof(TemplateExecutor).GetField("_repositoryBacking",
            BindingFlags.NonPublic | BindingFlags.Instance);
        repoBacking?.SetValue(executor, ctx.RayMigratorOptions.Repository!);

        return (executor, dal, () => captured);
    }

    private static MigrationContext BuildContext(int environmentId, int productId)
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
                            TargetMigrationOrder = "Simultaneously",
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

        var ctx = new MigrationContext(rayOptions, consoleOptions, "3.0.0");
        ctx.MigrationState.EnvironmentId = environmentId;
        ctx.MigrationState.ProductId = productId;
        ctx.MigrationState.MigrationRunId = 1;
        ctx.MigrationState.MigrationRunResult = MigrationRunResult.Running;
        return ctx;
    }

    /// <summary>
    /// Builds a real TemplateCache from the DataAccessLayers in the test output directory.
    /// validateConfiguration=false so we don't need all products/targets to have matching templates.
    /// </summary>
    private static TemplateCache BuildRealTemplateCache(RayMigratorOptions options)
    {
        var opts = Options.Create(options);
        return new TemplateCache(opts, false, NullLogger<TemplateCache>.Instance, validateConfiguration: false);
    }

    /// <summary>
    /// Invokes a TemplateExecutor method (ignoring any exception from the stub result),
    /// then returns the captured DalParameterList. Fails the test if the DAL was never called.
    /// </summary>
    private static DalParameterList InvokeAndCapture(
        TemplateExecutor executor,
        Action<TemplateExecutor> act,
        Func<DalParameterList?> getCapture)
    {
        try { act(executor); } catch { /* DAL returns "1,ok" but some methods may still throw on result parsing */ }

        getCapture().Should().NotBeNull("DAL must be called so parameters are captured");
        return getCapture()!;
    }

    #endregion

    #region RepositoryMigrationRunInsert — passes EnvironmentId int, not text @Environment

    [Fact]
    public void RepositoryMigrationRunInsert_PassesEnvironmentIdInt()
    {
        var (executor, _, getCapture) = CreateExecutor(environmentId: TestEnvironmentId);

        InvokeAndCapture(executor, e => e.RepositoryMigrationRunInsert("{}"), getCapture);

        var captured = getCapture()!;
        captured.TryGetValue("EnvironmentId", out var param).Should().BeTrue(
            "RepositoryMigrationRunInsert must add a parameter named 'EnvironmentId'");
        param!.ParameterType.Should().Be(typeof(int),
            "EnvironmentId must be bound as int (not string)");
        param.ParameterValue.Should().Be(TestEnvironmentId);
    }

    [Fact]
    public void RepositoryMigrationRunInsert_DoesNotPassTextEnvironmentParameter()
    {
        var (executor, _, getCapture) = CreateExecutor();

        InvokeAndCapture(executor, e => e.RepositoryMigrationRunInsert("{}"), getCapture);

        var captured = getCapture()!;
        captured.TryGetValue("Environment", out _).Should().BeFalse(
            "RepositoryMigrationRunInsert must NOT add a text 'Environment' parameter after EnvironmentId FK refactor");
    }

    #endregion

    #region RepositoryMigrationGetInterrupted — passes EnvironmentId int

    [Fact]
    public void RepositoryMigrationGetInterrupted_PassesEnvironmentIdInt()
    {
        var (executor, _, getCapture) = CreateExecutor(environmentId: TestEnvironmentId);

        InvokeAndCapture(executor, e => e.RepositoryMigrationGetInterrupted(), getCapture);

        var captured = getCapture()!;
        captured.TryGetValue("EnvironmentId", out var param).Should().BeTrue(
            "RepositoryMigrationGetInterrupted must add a parameter named 'EnvironmentId'");
        param!.ParameterType.Should().Be(typeof(int));
        param.ParameterValue.Should().Be(TestEnvironmentId);
    }

    [Fact]
    public void RepositoryMigrationGetInterrupted_DoesNotPassTextEnvironmentParameter()
    {
        var (executor, _, getCapture) = CreateExecutor();

        InvokeAndCapture(executor, e => e.RepositoryMigrationGetInterrupted(), getCapture);

        var captured = getCapture()!;
        captured.TryGetValue("Environment", out _).Should().BeFalse(
            "RepositoryMigrationGetInterrupted must NOT add a text 'Environment' parameter");
    }

    #endregion

    #region RepositoryMigrationInsert — passes EnvironmentId int

    [Fact]
    public void RepositoryMigrationInsert_PassesEnvironmentIdInt()
    {
        var (executor, _, getCapture) = CreateExecutor(environmentId: TestEnvironmentId);

        InvokeAndCapture(executor, e => e.RepositoryMigrationInsert(
            existingMigrationRecordId: 0,
            filename: "10_Create.sql",
            releaseVersion: "Release 1.0",
            targetGroupAlias: "Backend",
            targetAlias: "MainDB",
            fileOrderId: 1,
            fileUpHash: "abc",
            fileUpConfigHash: null,
            fileUpBlocksHash: "def",
            fileUpBlocksTotal: 2,
            fileUpConfigJson: null,
            migrateDownFileExists: false), getCapture);

        var captured = getCapture()!;
        captured.TryGetValue("EnvironmentId", out var param).Should().BeTrue(
            "RepositoryMigrationInsert must add a parameter named 'EnvironmentId'");
        param!.ParameterType.Should().Be(typeof(int));
        param.ParameterValue.Should().Be(TestEnvironmentId);
    }

    [Fact]
    public void RepositoryMigrationInsert_DoesNotPassTextEnvironmentParameter()
    {
        var (executor, _, getCapture) = CreateExecutor();

        InvokeAndCapture(executor, e => e.RepositoryMigrationInsert(
            existingMigrationRecordId: 0,
            filename: "10_Create.sql",
            releaseVersion: "Release 1.0",
            targetGroupAlias: "Backend",
            targetAlias: "MainDB",
            fileOrderId: 1,
            fileUpHash: "abc",
            fileUpConfigHash: null,
            fileUpBlocksHash: "def",
            fileUpBlocksTotal: 2,
            fileUpConfigJson: null,
            migrateDownFileExists: false), getCapture);

        var captured = getCapture()!;
        captured.TryGetValue("Environment", out _).Should().BeFalse(
            "RepositoryMigrationInsert must NOT add a text 'Environment' parameter");
    }

    #endregion

    #region RepositoryMigrationSelect — passes EnvironmentId int

    [Fact]
    public void RepositoryMigrationSelect_PassesEnvironmentIdInt()
    {
        var (executor, _, getCapture) = CreateExecutor(environmentId: TestEnvironmentId);

        InvokeAndCapture(executor, e => e.RepositoryMigrationSelect(), getCapture);

        var captured = getCapture()!;
        captured.TryGetValue("EnvironmentId", out var param).Should().BeTrue(
            "RepositoryMigrationSelect must add a parameter named 'EnvironmentId'");
        param!.ParameterType.Should().Be(typeof(int));
        param.ParameterValue.Should().Be(TestEnvironmentId);
    }

    [Fact]
    public void RepositoryMigrationSelect_DoesNotPassTextEnvironmentParameter()
    {
        var (executor, _, getCapture) = CreateExecutor();

        InvokeAndCapture(executor, e => e.RepositoryMigrationSelect(), getCapture);

        var captured = getCapture()!;
        captured.TryGetValue("Environment", out _).Should().BeFalse(
            "RepositoryMigrationSelect must NOT add a text 'Environment' parameter");
    }

    #endregion

    #region RepositoryMigrationRunSelectOrphaned — accepts int environmentId, passes it correctly

    [Fact]
    public void RepositoryMigrationRunSelectOrphaned_AcceptsIntSignature()
    {
        // Verify method signature: second param must be int (not string)
        var method = typeof(TemplateExecutor).GetMethod(
            "RepositoryMigrationRunSelectOrphaned",
            BindingFlags.Public | BindingFlags.Instance);

        method.Should().NotBeNull("method must exist");
        var parameters = method!.GetParameters();
        parameters.Should().HaveCount(2, "RepositoryMigrationRunSelectOrphaned(int productId, int environmentId)");
        parameters[0].Name.Should().Be("productId");
        parameters[0].ParameterType.Should().Be(typeof(int));
        parameters[1].Name.Should().Be("environmentId");
        parameters[1].ParameterType.Should().Be(typeof(int),
            "environmentId second parameter must be int after EnvironmentId FK refactor (was text before)");
    }

    [Fact]
    public void RepositoryMigrationRunSelectOrphaned_PassesEnvironmentIdInt()
    {
        var (executor, _, getCapture) = CreateExecutor(environmentId: TestEnvironmentId);

        InvokeAndCapture(executor,
            e => e.RepositoryMigrationRunSelectOrphaned(productId: TestProductId, environmentId: TestEnvironmentId),
            getCapture);

        var captured = getCapture()!;
        captured.TryGetValue("EnvironmentId", out var param).Should().BeTrue(
            "RepositoryMigrationRunSelectOrphaned must add a parameter named 'EnvironmentId'");
        param!.ParameterType.Should().Be(typeof(int));
        param.ParameterValue.Should().Be(TestEnvironmentId);
    }

    [Fact]
    public void RepositoryMigrationRunSelectOrphaned_DoesNotPassTextEnvironmentParameter()
    {
        var (executor, _, getCapture) = CreateExecutor();

        InvokeAndCapture(executor,
            e => e.RepositoryMigrationRunSelectOrphaned(productId: TestProductId, environmentId: TestEnvironmentId),
            getCapture);

        var captured = getCapture()!;
        captured.TryGetValue("Environment", out _).Should().BeFalse(
            "RepositoryMigrationRunSelectOrphaned must NOT add a text 'Environment' parameter");
    }

    #endregion
}
