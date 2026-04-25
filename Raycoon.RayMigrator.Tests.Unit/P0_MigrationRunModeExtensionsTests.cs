
using FluentAssertions;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Core.Extensions;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P0: MigrationRunMode extension methods.
/// Errors here lead to wrong execution paths (SQL executed when it shouldn't, repository skipped, etc.).
/// </summary>
public class MigrationRunModeExtensionsTests
{
    [Theory]
    [InlineData(MigrationRunMode.Undefined, false)]
    [InlineData(MigrationRunMode.Validate, false)]
    [InlineData(MigrationRunMode.Simulate, false)]
    [InlineData(MigrationRunMode.Migrate, true)]
    public void ShouldExecuteSql_ReturnsExpectedValue(MigrationRunMode runMode, bool expected)
    {
        runMode.ShouldExecuteSql().Should().Be(expected);
    }

    [Theory]
    [InlineData(MigrationRunMode.Undefined, false)]
    [InlineData(MigrationRunMode.Validate, false)]
    [InlineData(MigrationRunMode.Simulate, false)]
    [InlineData(MigrationRunMode.Migrate, true)]
    public void ShouldWriteRepository_ReturnsExpectedValue(MigrationRunMode runMode, bool expected)
    {
        runMode.ShouldWriteRepository().Should().Be(expected);
    }

    [Theory]
    [InlineData(MigrationRunMode.Undefined, false)]
    [InlineData(MigrationRunMode.Validate, false)]
    [InlineData(MigrationRunMode.Simulate, true)]
    [InlineData(MigrationRunMode.Migrate, true)]
    public void ShouldReadRepository_ReturnsExpectedValue(MigrationRunMode runMode, bool expected)
    {
        runMode.ShouldReadRepository().Should().Be(expected);
    }
}
