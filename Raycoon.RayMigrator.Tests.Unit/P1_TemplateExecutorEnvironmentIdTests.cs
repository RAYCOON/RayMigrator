using System.Reflection;
using System.Runtime.CompilerServices;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
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
        int productId = TestProductId,
        MigrationRunMode runMode = MigrationRunMode.Migrate,
        string scalarResult = "1,ok",
        List<Dictionary<string, object?>>? readerRows = null,
        Action<MigrationContext>? configureContext = null)
    {
        var dal = Substitute.For<IDal>();
        DalParameterList? captured = null;

        dal.ExecuteScalarAsync(Arg.Any<string>(), Arg.Any<IDalSettings>(), Arg.Any<DalParameterList>(), Arg.Any<CancellationToken>())
           .Returns(callInfo =>
           {
               captured = callInfo.ArgAt<DalParameterList>(2);
               return Task.FromResult<object?>(scalarResult);
           });

        dal.ExecuteReaderAsync(Arg.Any<string>(), Arg.Any<IDalSettings>(), Arg.Any<DalParameterList>(), Arg.Any<CancellationToken>())
           .Returns(callInfo =>
           {
               captured = callInfo.ArgAt<DalParameterList>(2);
               return Task.FromResult(readerRows ?? new List<Dictionary<string, object?>>());
           });

        var ctx = BuildContext(environmentId, productId, runMode);
        configureContext?.Invoke(ctx);
        var templateCache = BuildRealTemplateCache();

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

    private static MigrationContext BuildContext(int environmentId, int productId, MigrationRunMode runMode = MigrationRunMode.Migrate)
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
            RunMode = runMode,
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
    /// The cache is not validated against the options, so not every product needs matching templates.
    /// </summary>
    private static TemplateCache BuildRealTemplateCache()
    {
        return new TemplateCache(false, NullLogger<TemplateCache>.Instance);
    }

    /// <summary>
    /// Invokes a TemplateExecutor method (ignoring any exception from the stub result),
    /// then returns the captured DalParameterList. Fails the test if the DAL was never called.
    /// </summary>
    private static async Task<DalParameterList> InvokeAndCaptureAsync(
        TemplateExecutor executor,
        Func<TemplateExecutor, Task> act,
        Func<DalParameterList?> getCapture)
    {
        try { await act(executor); } catch { /* DAL returns "1,ok" but some methods may still throw on result parsing */ }

        getCapture().Should().NotBeNull("DAL must be called so parameters are captured");
        return getCapture()!;
    }

    #endregion

    #region RepositoryMigrationRunInsert — passes EnvironmentId int, not text @Environment

    [Fact]
    public async Task RepositoryMigrationRunInsert_PassesEnvironmentIdInt()
    {
        var (executor, _, getCapture) = CreateExecutor(environmentId: TestEnvironmentId);

        await InvokeAndCaptureAsync(executor, e => e.RepositoryMigrationRunInsertAsync("{}"), getCapture);

        var captured = getCapture()!;
        captured.TryGetValue("EnvironmentId", out var param).Should().BeTrue(
            "RepositoryMigrationRunInsert must add a parameter named 'EnvironmentId'");
        param!.ParameterType.Should().Be(typeof(int),
            "EnvironmentId must be bound as int (not string)");
        param.ParameterValue.Should().Be(TestEnvironmentId);
    }

    [Fact]
    public async Task RepositoryMigrationRunInsert_PassesTheReleaseTheRunStartsFrom()
    {
        var (executor, _, getCapture) = CreateExecutor(configureContext: ctx => ctx.MigrationState.FromReleaseVersion = "Release_1.0");

        await InvokeAndCaptureAsync(executor, e => e.RepositoryMigrationRunInsertAsync("{}"), getCapture);

        var captured = getCapture()!;
        captured.TryGetValue("FromReleaseVersion", out var param).Should().BeTrue();
        param!.ParameterValue.Should().Be("Release_1.0", "the run row records the highest Migrated release when the run started");
    }

    [Fact]
    public async Task RepositoryMigrationRunInsert_FirstRun_PassesNullFromReleaseVersion()
    {
        var (executor, _, getCapture) = CreateExecutor();

        await InvokeAndCaptureAsync(executor, e => e.RepositoryMigrationRunInsertAsync("{}"), getCapture);

        var captured = getCapture()!;
        captured.TryGetValue("FromReleaseVersion", out var param).Should().BeTrue();
        param!.ParameterValue.Should().BeNull("nothing was migrated before a first run; the DAL binds null as DB NULL");
        param.ParameterType.Should().Be(typeof(string));
    }

    [Fact]
    public async Task RepositoryMigrationRunInsert_DoesNotPassTextEnvironmentParameter()
    {
        var (executor, _, getCapture) = CreateExecutor();

        await InvokeAndCaptureAsync(executor, e => e.RepositoryMigrationRunInsertAsync("{}"), getCapture);

        var captured = getCapture()!;
        captured.TryGetValue("Environment", out _).Should().BeFalse(
            "RepositoryMigrationRunInsert must NOT add a text 'Environment' parameter after EnvironmentId FK refactor");
    }

    #endregion

    #region RepositoryMigrationGetInterrupted — passes EnvironmentId int

    [Fact]
    public async Task RepositoryMigrationGetInterrupted_PassesEnvironmentIdInt()
    {
        var (executor, _, getCapture) = CreateExecutor(environmentId: TestEnvironmentId);

        await InvokeAndCaptureAsync(executor, e => e.RepositoryMigrationGetInterruptedAsync(), getCapture);

        var captured = getCapture()!;
        captured.TryGetValue("EnvironmentId", out var param).Should().BeTrue(
            "RepositoryMigrationGetInterrupted must add a parameter named 'EnvironmentId'");
        param!.ParameterType.Should().Be(typeof(int));
        param.ParameterValue.Should().Be(TestEnvironmentId);
    }

    [Fact]
    public async Task RepositoryMigrationGetInterrupted_DoesNotPassTextEnvironmentParameter()
    {
        var (executor, _, getCapture) = CreateExecutor();

        await InvokeAndCaptureAsync(executor, e => e.RepositoryMigrationGetInterruptedAsync(), getCapture);

        var captured = getCapture()!;
        captured.TryGetValue("Environment", out _).Should().BeFalse(
            "RepositoryMigrationGetInterrupted must NOT add a text 'Environment' parameter");
    }

    #endregion

    #region RepositoryMigrationInsert — passes EnvironmentId int

    [Fact]
    public async Task RepositoryMigrationInsert_PassesEnvironmentIdInt()
    {
        var (executor, _, getCapture) = CreateExecutor(environmentId: TestEnvironmentId);

        await InvokeAndCaptureAsync(executor, e => e.RepositoryMigrationInsertAsync(
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
    public async Task RepositoryMigrationInsert_DoesNotPassTextEnvironmentParameter()
    {
        var (executor, _, getCapture) = CreateExecutor();

        await InvokeAndCaptureAsync(executor, e => e.RepositoryMigrationInsertAsync(
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
    public async Task RepositoryMigrationSelect_PassesEnvironmentIdInt()
    {
        var (executor, _, getCapture) = CreateExecutor(environmentId: TestEnvironmentId);

        await InvokeAndCaptureAsync(executor, e => e.RepositoryMigrationSelectAsync(), getCapture);

        var captured = getCapture()!;
        captured.TryGetValue("EnvironmentId", out var param).Should().BeTrue(
            "RepositoryMigrationSelect must add a parameter named 'EnvironmentId'");
        param!.ParameterType.Should().Be(typeof(int));
        param.ParameterValue.Should().Be(TestEnvironmentId);
    }

    [Fact]
    public async Task RepositoryMigrationSelect_DoesNotPassTextEnvironmentParameter()
    {
        var (executor, _, getCapture) = CreateExecutor();

        await InvokeAndCaptureAsync(executor, e => e.RepositoryMigrationSelectAsync(), getCapture);

        var captured = getCapture()!;
        captured.TryGetValue("Environment", out _).Should().BeFalse(
            "RepositoryMigrationSelect must NOT add a text 'Environment' parameter");
    }

    #endregion

    #region RepositoryProductSelect / RepositoryEnvironmentSelect — read-only id lookups (#7)

    private static MigrationContext ContextOf(TemplateExecutor executor)
    {
        var field = typeof(TemplateExecutor).GetField("_ctxAccessor", BindingFlags.NonPublic | BindingFlags.Instance);
        field.Should().NotBeNull("TemplateExecutor keeps its IMigrationContextAccessor in a field named _ctxAccessor");
        return ((IMigrationContextAccessor)field!.GetValue(executor)!).Current;
    }

    [Fact]
    public async Task RepositoryProductSelect_PassesNameAndNameLower_AndSetsProductIdFromResult()
    {
        var (executor, _, getCapture) = CreateExecutor(productId: 0, runMode: MigrationRunMode.Simulate, scalarResult: "42,Product [TestProduct] with Id [42] found");

        bool found = await executor.RepositoryProductSelectAsync(cancellationToken: TestContext.Current.CancellationToken);

        found.Should().BeTrue();
        ContextOf(executor).MigrationState.ProductId.Should().Be(42, "the id returned by the template becomes MigrationState.ProductId");
        var captured = getCapture()!;
        captured.TryGetValue("Name", out var name).Should().BeTrue();
        name!.ParameterValue.Should().Be("TestProduct");
        captured.TryGetValue("NameLower", out var nameLower).Should().BeTrue();
        nameLower!.ParameterValue.Should().Be("testproduct", "the lookup key is pre-computed lowercase, like the CheckInsert template");
    }

    [Fact]
    public async Task RepositoryProductSelect_NotFound_ReturnsFalseAndLeavesProductIdZero()
    {
        var (executor, _, _) = CreateExecutor(productId: 0, runMode: MigrationRunMode.Simulate, scalarResult: "0,Product [TestProduct] not found");

        bool found = await executor.RepositoryProductSelectAsync(cancellationToken: TestContext.Current.CancellationToken);

        found.Should().BeFalse("result code 0 means the product is not registered yet — not an error");
        ContextOf(executor).MigrationState.ProductId.Should().Be(0);
    }

    [Fact]
    public async Task RepositoryEnvironmentSelect_PassesNameAndNameLower_AndSetsEnvironmentIdFromResult()
    {
        var (executor, _, getCapture) = CreateExecutor(environmentId: 0, runMode: MigrationRunMode.Simulate, scalarResult: "7,Environment [Docker] with Id [7] found");

        bool found = await executor.RepositoryEnvironmentSelectAsync(cancellationToken: TestContext.Current.CancellationToken);

        found.Should().BeTrue();
        ContextOf(executor).MigrationState.EnvironmentId.Should().Be(7);
        var captured = getCapture()!;
        captured.TryGetValue("Name", out var name).Should().BeTrue();
        name!.ParameterValue.Should().Be("Docker");
        captured.TryGetValue("NameLower", out var nameLower).Should().BeTrue();
        nameLower!.ParameterValue.Should().Be("docker");
    }

    [Fact]
    public async Task RepositoryProductSelect_NegativeResultCode_ThrowsLikeCheckInsert()
    {
        var (executor, _, _) = CreateExecutor(productId: 0, runMode: MigrationRunMode.Simulate, scalarResult: "-1,lookup failed");

        var act = () => executor.RepositoryProductSelectAsync();

        await act.Should().ThrowAsync<Exception>("a negative result code is an error for the select exactly as for the CheckInsert template");
        ContextOf(executor).MigrationState.ProductId.Should().Be(0, "a failed lookup must not leave a partial id behind");
    }

    [Fact]
    public async Task RepositoryEnvironmentSelect_NotFound_ReturnsFalseAndLeavesEnvironmentIdZero()
    {
        var (executor, _, _) = CreateExecutor(environmentId: 0, runMode: MigrationRunMode.Simulate, scalarResult: "0,Environment [Docker] not found");

        bool found = await executor.RepositoryEnvironmentSelectAsync(cancellationToken: TestContext.Current.CancellationToken);

        found.Should().BeFalse();
        ContextOf(executor).MigrationState.EnvironmentId.Should().Be(0);
    }

    #endregion

    #region RepositoryMigrationSelect — config hash of TOML-less files (#9)

    private static Dictionary<string, object?> RecordRow(object? fileUpConfigHash, object? fileDownConfigHash) => new()
    {
        ["Id"] = 1, ["ProductId"] = TestProductId, ["MigrationRunId"] = 1,
        ["MigrationOperationId"] = (byte)MigrationOperation.MigrateUp, ["MigrationStatusId"] = (byte)MigrationStatus.Migrated,
        ["ReleaseVersion"] = "Release 1.0", ["TargetGroupAlias"] = "Backend", ["TargetAlias"] = "MainDB",
        ["Filename"] = "10_Create.sql", ["FileOrderId"] = 1,
        ["FileUpHash"] = "abc", ["FileUpConfigHash"] = fileUpConfigHash, ["FileUpBlocksHash"] = "def",
        ["FileUpBlocksMigrated"] = 1, ["FileUpBlocksTotal"] = 1, ["MigrateDownFileExists"] = false,
        ["FileDownHash"] = null, ["FileDownConfigHash"] = fileDownConfigHash, ["FileDownBlocksHash"] = null,
        ["FileDownBlocksMigrated"] = null, ["FileDownBlocksTotal"] = null
    };

    [Fact]
    public async Task RepositoryMigrationSelect_NullConfigHash_IsReadAsNull()
    {
        // A file without a TOML block has a null config hash; the insert templates store NULL, so the
        // repository value and a freshly parsed file compare equal without any normalisation (#9).
        var (executor, _, _) = CreateExecutor(readerRows: new List<Dictionary<string, object?>> { RecordRow(null, null) });

        var records = await executor.RepositoryMigrationSelectAsync(cancellationToken: TestContext.Current.CancellationToken);

        records.Should().ContainSingle();
        records[0].FileUpConfigHash.Should().BeNull();
        records[0].FileDownConfigHash.Should().BeNull();
    }

    [Fact]
    public async Task RepositoryMigrationSelect_NonEmptyConfigHash_IsKept()
    {
        var (executor, _, _) = CreateExecutor(readerRows: new List<Dictionary<string, object?>> { RecordRow("cfg-up", "cfg-down") });

        var records = await executor.RepositoryMigrationSelectAsync(cancellationToken: TestContext.Current.CancellationToken);

        records[0].FileUpConfigHash.Should().Be("cfg-up");
        records[0].FileDownConfigHash.Should().Be("cfg-down");
    }

    #endregion

    #region RepositoryMigrationSelect — always queries Migrate-mode records (#5)

    /// <summary>
    /// #5: MigrationRecord rows are only written in Migrate mode, so the select must filter on
    /// MigrationRunModeId = 100 no matter which run mode the current command runs in.
    /// Before the fix, validate-hash (RunMode = Validate) queried with 10 and never found a record.
    /// </summary>
    [Theory]
    [InlineData(MigrationRunMode.Migrate)]
    [InlineData(MigrationRunMode.Simulate)]
    [InlineData(MigrationRunMode.Validate)]
    public async Task RepositoryMigrationSelect_AlwaysFiltersOnMigrateRunMode(MigrationRunMode contextRunMode)
    {
        var (executor, _, getCapture) = CreateExecutor(runMode: contextRunMode);

        await InvokeAndCaptureAsync(executor, e => e.RepositoryMigrationSelectAsync(), getCapture);

        var captured = getCapture()!;
        captured.TryGetValue("MigrationRunModeId", out var param).Should().BeTrue(
            "RepositoryMigrationSelect must add a parameter named 'MigrationRunModeId'");
        param!.ParameterType.Should().Be(typeof(byte));
        param.ParameterValue.Should().Be((byte)MigrationRunMode.Migrate,
            $"records are only ever written in Migrate mode, so a command running in {contextRunMode} mode must still query for Migrate records");
    }

    [Fact]
    public void RepositoryMigrationSelect_HasNoRunModeParameter()
    {
        // The overload that let callers pass a run mode was the trap behind #5: every caller that
        // forgot it inherited the context run mode. No overload may take a run mode again.
        var runModeParameters = typeof(TemplateExecutor).GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name == "RepositoryMigrationSelectAsync")
            .SelectMany(m => m.GetParameters())
            .Where(p => p.ParameterType == typeof(MigrationRunMode) || p.ParameterType == typeof(MigrationRunMode?))
            .ToList();

        runModeParameters.Should().BeEmpty(
            "the run mode used for the record query must not be a caller decision");
    }

    #endregion

    #region RepositoryMigrationRunSelectOrphaned — accepts int environmentId, passes it correctly

    [Fact]
    public void RepositoryMigrationRunSelectOrphaned_AcceptsIntSignature()
    {
        // Verify method signature: second param must be int (not string)
        var method = typeof(TemplateExecutor).GetMethod(
            "RepositoryMigrationRunSelectOrphanedAsync",
            BindingFlags.Public | BindingFlags.Instance);

        method.Should().NotBeNull("method must exist");
        var parameters = method!.GetParameters();
        parameters.Should().HaveCount(3, "RepositoryMigrationRunSelectOrphanedAsync(int productId, int environmentId, CancellationToken cancellationToken)");
        parameters[0].Name.Should().Be("productId");
        parameters[0].ParameterType.Should().Be(typeof(int));
        parameters[1].Name.Should().Be("environmentId");
        parameters[1].ParameterType.Should().Be(typeof(int),
            "environmentId second parameter must be int after EnvironmentId FK refactor (was text before)");
        parameters[2].ParameterType.Should().Be(typeof(CancellationToken));
    }

    [Fact]
    public async Task RepositoryMigrationRunSelectOrphaned_PassesEnvironmentIdInt()
    {
        var (executor, _, getCapture) = CreateExecutor(environmentId: TestEnvironmentId);

        await InvokeAndCaptureAsync(executor,
            e => e.RepositoryMigrationRunSelectOrphanedAsync(productId: TestProductId, environmentId: TestEnvironmentId),
            getCapture);

        var captured = getCapture()!;
        captured.TryGetValue("EnvironmentId", out var param).Should().BeTrue(
            "RepositoryMigrationRunSelectOrphaned must add a parameter named 'EnvironmentId'");
        param!.ParameterType.Should().Be(typeof(int));
        param.ParameterValue.Should().Be(TestEnvironmentId);
    }

    [Fact]
    public async Task RepositoryMigrationRunSelectOrphaned_DoesNotPassTextEnvironmentParameter()
    {
        var (executor, _, getCapture) = CreateExecutor();

        await InvokeAndCaptureAsync(executor,
            e => e.RepositoryMigrationRunSelectOrphanedAsync(productId: TestProductId, environmentId: TestEnvironmentId),
            getCapture);

        var captured = getCapture()!;
        captured.TryGetValue("Environment", out _).Should().BeFalse(
            "RepositoryMigrationRunSelectOrphaned must NOT add a text 'Environment' parameter");
    }

    #endregion
}
