using System.Data.Common;
using System.Reflection;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.Core;
using Raycoon.RayMigrator.Core;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Core.Templates;
using Raycoon.RayMigrator.Database.Common;
using Raycoon.RayMigrator.Shared.Exceptions;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1: Every method of <see cref="TemplateExecutor"/> hands its <see cref="CancellationToken"/> to the repository DAL,
/// and a call that the DAL ends as cancelled leaves the executor as the DAL's <see cref="OperationCanceledException"/>
/// instead of a <see cref="TemplateExecutionException"/>.
/// </summary>
public class TemplateExecutorCancellationTests
{
    private static readonly string ScalarResult = $"{RepositorySchema.CurrentVersion},0,ok";

    private static readonly Dictionary<string, Func<TemplateExecutor, CancellationToken, Task>> Calls = new()
    {
        ["RepositoryCheckCreateAsync"] = (e, ct) => e.RepositoryCheckCreateAsync(ct),
        ["RepositoryProductCheckInsertAsync"] = (e, ct) => e.RepositoryProductCheckInsertAsync(ct),
        ["RepositoryEnvironmentCheckInsertAsync"] = (e, ct) => e.RepositoryEnvironmentCheckInsertAsync(ct),
        ["RepositoryProductSelectAsync"] = (e, ct) => e.RepositoryProductSelectAsync(ct),
        ["RepositoryEnvironmentSelectAsync"] = (e, ct) => e.RepositoryEnvironmentSelectAsync(ct),
        ["RepositoryMigrationRunInsertAsync"] = (e, ct) => e.RepositoryMigrationRunInsertAsync("{}", ct),
        ["RepositoryMigrationRunUpdateAsync"] = (e, ct) => e.RepositoryMigrationRunUpdateAsync(MigrationRunResult.Ok, cancellationToken: ct),
        ["RepositoryMigrationRunSelectOrphanedAsync"] = (e, ct) => e.RepositoryMigrationRunSelectOrphanedAsync(3, 7, ct),
        ["RepositoryMigrationRunHeartbeatAsync"] = (e, ct) => e.RepositoryMigrationRunHeartbeatAsync(ct),
        ["RepositoryMigrationRunFixOrphanedAsync"] = (e, ct) => e.RepositoryMigrationRunFixOrphanedAsync(9, 2, ct),
        ["RepositoryMigrationRecordFixOrphanedAsync"] = (e, ct) => e.RepositoryMigrationRecordFixOrphanedAsync(9, MigrationStatus.NotMigrated, ct),
        ["RepositoryMigrationGetInterruptedAsync"] = (e, ct) => e.RepositoryMigrationGetInterruptedAsync(ct),
        ["RepositoryMigrationInsertAsync"] = (e, ct) => e.RepositoryMigrationInsertAsync(0, "001.sql", "Release_1.0", "Backend", "MainDB", 1, "h", null, "bh", 2, null, false, ct),
        ["RepositoryMigrationUpdateAsync"] = (e, ct) => e.RepositoryMigrationUpdateAsync(11, MigrationStatus.Executing, 1, ct),
        ["RepositoryMigrationUpdateAsync(shared)"] = (e, ct) => e.RepositoryMigrationUpdateAsync(11, MigrationStatus.Executing, 1, SharedConnection(), SharedTransaction(), 30, ct),
        ["RepositoryMigrationUpdateRollbackAsync"] = (e, ct) => e.RepositoryMigrationUpdateRollbackAsync(11, MigrationStatus.Executing, "h", null, "bh", 1, 2, null, ct),
        ["RepositoryMigrationUpdateRollbackAsync(shared)"] = (e, ct) => e.RepositoryMigrationUpdateRollbackAsync(11, MigrationStatus.Executing, "h", null, "bh", 1, 2, null, SharedConnection(), SharedTransaction(), 30, ct),
        ["RepositoryMigrationSelectAsync"] = (e, ct) => e.RepositoryMigrationSelectAsync(ct),
        ["RepositoryMigrationRecordHistorySelectAsync"] = (e, ct) => e.RepositoryMigrationRecordHistorySelectAsync(ct),
        ["RepositoryMigrationUpdateHashAsync"] = (e, ct) => e.RepositoryMigrationUpdateHashAsync(11, "h", null, "bh", ct),
        ["RepositoryMigrationRunSelectAsync"] = (e, ct) => e.RepositoryMigrationRunSelectAsync(5, ct),
    };

    public static TheoryData<string> CallNames => [.. Calls.Keys];

    [Fact]
    public void TestCases_EveryPublicRepositoryMethod_HasACase()
    {
        // Arrange
        var methods = typeof(TemplateExecutor).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => typeof(Task).IsAssignableFrom(m.ReturnType) && m.Name.StartsWith("Repository", StringComparison.Ordinal))
            .Select(m => m.GetParameters().Any(p => p.ParameterType == typeof(DbConnection)) ? $"{m.Name}(shared)" : m.Name)
            .ToList();

        // Assert
        methods.Should().BeEquivalentTo(Calls.Keys, because: "a repository method without a case here would escape the token test");
        typeof(TemplateExecutor).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => typeof(Task).IsAssignableFrom(m.ReturnType))
            .Should().OnlyContain(m => m.GetParameters().Last().ParameterType == typeof(CancellationToken),
                because: "every asynchronous method takes the token as its last parameter");
    }

    [Theory]
    [MemberData(nameof(CallNames))]
    public async Task RepositoryMethod_WithToken_HandsTheTokenToEveryDalCall(string call)
    {
        // Arrange
        var (executor, dal) = CreateExecutor();
        using var cts = new CancellationTokenSource();

        // Act
        await Calls[call](executor, cts.Token);

        // Assert
        var tokens = dal.ReceivedCalls().Select(TokenOf).Where(t => t != null).ToList();
        tokens.Should().NotBeEmpty(because: "the method reaches the DAL");
        tokens.Should().OnlyContain(t => t == cts.Token, because: "the executor passes the caller's token to the DAL");
    }

    [Fact]
    public async Task ExecuteScalarWithNegativeResultCodeExceptionAsync_WithToken_HandsTheTokenToTheDal()
    {
        // Arrange
        var (executor, dal) = CreateExecutor();
        var template = new TemplateCache(false, NullLogger<TemplateCache>.Instance)
            .GetRepositoryTemplate(TemplateType.Repository_MigrationRecord_Update, BuildContext().RayMigratorOptions.Repository!);
        using var cts = new CancellationTokenSource();

        // Act
        await executor.ExecuteScalarWithNegativeResultCodeExceptionAsync(template, dal, new DalSettings(), null, cancellationToken: cts.Token);
        await executor.ExecuteScalarWithNegativeResultCodeExceptionAsync(template, dal, SharedConnection(), SharedTransaction(), 30, null, cancellationToken: cts.Token);

        // Assert
        dal.ReceivedCalls().Select(TokenOf).Where(t => t != null).Should().Equal(cts.Token, cts.Token);
    }

    [Fact]
    public async Task RepositoryMigrationUpdateAsync_DalCancelled_ThrowsTheDalsOperationCanceledException()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var (executor, dal) = CreateExecutor();
        dal.ExecuteScalarAsync(Arg.Any<string>(), Arg.Any<IDalSettings>(), Arg.Any<DalParameterList?>(), Arg.Any<CancellationToken>())
            .Returns<Task<object?>>(_ =>
            {
                cts.Cancel();
                throw new OperationCanceledException(cts.Token);
            });

        // Act
        var act = () => executor.RepositoryMigrationUpdateAsync(11, MigrationStatus.Executing, 1, cts.Token);

        // Assert
        (await act.Should().ThrowExactlyAsync<OperationCanceledException>(
                because: "a cancellation is not a failed template, so it is not wrapped in a TemplateExecutionException"))
            .Which.CancellationToken.Should().Be(cts.Token);
    }

    [Fact]
    public async Task RepositoryMigrationSelectAsync_DalCancelled_ThrowsTheDalsOperationCanceledException()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var (executor, dal) = CreateExecutor();
        dal.ExecuteReaderAsync(Arg.Any<string>(), Arg.Any<IDalSettings>(), Arg.Any<DalParameterList?>(), Arg.Any<CancellationToken>())
            .Returns<Task<List<Dictionary<string, object?>>>>(_ =>
            {
                cts.Cancel();
                throw new OperationCanceledException(cts.Token);
            });

        // Act
        var act = () => executor.RepositoryMigrationSelectAsync(cts.Token);

        // Assert
        await act.Should().ThrowExactlyAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task RepositoryMigrationSelectAsync_DalFailsWithoutCancellation_ThrowsTemplateExecutionException()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var (executor, dal) = CreateExecutor();
        dal.ExecuteReaderAsync(Arg.Any<string>(), Arg.Any<IDalSettings>(), Arg.Any<DalParameterList?>(), Arg.Any<CancellationToken>())
            .Returns<Task<List<Dictionary<string, object?>>>>(_ => throw new InvalidOperationException("Invalid object name."));

        // Act
        var act = () => executor.RepositoryMigrationSelectAsync(cts.Token);

        // Assert
        (await act.Should().ThrowAsync<TemplateExecutionException>(because: "a failure stays a failed template as before"))
            .Which.InnerException.Should().BeOfType<InvalidOperationException>();
    }

    #region Helpers

    private static CancellationToken? TokenOf(ICall call) => call.GetArguments().OfType<CancellationToken>().Cast<CancellationToken?>().SingleOrDefault();

    private static DbConnection SharedConnection() => Substitute.For<DbConnection>();

    private static DbTransaction SharedTransaction() => Substitute.For<DbTransaction>();

    /// <summary>
    /// A TemplateExecutor with the real templates and a mocked repository DAL that answers every scalar with the current
    /// schema version (so that the schema check finds nothing to do) and every reader with no rows.
    /// </summary>
    private static (TemplateExecutor Executor, IDal Dal) CreateExecutor()
    {
        var dal = Substitute.For<IDal>();
        dal.DatabaseType.Returns("SqlServer");
        dal.ExecuteScalarAsync(Arg.Any<string>(), Arg.Any<IDalSettings>(), Arg.Any<DalParameterList?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<object?>(ScalarResult));
        dal.ExecuteScalarAsync(Arg.Any<string>(), Arg.Any<DbConnection>(), Arg.Any<DbTransaction>(), Arg.Any<int>(), Arg.Any<DalParameterList?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<object?>(ScalarResult));
        dal.ExecuteReaderAsync(Arg.Any<string>(), Arg.Any<IDalSettings>(), Arg.Any<DalParameterList?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new List<Dictionary<string, object?>>()));

        var context = BuildContext();
        var executor = new TemplateExecutor(new TemplateCache(false, NullLogger<TemplateCache>.Instance), NullLogger<TemplateExecutor>.Instance,
            new SingletonMigrationContextAccessor { Current = context });
        // The repository DAL comes from the DalFactory otherwise; reflection is the only way to put the mock in its place.
        typeof(TemplateExecutor).GetField("_repositoryDalBacking", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(executor, dal);
        typeof(TemplateExecutor).GetField("_repositoryBacking", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(executor, context.RayMigratorOptions.Repository!);
        return (executor, dal);
    }

    private static MigrationContext BuildContext()
    {
        var rayOptions = new RayMigratorOptions
        {
            Repository = new RepositoryOptions
            {
                DatabaseType = "SqlServer", ConnectionString = "Server=test", SchemaName = "ray", TableBaseName = "",
                DbCommandTimeoutInSeconds = 30, DbCommandMaxRetries = 0, DbCommandWaitTimeInMsBeforeRetry = 0
            },
            ProductDefaults = new ProductDefaultOptions("UTF-8")
            {
                MigrationErrorAction = "Terminate", MigrationFilesExtension = "sql", MigrationRollbackFilesPreExtension = "rollback",
                MigrationFilesEncoding = "UTF-8", RequireRollbackFile = false,
                TargetGroupDefaults = new TargetGroupDefaultOptions
                {
                    TargetMigrationOrder = "FileByFile", HashValidationScope = "File",
                    TargetDefaults = new TargetDefaultsOptions { DbCommandTimeoutInSeconds = 20, DbCommandMaxRetries = 0, DbCommandWaitTimeInMsBeforeRetry = 250 }
                }
            },
            Products =
            [
                new ProductOptions("rollback")
                {
                    Alias = "TestProduct", MigrationFilesRootDirectory = "/tmp", MigrationErrorAction = "Terminate",
                    MigrationFilesExtension = "sql", MigrationRollbackFilesPreExtension = "rollback", MigrationFilesEncoding = "UTF-8", RequireRollbackFile = false,
                    TargetGroups =
                    [
                        new TargetGroupOptions
                        {
                            Alias = "Backend", DatabaseType = "SqlServer", TargetMigrationOrder = "FileByFile", HashValidationScope = "File",
                            Targets = [new TargetOptions { Alias = "MainDB", ConnectionString = "Server=target", DbCommandTimeoutInSeconds = 20, DbCommandMaxRetries = 0, DbCommandWaitTimeInMsBeforeRetry = 250 }]
                        }
                    ]
                }
            ]
        };

        var consoleOptions = new RayMigratorConsoleOptions
        {
            Command = MigrationCommand.MigrateUp, Product = "TestProduct", Environment = "Docker",
            RunMode = MigrationRunMode.Migrate, ShowStartupInfo = false, RevealSensitiveData = false
        };

        var context = new MigrationContext(rayOptions, consoleOptions, "3.0.0");
        context.MigrationState.EnvironmentId = 7;
        context.MigrationState.ProductId = 3;
        context.MigrationState.MigrationRunId = 5;
        context.MigrationState.MigratorMetaId = 1;
        context.MigrationState.MigrationRunResult = MigrationRunResult.Running;
        return context;
    }

    #endregion Helpers
}
