using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Core.Configuration.Validation;
using Raycoon.RayMigrator.Database.Common;
using Raycoon.RayMigrator.Infrastructure.Logging;
using Raycoon.RayMigrator.Pipeline;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1: Database logging goes into the repository by default (WP2, D5): <see cref="DatabaseLoggingDefaults"/> fills a
/// missing or partial <c>DatabaseLogging</c> section from <c>Repository</c> with level Information, <c>Enabled: false</c>
/// switches it off, the Serilog factory creates the sink by that rule, the preparer applies the defaults before
/// validation, the writer tolerates a missing level and the queue can be awaited without closing it.
/// </summary>
public class DatabaseLoggingDefaultsTests
{
    private static RayMigratorOptions Options(DatabaseLoggingOptions? logging) => new()
    {
        Repository = new RepositoryOptions
        {
            DatabaseType = "PostgreSQL", ConnectionString = "Host=repo;Database=ray", SchemaName = "ray", TableBaseName = "rm_",
            DbCommandTimeoutInSeconds = 60, DbCommandMaxRetries = 0, DbCommandWaitTimeInMsBeforeRetry = 0
        },
        DatabaseLogging = logging
    };

    [Fact]
    public void Apply_WithoutSection_LogsIntoTheRepositoryAtInformation()
    {
        var options = Options(null);

        DatabaseLoggingDefaults.Apply(options);

        var logging = options.DatabaseLogging.Should().NotBeNull().And.Subject.As<DatabaseLoggingOptions>();
        logging.Enabled.Should().BeNull();
        logging.DatabaseType.Should().Be("PostgreSQL");
        logging.ConnectionString.Should().Be("Host=repo;Database=ray");
        logging.SchemaName.Should().Be("ray");
        logging.TableBaseName.Should().Be("rm_");
        logging.MinimumLevel.Should().Be("Information");
        options.IsDatabaseLoggingEnabled().Should().BeTrue();
    }

    [Fact]
    public void Apply_Disabled_LeavesTheSectionAloneAndReportsOff()
    {
        var options = Options(new DatabaseLoggingOptions { Enabled = false });

        DatabaseLoggingDefaults.Apply(options);

        options.DatabaseLogging!.DatabaseType.Should().BeNull("a disabled section gets no defaults");
        options.DatabaseLogging.MinimumLevel.Should().BeNull();
        options.IsDatabaseLoggingEnabled().Should().BeFalse();
    }

    [Fact]
    public void Apply_PartialSection_FillsOnlyTheMissingValues()
    {
        var options = Options(new DatabaseLoggingOptions { MinimumLevel = "Debug", SchemaName = "logs" });

        DatabaseLoggingDefaults.Apply(options);

        options.DatabaseLogging!.MinimumLevel.Should().Be("Debug");
        options.DatabaseLogging.SchemaName.Should().Be("logs");
        options.DatabaseLogging.DatabaseType.Should().Be("PostgreSQL");
        options.DatabaseLogging.ConnectionString.Should().Be("Host=repo;Database=ray");
        options.DatabaseLogging.TableBaseName.Should().Be("rm_");
    }

    [Fact]
    public void Apply_OtherDatabaseType_DoesNotBorrowTheRepositoryConnection()
    {
        var options = Options(new DatabaseLoggingOptions { DatabaseType = "SqlServer" });

        DatabaseLoggingDefaults.Apply(options);

        options.DatabaseLogging!.ConnectionString.Should().BeNull("a repository connection string does not fit another engine");
        options.DatabaseLogging.SchemaName.Should().BeNull();
        options.DatabaseLogging.MinimumLevel.Should().Be("Information");
    }

    [Fact]
    public void Apply_Twice_IsIdempotent()
    {
        var options = Options(null);

        DatabaseLoggingDefaults.Apply(options);
        var first = options.DatabaseLogging;
        DatabaseLoggingDefaults.Apply(options);

        options.DatabaseLogging.Should().BeSameAs(first);
        options.DatabaseLogging!.ConnectionString.Should().Be("Host=repo;Database=ray");
    }

    [Fact]
    public void IsDatabaseLoggingEnabled_NullSection_IsFalseUntilApplied()
    {
        Options(null).IsDatabaseLoggingEnabled().Should().BeFalse();
        Options(new DatabaseLoggingOptions()).IsDatabaseLoggingEnabled().Should().BeTrue();
        Options(new DatabaseLoggingOptions { Enabled = true }).IsDatabaseLoggingEnabled().Should().BeTrue();
    }

    [Fact]
    public void PostConfigure_AppliesTheDefaultsBeforeValidation()
    {
        var options = Options(null);
        options.Products = [];

        new ProductDefaultsPostConfigureOptions().PostConfigure(null, options);

        options.DatabaseLogging!.ConnectionString.Should().Be("Host=repo;Database=ray");
        options.DatabaseLogging.MinimumLevel.Should().Be("Information");
    }

    [Fact]
    public void ToInput_InheritedConnectionString_IsReportedAsTheRepositoryWroteIt()
    {
        var options = Options(null);
        options.Repository!.ConnectionString = "Host=repo;Database=ray;Password=s3cret";
        DatabaseLoggingDefaults.Apply(options);
        var written = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["RayMigrator:Repository:ConnectionString"] = "{ENV:REPO}" })
            .Build().GetSection("RayMigrator");

        var input = OptionsValidationInputAdapter.ToInput(options, written);

        input.Repository!.ConnectionString.Should().Be("{ENV:REPO}");
        input.DatabaseLogging!.ConnectionString.Should().Be("{ENV:REPO}", "the inherited value must not be reported as a hardcoded credential");
        input.DatabaseLogging.EffectiveConnectionString.Should().Be("Host=repo;Database=ray;Password=s3cret");
    }

    [Fact]
    public void ToInput_OwnConnectionString_IsReportedAsWritten()
    {
        var options = Options(new DatabaseLoggingOptions { ConnectionString = "Host=logs;Database=log;Password=other" });
        DatabaseLoggingDefaults.Apply(options);

        var input = OptionsValidationInputAdapter.ToInput(options, null);

        input.DatabaseLogging!.ConnectionString.Should().Be("Host=logs;Database=log;Password=other");
    }

    [Fact]
    public void ToInput_Disabled_HasNoDatabaseLoggingInput()
    {
        var options = Options(new DatabaseLoggingOptions { Enabled = false });
        DatabaseLoggingDefaults.Apply(options);

        OptionsValidationInputAdapter.ToInput(options, null).DatabaseLogging.Should().BeNull();
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData("False", false)]
    [InlineData("yes", true)]
    public void SerilogFactory_CreatesTheSinkUnlessTheSectionSaysEnabledFalse(string? enabled, bool expectSink)
    {
        var values = new Dictionary<string, string?> { ["RayMigrator:Serilog:MinimumLevel:Default"] = "Warning" };
        if (enabled != null)
            values["RayMigrator:DatabaseLogging:Enabled"] = enabled;
        var section = new ConfigurationBuilder().AddInMemoryCollection(values).Build().GetSection("RayMigrator");

        SerilogFactory.IsDatabaseLoggingEnabled(section.GetSection("DatabaseLogging")).Should().Be(expectSink);
        var (_, sink) = SerilogFactory.Create(section);
        (sink != null).Should().Be(expectSink);
    }

    [Fact]
    public void DatabaseLogWriter_WithoutMinimumLevel_UsesInformation()
    {
        var options = Options(new DatabaseLoggingOptions { DatabaseType = "PostgreSQL", ConnectionString = "x", DbCommandTimeoutInSeconds = 20 });

        var writer = new DatabaseLogWriter(options, Substitute.For<IDal>());

        writer.MinimumLogLevel.Should().Be(LogLevel.Information);
    }

    [Fact]
    public async Task DatabaseLoggerQueue_WaitForEmpty_ReturnsWhenEveryEntryIsWritten()
    {
        var queue = new DatabaseLoggerQueue();
        int written = 0;
        queue.EnqueueLog(() => { Thread.Sleep(80); Interlocked.Increment(ref written); });
        queue.EnqueueLog(() => Interlocked.Increment(ref written));

        queue.IsIdle.Should().BeFalse();
        bool empty = await queue.WaitForEmptyAsync(TimeSpan.FromSeconds(5));

        empty.Should().BeTrue();
        written.Should().Be(2);
        queue.IsIdle.Should().BeTrue();
        queue.EnqueueLog(() => Interlocked.Increment(ref written));
        (await queue.WaitForEmptyAsync(TimeSpan.FromSeconds(5))).Should().BeTrue("the queue stays open after a wait");
        written.Should().Be(3);
    }

    [Fact]
    public async Task DatabaseLoggerQueue_WaitForEmpty_TimesOut()
    {
        var queue = new DatabaseLoggerQueue();
        queue.EnqueueLog(() => Thread.Sleep(500));

        (await queue.WaitForEmptyAsync(TimeSpan.FromMilliseconds(50))).Should().BeFalse();
    }
}
