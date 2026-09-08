using AwesomeAssertions;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Core.Configuration.Validation.RayAttributes;
using Raycoon.RayMigrator.Core.Extensions;
using System.ComponentModel.DataAnnotations;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1: The former TargetMigrationOrder names Simultaneously / Successively stay accepted in configuration files as
/// aliases of FileByFile / TargetByTarget (#19). Aliases resolve everywhere a configuration value is parsed or
/// validated, but never appear in the allowed-value lists or in what the engine writes.
/// </summary>
public class EnumAliasTests
{
    [Fact]
    public void TargetMigrationOrder_DeclaresTheFormerNamesAsAliases()
    {
        var aliases = typeof(TargetMigrationOrder).Aliases();

        aliases.Should().HaveCount(2);
        aliases["Simultaneously"].Should().Be(nameof(TargetMigrationOrder.FileByFile));
        aliases["successively"].Should().Be(nameof(TargetMigrationOrder.TargetByTarget), "aliases are matched case-insensitively");
    }

    [Fact]
    public void AllowedValues_ListOnlyTheCurrentNames()
    {
        typeof(TargetMigrationOrder).AllowedValues().Should().Equal("FileByFile", "TargetByTarget");
    }

    [Theory]
    [InlineData("Simultaneously", TargetMigrationOrder.FileByFile)]
    [InlineData("SUCCESSIVELY", TargetMigrationOrder.TargetByTarget)]
    [InlineData("FileByFile", TargetMigrationOrder.FileByFile)]
    [InlineData("targetbytarget", TargetMigrationOrder.TargetByTarget)]
    public void OptionsGetter_ResolvesAliasesAndCurrentNames(string configured, TargetMigrationOrder expected)
    {
        var options = new TargetGroupOptions { TargetMigrationOrder = configured };

        options.TargetMigrationOrderEnum.Should().Be(expected);
    }

    [Fact]
    public void OptionsGetter_StillRejectsUnknownValues_ListingCurrentNamesOnly()
    {
        var options = new TargetGroupOptions { TargetMigrationOrder = "Parallel" };

        var act = () => options.TargetMigrationOrderEnum;

        act.Should().Throw<Raycoon.RayMigrator.Shared.Exceptions.ConfigurationValidationException>()
           .WithMessage("*[Parallel]*Allowed values: [FileByFile, TargetByTarget]*");
    }

    [Theory]
    [InlineData("Simultaneously")]
    [InlineData("successively")]
    [InlineData("TargetByTarget")]
    public void RayEnumAttribute_AcceptsAliases(string value)
    {
        var attribute = new RayEnumAttribute(typeof(TargetMigrationOrder), isRequired: true);
        var context = new ValidationContext(new object()) { MemberName = "TargetMigrationOrder" };

        attribute.GetValidationResult(value, context).Should().Be(ValidationResult.Success);
    }

    [Fact]
    public void RayEnumAttribute_ErrorMessage_ListsCurrentNamesOnly()
    {
        var attribute = new RayEnumAttribute(typeof(TargetMigrationOrder), isRequired: true);
        var context = new ValidationContext(new object()) { MemberName = "TargetMigrationOrder" };

        var result = attribute.GetValidationResult("Parallel", context);

        result.Should().NotBe(ValidationResult.Success);
        result!.ErrorMessage.Should().Contain("[FileByFile, TargetByTarget]").And.NotContain("Simultaneously");
    }

    [Fact]
    public void EnumsWithoutAliases_HaveAnEmptyAliasMap()
    {
        typeof(MigrationErrorAction).Aliases().Should().BeEmpty();
        typeof(MigrationErrorAction).ResolveMemberName("Terminate").Should().Be("Terminate");
        typeof(MigrationErrorAction).ResolveMemberName("Undefined").Should().BeNull("the sentinel is never an accepted value");
    }
}
