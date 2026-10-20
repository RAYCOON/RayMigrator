using System.Reflection;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Raycoon.RayMigrator.Core;
using Raycoon.RayMigrator.Core.Configuration;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Core.Templates;
using Raycoon.RayMigrator.Database.Common;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1: The run details of schema version 2 reach the templates: <c>RepositoryMigrationRunInsert</c> binds the
/// operation, the source, the initiator and the host from the context, <c>RepositoryMigrationRunUpdate</c> binds the
/// error code and the message of a failed run, cut to 4000 characters. Uses a real TemplateCache from the test output
/// DataAccessLayers/ and a mocked IDal that records the parameter list.
/// </summary>
public class TemplateExecutorRunDetailsTests
{
    [Fact]
    public async Task RepositoryMigrationRunInsert_BindsOperationSourceInitiatorAndHost()
    {
        var (executor, calls) = CreateExecutor(MigrationRunSource.Studio, "Daniel", "oidc:123", "studio-host",
            ctx => ctx.MigrationState.MigrationOperation = MigrationOperation.Baseline);

        await executor.RepositoryMigrationRunInsertAsync("{}", cancellationToken: TestContext.Current.CancellationToken);

        var parameters = calls.Single()!;
        Value(parameters, "MigrationOperationId").Should().Be((byte)MigrationOperation.Baseline);
        Value(parameters, "MigrationRunSourceId").Should().Be((byte)MigrationRunSource.Studio);
        Value(parameters, "InitiatedBy").Should().Be("Daniel");
        Value(parameters, "InitiatedBySubject").Should().Be("oidc:123");
        Value(parameters, "Host").Should().Be("studio-host");
    }

    [Fact]
    public async Task RepositoryMigrationRunInsert_WithoutInitiator_BindsNullsAndTheMachineName()
    {
        var (executor, calls) = CreateExecutor(configureContext: ctx => ctx.MigrationState.MigrationOperation = MigrationOperation.MigrateUp);

        await executor.RepositoryMigrationRunInsertAsync("{}", cancellationToken: TestContext.Current.CancellationToken);

        var parameters = calls.Single()!;
        Value(parameters, "MigrationOperationId").Should().Be((byte)MigrationOperation.MigrateUp);
        Value(parameters, "MigrationRunSourceId").Should().BeNull("a host that does not name its source stores NULL");
        Parameter(parameters, "MigrationRunSourceId").ParameterType.Should().Be(typeof(byte));
        Value(parameters, "InitiatedBy").Should().BeNull();
        Value(parameters, "InitiatedBySubject").Should().BeNull();
        Value(parameters, "Host").Should().Be(Environment.MachineName, "the engine names the machine when the host does not");
    }

    [Fact]
    public async Task RepositoryMigrationRunInsert_UndefinedSource_BindsNull()
    {
        var (executor, calls) = CreateExecutor(MigrationRunSource.Undefined);

        await executor.RepositoryMigrationRunInsertAsync("{}", cancellationToken: TestContext.Current.CancellationToken);

        Value(calls.Single()!, "MigrationRunSourceId").Should().BeNull();
    }

    [Fact]
    public async Task RepositoryMigrationRunUpdate_WithError_BindsCodeAndMessage()
    {
        var (executor, calls) = CreateExecutor(configureContext: ctx => ctx.MigrationState.MigrationRunId = 5);

        await executor.RepositoryMigrationRunUpdateAsync(MigrationRunResult.Error, "-2", "it's broken", cancellationToken: TestContext.Current.CancellationToken);

        var parameters = calls.Single()!;
        Value(parameters, "MigrationRunResultId").Should().Be((byte)MigrationRunResult.Error);
        Value(parameters, "ErrorCode").Should().Be("-2");
        Value(parameters, "ErrorMessage").Should().Be("it's broken");
    }

    [Fact]
    public async Task RepositoryMigrationRunUpdate_Ok_BindsNullErrorDetails()
    {
        var (executor, calls) = CreateExecutor(configureContext: ctx => ctx.MigrationState.MigrationRunId = 5);

        await executor.RepositoryMigrationRunUpdateAsync(MigrationRunResult.Ok, cancellationToken: TestContext.Current.CancellationToken);

        var parameters = calls.Single()!;
        Value(parameters, "ErrorCode").Should().BeNull();
        Value(parameters, "ErrorMessage").Should().BeNull();
        Parameter(parameters, "ErrorMessage").ParameterType.Should().Be(typeof(string));
    }

    [Fact]
    public async Task RepositoryMigrationRunUpdate_LongMessage_IsCutToTheColumnLength()
    {
        var (executor, calls) = CreateExecutor(configureContext: ctx => ctx.MigrationState.MigrationRunId = 5);
        var message = new string('x', TemplateExecutor.MaxErrorMessageLength + 100);

        await executor.RepositoryMigrationRunUpdateAsync(MigrationRunResult.Error, null, message, cancellationToken: TestContext.Current.CancellationToken);

        ((string)Value(calls.Single()!, "ErrorMessage")!).Length.Should().Be(TemplateExecutor.MaxErrorMessageLength);
    }

    [Fact]
    public async Task RepositoryMigrationRunHeartbeat_BindsTheRunId_AndReportsAnOpenRow()
    {
        var (executor, calls) = CreateExecutor(configureContext: ctx => ctx.MigrationState.MigrationRunId = 5);

        bool open = await executor.RepositoryMigrationRunHeartbeatAsync(cancellationToken: TestContext.Current.CancellationToken);

        open.Should().BeTrue("the mocked template answers 1");
        Value(calls.Single()!, "MigrationRunId").Should().Be(5);
    }

    [Fact]
    public async Task RepositoryMigrationRunHeartbeat_WithoutRun_Throws()
    {
        var (executor, _) = CreateExecutor();

        var act = () => executor.RepositoryMigrationRunHeartbeatAsync();

        await act.Should().ThrowAsync<Raycoon.RayMigrator.Shared.Exceptions.RayMigratorInternalException>();
    }

    [Fact]
    public async Task RepositoryMigrationRunFixOrphaned_BindsTheRunIdAndTheThreshold()
    {
        var (executor, calls) = CreateExecutor();

        await executor.RepositoryMigrationRunFixOrphanedAsync(9, 2, cancellationToken: TestContext.Current.CancellationToken);

        var parameters = calls.Single()!;
        Value(parameters, "MigrationRunId").Should().Be(9);
        Value(parameters, "ThresholdMinutes").Should().Be(2);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("abc", "abc")]
    [InlineData("abcdef", "abcde")]
    public void Truncate_CutsToTheLengthAndMapsEmptyToNull(string? value, string? expected)
    {
        TemplateExecutor.Truncate(value, 5).Should().Be(expected);
    }

    #region Helpers

    private static object? Value(DalParameterList parameters, string name) => Parameter(parameters, name).ParameterValue;

    private static DalParameter Parameter(DalParameterList parameters, string name)
    {
        parameters.TryGetValue(name, out var parameter).Should().BeTrue($"the template needs the parameter {name}");
        return parameter!;
    }

    private static (TemplateExecutor Executor, List<DalParameterList?> Calls) CreateExecutor(
        MigrationRunSource? source = null, string? initiatedBy = null, string? initiatedBySubject = null, string? host = null,
        Action<MigrationContext>? configureContext = null)
    {
        var calls = new List<DalParameterList?>();
        var dal = Substitute.For<IDal>();
        dal.DatabaseType.Returns("SqlServer");
        dal.ExecuteScalarAsync(Arg.Any<string>(), Arg.Any<IDalSettings>(), Arg.Any<DalParameterList?>(), Arg.Any<CancellationToken>())
           .Returns(callInfo =>
           {
               calls.Add(callInfo.ArgAt<DalParameterList?>(2));
               return Task.FromResult<object?>("1,ok");
           });

        var context = BuildContext(source, initiatedBy, initiatedBySubject, host);
        configureContext?.Invoke(context);
        var executor = new TemplateExecutor(new TemplateCache(false, NullLogger<TemplateCache>.Instance), NullLogger<TemplateExecutor>.Instance, new SingletonMigrationContextAccessor { Current = context });
        typeof(TemplateExecutor).GetField("_repositoryDalBacking", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(executor, dal);
        typeof(TemplateExecutor).GetField("_repositoryBacking", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(executor, context.RayMigratorOptions.Repository!);
        return (executor, calls);
    }

    private static MigrationContext BuildContext(MigrationRunSource? source, string? initiatedBy, string? initiatedBySubject, string? host)
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
            RunMode = MigrationRunMode.Migrate, ShowStartupInfo = false, RevealSensitiveData = false,
            Source = source, InitiatedBy = initiatedBy, InitiatedBySubject = initiatedBySubject, Host = host
        };

        var context = new MigrationContext(rayOptions, consoleOptions, "3.0.0");
        context.MigrationState.EnvironmentId = 7;
        context.MigrationState.ProductId = 3;
        context.MigrationState.MigratorMetaId = 1;
        context.MigrationState.MigrationRunResult = MigrationRunResult.Running;
        return context;
    }

    #endregion
}
