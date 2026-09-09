using AwesomeAssertions;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Core.Configuration.Validation.RayAttributes;
using Raycoon.RayMigrator.Core.Extensions;
using System.ComponentModel.DataAnnotations;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1: TargetMigrationOrder accepts only its current member names FileByFile / TargetByTarget. The former names
/// Simultaneously / Successively are rejected with the normal error message that lists the current names (#19).
/// </summary>
public class TargetMigrationOrderNamesTests
{
    [Fact]
    public void AllowedValues_ListOnlyTheCurrentNames()
    {
        typeof(TargetMigrationOrder).AllowedValues().Should().Equal("FileByFile", "TargetByTarget");
    }

    [Theory]
    [InlineData("FileByFile", TargetMigrationOrder.FileByFile)]
    [InlineData("targetbytarget", TargetMigrationOrder.TargetByTarget)]
    public void OptionsGetter_ResolvesCurrentNamesCaseInsensitively(string configured, TargetMigrationOrder expected)
    {
        new TargetGroupOptions { TargetMigrationOrder = configured }.TargetMigrationOrderEnum.Should().Be(expected);
    }

    [Theory]
    [InlineData("Simultaneously")]
    [InlineData("Successively")]
    [InlineData("Parallel")]
    public void OptionsGetter_RejectsFormerAndUnknownNames_ListingCurrentNamesOnly(string configured)
    {
        var options = new TargetGroupOptions { TargetMigrationOrder = configured };

        var act = () => options.TargetMigrationOrderEnum;

        act.Should().Throw<Raycoon.RayMigrator.Shared.Exceptions.ConfigurationValidationException>()
           .WithMessage($"*[{configured}]*Allowed values: [FileByFile, TargetByTarget]*");
    }

    [Theory]
    [InlineData("Simultaneously")]
    [InlineData("successively")]
    public void RayEnumAttribute_RejectsFormerNames(string value)
    {
        var attribute = new RayEnumAttribute(typeof(TargetMigrationOrder), isRequired: true);
        var context = new ValidationContext(new object()) { MemberName = "TargetMigrationOrder" };

        var result = attribute.GetValidationResult(value, context);

        result.Should().NotBe(ValidationResult.Success);
        result!.ErrorMessage.Should().Contain("[FileByFile, TargetByTarget]");
    }

    [Fact]
    public void ResolveMemberName_MatchesCurrentNamesOnly()
    {
        typeof(TargetMigrationOrder).ResolveMemberName("filebyfile").Should().Be("FileByFile");
        typeof(TargetMigrationOrder).ResolveMemberName("Successively").Should().BeNull();
        typeof(TargetMigrationOrder).ResolveMemberName("Undefined").Should().BeNull("the sentinel is never an accepted value");
    }
}
