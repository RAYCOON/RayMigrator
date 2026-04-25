
using Raycoon.RayMigrator.Validation.Helpers;

namespace Raycoon.RayMigrator.Tests.Unit.Validation.Helpers;

public class ExitCodeExpressionValidatorTests
{
    [Theory]
    [InlineData("0")]
    [InlineData("42")]
    [InlineData("-1")]
    [InlineData("1..5")]
    [InlineData("10..")]
    [InlineData("..-1")]
    [InlineData("  7  ")]
    public void ValidExpressions_ReturnTrue(string expr)
    {
        ExitCodeExpressionValidator.IsValid(expr, out _).Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("..")]
    [InlineData("5..1")]
    [InlineData("1..abc")]
    [InlineData("abc..5")]
    public void InvalidExpressions_ReturnFalse(string expr)
    {
        ExitCodeExpressionValidator.IsValid(expr, out var err).Should().BeFalse();
        err.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void ValidateAll_FirstError_IsReturned()
    {
        var err = ExitCodeExpressionValidator.ValidateAll(new[] { "0", "abc", "1..5" });
        err.Should().NotBeNull();
    }

    [Fact]
    public void ValidateAll_AllValid_ReturnsNull()
    {
        var err = ExitCodeExpressionValidator.ValidateAll(new[] { "0", "1..5", "10.." });
        err.Should().BeNull();
    }

    [Fact]
    public void ValidateAll_NullInput_ReturnsNull()
    {
        ExitCodeExpressionValidator.ValidateAll(null).Should().BeNull();
    }
}
