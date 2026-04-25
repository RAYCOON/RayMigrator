
using System.Collections.Concurrent;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Raycoon.RayMigrator.Core.Configuration.Validation;
using Raycoon.RayMigrator.Database.Common;
using Raycoon.RayMigrator.Shared.Exceptions;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1: SchemaNameValidator tests — validates pipeline-level schema name validation
/// based on DalSpecificProperties.SupportsSchema.
/// </summary>
public class SchemaNameValidationTests
{
    private readonly ILogger _logger = Substitute.For<ILogger>();

    private static ConcurrentDictionary<string, DalSpecificProperties> CreateDict(string databaseType, bool supportsSchema)
    {
        var dict = new ConcurrentDictionary<string, DalSpecificProperties>();
        dict.TryAdd(databaseType, new DalSpecificProperties { SupportsSchema = supportsSchema });
        return dict;
    }

    [Fact]
    public void SupportsSchema_True_SchemaNameEmpty_ThrowsConfigurationValidationException()
    {
        var dict = CreateDict("SqlServer", true);

        var act = () => SchemaNameValidator.ValidateSchemaName(dict, "SqlServer", null, "Repository", _logger);

        act.Should().Throw<ConfigurationValidationException>()
            .WithMessage("*SchemaName is required*SqlServer*Repository*");
    }

    [Fact]
    public void SupportsSchema_True_SchemaNameProvided_NoException()
    {
        var dict = CreateDict("SqlServer", true);

        var act = () => SchemaNameValidator.ValidateSchemaName(dict, "SqlServer", "dbo", "Repository", _logger);

        act.Should().NotThrow();
    }

    [Fact]
    public void SupportsSchema_False_SchemaNameProvided_LogsWarning()
    {
        var dict = CreateDict("MariaDb", false);

        SchemaNameValidator.ValidateSchemaName(dict, "MariaDb", "myschema", "Repository", _logger);

        _logger.Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("will be ignored")),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public void SupportsSchema_False_SchemaNameEmpty_NoWarningNoException()
    {
        var dict = CreateDict("MariaDb", false);

        var act = () => SchemaNameValidator.ValidateSchemaName(dict, "MariaDb", null, "Repository", _logger);

        act.Should().NotThrow();
        _logger.DidNotReceive().Log(
            Arg.Any<LogLevel>(),
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public void DatabaseLogging_SupportsSchema_True_SchemaNameEmpty_ThrowsException()
    {
        var dict = CreateDict("PostgreSQL", true);

        var act = () => SchemaNameValidator.ValidateSchemaName(dict, "PostgreSQL", "", "DatabaseLogging", _logger);

        act.Should().Throw<ConfigurationValidationException>()
            .WithMessage("*SchemaName is required*PostgreSQL*DatabaseLogging*");
    }

    [Fact]
    public void DatabaseType_NotInDictionary_NoValidation()
    {
        var dict = new ConcurrentDictionary<string, DalSpecificProperties>();

        var act = () => SchemaNameValidator.ValidateSchemaName(dict, "UnknownDb", null, "Repository", _logger);

        act.Should().NotThrow();
    }
}
