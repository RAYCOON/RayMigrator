using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Raycoon.RayMigrator.Core;
using Raycoon.RayMigrator.Core.Configuration;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Services;
using Raycoon.RayMigrator.Shared.Exceptions;
using Raycoon.RayMigrator.Tests.Unit.Helpers;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1: <see cref="MigrationContextFactory"/> is the entry point of a host for one run: it prepares the options,
/// fills the DAL properties, and makes the context current for the calling async flow.
/// </summary>
[Collection("EnvironmentVariableTests")]
public class MigrationContextFactoryTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "raymigrator-ctxf-" + Guid.NewGuid().ToString("N"));
    private readonly List<string> _variables = [];

    public MigrationContextFactoryTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        foreach (var variable in _variables)
        {
            Environment.SetEnvironmentVariable(variable, null);
        }

        MigrationLoggingContext.Current = null;
        try { Directory.Delete(_dir, true); } catch { /* best effort */ }
    }

    private static MigrationContextFactory CreateFactory(IMigrationContextAccessor? accessor = null)
        => new(accessor ?? new SingletonMigrationContextAccessor(), NullLogger<MigrationContextFactory>.Instance);

    private RayMigratorOptions CreateOptions(string targetConnectionString = "Data Source=:memory:", string? databaseLoggingConnectionString = null)
    {
        var options = ProfileTestContext.CreateOptions(targetConnectionString, databaseLoggingConnectionString);
        options.Products![0].MigrationFilesRootDirectory = _dir;
        return options;
    }

    private static RayMigratorConsoleOptions ConsoleOptions(string product = ProfileTestContext.ProductAlias)
        => new()
        {
            Command = MigrationCommand.MigrateUp, Product = product, Environment = "Docker", RunMode = MigrationRunMode.Simulate,
            ShowStartupInfo = false, RevealSensitiveData = false, StopRollbackOnMissingRollbackFile = false
        };

    [Fact]
    public void CreateFromValidatedOptions_FillsDalPropertiesForRepositoryTargetsAndLogging()
    {
        using var maskerScope = SensitiveDataMasker.BeginScope(revealSensitiveData: false);
        var options = CreateOptions(databaseLoggingConnectionString: "Data Source=:memory:");
        options.Products![0].TargetGroups!.Add(new TargetGroupOptions
        {
            Alias = "Pg", DatabaseType = "PostgreSQL", TargetMigrationOrder = "FileByFile", HashValidationScope = "File",
            Targets = [new TargetOptions { Alias = "PgDb", ConnectionString = "Host=localhost;Database=raydb;Username=u;Password=p", DbCommandTimeoutInSeconds = 20, DbCommandMaxRetries = 0, DbCommandWaitTimeInMsBeforeRetry = 250 }]
        });

        var ctx = CreateFactory().CreateFromValidatedOptions(options, ConsoleOptions());

        ctx.DalSpecificPropertiesDictionary.Keys.Should().BeEquivalentTo(["Sqlite", "PostgreSQL"],
            "every DatabaseType of the repository, the product's targets and the database logging gets its DAL properties");
        ctx.DalSpecificPropertiesDictionary["PostgreSQL"].SqlBlockDelimiter.Should().NotBe("GO",
            "a host that skipped this step fell back to the SQL Server delimiter for every engine");
        ctx.RayMigratorConsoleOptions.StopRollbackOnMissingRollbackFile.Should().BeFalse("the console options are taken as given");
    }

    [Fact]
    public void Create_MakesTheContextCurrentForTheCallingFlow()
    {
        using var maskerScope = SensitiveDataMasker.BeginScope(revealSensitiveData: false);
        var accessor = new AsyncLocalMigrationContextAccessor();
        var factory = CreateFactory(accessor);

        var first = factory.Create(CreateOptions(), ConsoleOptions());
        accessor.Current.Should().BeSameAs(first);
        MigrationLoggingContext.Current.Should().BeSameAs(first, "the Serilog enricher reads the ambient logging context");

        var second = factory.Create(CreateOptions(), ConsoleOptions());
        accessor.Current.Should().BeSameAs(second, "a host creates one context per run and the newest one is current");
        MigrationLoggingContext.Current.Should().BeSameAs(second);
    }

    [Fact]
    public void Create_PreparesTheOptions()
    {
        using var maskerScope = SensitiveDataMasker.BeginScope(revealSensitiveData: false);
        string dirVar = "RM_CTXF_" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        Environment.SetEnvironmentVariable(dirVar, _dir);
        _variables.Add(dirVar);
        var options = CreateOptions($"Data Source={{ENV:{dirVar}}}/target.db");
        options.Repository!.DbCommandTimeoutInSeconds = null;

        var ctx = CreateFactory().Create(options, ConsoleOptions());

        ctx.RayMigratorOptions.Products![0].TargetGroups![0].Targets![0].ConnectionString.Should().Be($"Data Source={_dir}/target.db");
        ctx.RayMigratorOptions.Repository!.DbCommandTimeoutInSeconds.Should().Be(60);
    }

    [Fact]
    public void Create_UnknownProduct_ThrowsNamingTheAvailableAliases()
    {
        using var maskerScope = SensitiveDataMasker.BeginScope(revealSensitiveData: false);
        var act = () => CreateFactory().Create(CreateOptions(), ConsoleOptions(product: "Nope"));

        act.Should().Throw<ConfigurationValidationException>()
            .WithMessage("*[Nope]*")
            .WithMessage($"*{ProfileTestContext.ProductAlias}*");
    }

    [Fact]
    public void Create_ProductAliasWithWrongCase_HintsAtTheConfiguredAlias()
    {
        using var maskerScope = SensitiveDataMasker.BeginScope(revealSensitiveData: false);
        var act = () => CreateFactory().Create(CreateOptions(), ConsoleOptions(product: ProfileTestContext.ProductAlias.ToUpperInvariant()));

        act.Should().Throw<ConfigurationValidationException>().WithMessage("*Did you mean*");
    }

    [Fact]
    public void AddRayMigratorServices_ApiMode_ResolvesTheFactoryFromAScope()
    {
        using var maskerScope = SensitiveDataMasker.BeginScope(revealSensitiveData: false);
        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddRayMigratorServices(RayMigratorHostMode.Api);
        // ValidateOnBuild is left off: TemplateCache and TemplateExecutor are registered by the host, not by AddRayMigratorServices.
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

        using var scope = provider.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IMigrationContextFactory>();
        var ctx = factory.Create(CreateOptions(), ConsoleOptions());

        scope.ServiceProvider.GetRequiredService<IMigrationContextAccessor>().Current.Should().BeSameAs(ctx,
            "the accessor is a singleton backed by an AsyncLocal, so the factory is not a captive dependency");
    }
}
