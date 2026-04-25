using FluentAssertions;
using Raycoon.RayMigrator.Core.Configuration.Options;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1: Tests for ExitCodeMatcher — exit code whitelist with C# range notation.
/// Covers TryParse (valid/invalid expressions), IsMatch edge cases, Default, and ToString.
/// </summary>
public class ExitCodeMatcherTests
{
    // ── TryParse — valid expressions ─────────────────────────────────────────

    [Fact]
    public void TryParse_NullInput_ReturnsDefault()
    {
        var result = ExitCodeMatcher.TryParse(null, out var matcher, out var error);

        result.Should().BeTrue();
        error.Should().BeNull();
        matcher.IsMatch(0).Should().BeTrue();
        matcher.IsMatch(1).Should().BeFalse();
    }

    [Fact]
    public void TryParse_EmptyArray_ReturnsDefault()
    {
        var result = ExitCodeMatcher.TryParse([], out var matcher, out var error);

        result.Should().BeTrue();
        error.Should().BeNull();
        matcher.IsMatch(0).Should().BeTrue();
        matcher.IsMatch(1).Should().BeFalse();
    }

    [Fact]
    public void TryParse_SingleZero_MatchesZeroOnly()
    {
        var result = ExitCodeMatcher.TryParse(["0"], out var matcher, out var error);

        result.Should().BeTrue();
        error.Should().BeNull();
        matcher.IsMatch(0).Should().BeTrue();
        matcher.IsMatch(1).Should().BeFalse();
    }

    [Fact]
    public void TryParse_SinglePositiveValue_MatchesThatValueOnly()
    {
        var result = ExitCodeMatcher.TryParse(["42"], out var matcher, out var error);

        result.Should().BeTrue();
        error.Should().BeNull();
        matcher.IsMatch(42).Should().BeTrue();
        matcher.IsMatch(0).Should().BeFalse();
    }

    [Fact]
    public void TryParse_SingleNegativeValue_MatchesThatValueOnly()
    {
        var result = ExitCodeMatcher.TryParse(["-1"], out var matcher, out var error);

        result.Should().BeTrue();
        error.Should().BeNull();
        matcher.IsMatch(-1).Should().BeTrue();
        matcher.IsMatch(0).Should().BeFalse();
    }

    [Fact]
    public void TryParse_ClosedRange_MatchesInclusiveBounds()
    {
        var result = ExitCodeMatcher.TryParse(["1..5"], out var matcher, out var error);

        result.Should().BeTrue();
        error.Should().BeNull();
        matcher.IsMatch(1).Should().BeTrue();
        matcher.IsMatch(3).Should().BeTrue();
        matcher.IsMatch(5).Should().BeTrue();
        matcher.IsMatch(0).Should().BeFalse();
        matcher.IsMatch(6).Should().BeFalse();
    }

    [Fact]
    public void TryParse_SingleElementRange_MatchesThatValueOnly()
    {
        var result = ExitCodeMatcher.TryParse(["3..3"], out var matcher, out var error);

        result.Should().BeTrue();
        error.Should().BeNull();
        matcher.IsMatch(3).Should().BeTrue();
        matcher.IsMatch(2).Should().BeFalse();
        matcher.IsMatch(4).Should().BeFalse();
    }

    [Fact]
    public void TryParse_OpenEndedUp_MatchesFromMinToMaxValue()
    {
        var result = ExitCodeMatcher.TryParse(["1.."], out var matcher, out var error);

        result.Should().BeTrue();
        error.Should().BeNull();
        matcher.IsMatch(1).Should().BeTrue();
        matcher.IsMatch(100).Should().BeTrue();
        matcher.IsMatch(int.MaxValue).Should().BeTrue();
        matcher.IsMatch(0).Should().BeFalse();
        matcher.IsMatch(-1).Should().BeFalse();
    }

    [Fact]
    public void TryParse_OpenEndedDown_MatchesFromMinValueToMax()
    {
        var result = ExitCodeMatcher.TryParse(["..-1"], out var matcher, out var error);

        result.Should().BeTrue();
        error.Should().BeNull();
        matcher.IsMatch(-1).Should().BeTrue();
        matcher.IsMatch(-100).Should().BeTrue();
        matcher.IsMatch(int.MinValue).Should().BeTrue();
        matcher.IsMatch(0).Should().BeFalse();
        matcher.IsMatch(1).Should().BeFalse();
    }

    [Fact]
    public void TryParse_MultipleExpressions_MatchesAny()
    {
        var result = ExitCodeMatcher.TryParse(["0", "10..20", "100.."], out var matcher, out var error);

        result.Should().BeTrue();
        error.Should().BeNull();
        matcher.IsMatch(0).Should().BeTrue();
        matcher.IsMatch(15).Should().BeTrue();
        matcher.IsMatch(200).Should().BeTrue();
        matcher.IsMatch(5).Should().BeFalse();
        matcher.IsMatch(50).Should().BeFalse();
    }

    [Fact]
    public void TryParse_WhitespacePaddedExpression_TrimsAndMatches()
    {
        var result = ExitCodeMatcher.TryParse([" 0 "], out var matcher, out var error);

        result.Should().BeTrue();
        error.Should().BeNull();
        matcher.IsMatch(0).Should().BeTrue();
        matcher.IsMatch(1).Should().BeFalse();
    }

    [Fact]
    public void TryParse_NegativeClosedRange_MatchesInclusiveBounds()
    {
        var result = ExitCodeMatcher.TryParse(["-10..-1"], out var matcher, out var error);

        result.Should().BeTrue();
        error.Should().BeNull();
        matcher.IsMatch(-5).Should().BeTrue();
        matcher.IsMatch(-1).Should().BeTrue();
        matcher.IsMatch(-10).Should().BeTrue();
        matcher.IsMatch(0).Should().BeFalse();
        matcher.IsMatch(-11).Should().BeFalse();
    }

    // ── TryParse — invalid expressions ───────────────────────────────────────

    [Fact]
    public void TryParse_EmptyString_ReturnsFalseWithError()
    {
        var result = ExitCodeMatcher.TryParse([""], out var matcher, out var error);

        result.Should().BeFalse();
        error.Should().NotBeNullOrEmpty();
        matcher.Should().BeSameAs(ExitCodeMatcher.Default);
    }

    [Fact]
    public void TryParse_WhitespaceOnlyString_ReturnsFalseWithError()
    {
        var result = ExitCodeMatcher.TryParse([" "], out var matcher, out var error);

        result.Should().BeFalse();
        error.Should().NotBeNullOrEmpty();
        matcher.Should().BeSameAs(ExitCodeMatcher.Default);
    }

    [Fact]
    public void TryParse_NonIntegerSingleValue_ReturnsFalseWithInvalidIntegerError()
    {
        var result = ExitCodeMatcher.TryParse(["abc"], out var matcher, out var error);

        result.Should().BeFalse();
        error.Should().Contain("abc");
        error.Should().Contain("not a valid integer");
        matcher.Should().BeSameAs(ExitCodeMatcher.Default);
    }

    [Fact]
    public void TryParse_InvalidLeftSideOfRange_ReturnsFalseWithLeftSideError()
    {
        var result = ExitCodeMatcher.TryParse(["a..5"], out var matcher, out var error);

        result.Should().BeFalse();
        error.Should().Contain("a");
        error.Should().Contain("left side");
        matcher.Should().BeSameAs(ExitCodeMatcher.Default);
    }

    [Fact]
    public void TryParse_InvalidRightSideOfRange_ReturnsFalseWithRightSideError()
    {
        var result = ExitCodeMatcher.TryParse(["1..b"], out var matcher, out var error);

        result.Should().BeFalse();
        error.Should().Contain("b");
        error.Should().Contain("right side");
        matcher.Should().BeSameAs(ExitCodeMatcher.Default);
    }

    [Fact]
    public void TryParse_UnboundedRange_ReturnsFalseWithUnboundedError()
    {
        var result = ExitCodeMatcher.TryParse([".."], out var matcher, out var error);

        result.Should().BeFalse();
        error.Should().Contain("unbounded");
        matcher.Should().BeSameAs(ExitCodeMatcher.Default);
    }

    [Fact]
    public void TryParse_ReversedRange_ReturnsFalseWithReversedError()
    {
        var result = ExitCodeMatcher.TryParse(["5..1"], out var matcher, out var error);

        result.Should().BeFalse();
        error.Should().Contain("5..1");
        error.Should().Contain("Reversed");
        matcher.Should().BeSameAs(ExitCodeMatcher.Default);
    }

    [Fact]
    public void TryParse_FloatValue_ReturnsFalseWithInvalidIntegerError()
    {
        // "1.5" has no ".." so is treated as a single value — not a valid integer
        var result = ExitCodeMatcher.TryParse(["1.5"], out var matcher, out var error);

        result.Should().BeFalse();
        error.Should().Contain("1.5");
        error.Should().Contain("not a valid integer");
        matcher.Should().BeSameAs(ExitCodeMatcher.Default);
    }

    [Fact]
    public void TryParse_ErrorMessageContainsIndex()
    {
        // Second entry is invalid — error message should reference index 1
        var result = ExitCodeMatcher.TryParse(["0", "abc"], out _, out var error);

        result.Should().BeFalse();
        error.Should().Contain("[1]");
    }

    // ── IsMatch edge cases ────────────────────────────────────────────────────

    [Fact]
    public void IsMatch_DuplicateEntries_MatchesWithoutError()
    {
        var result = ExitCodeMatcher.TryParse(["0", "0"], out var matcher, out var error);

        result.Should().BeTrue();
        error.Should().BeNull();
        matcher.IsMatch(0).Should().BeTrue();
        matcher.IsMatch(1).Should().BeFalse();
    }

    [Fact]
    public void IsMatch_OverlappingRanges_MatchesCorrectly()
    {
        // "0..5" and "3..10" overlap at 3-5
        var result = ExitCodeMatcher.TryParse(["0..5", "3..10"], out var matcher, out var error);

        result.Should().BeTrue();
        error.Should().BeNull();
        matcher.IsMatch(0).Should().BeTrue();
        matcher.IsMatch(4).Should().BeTrue();
        matcher.IsMatch(8).Should().BeTrue();
        matcher.IsMatch(10).Should().BeTrue();
        matcher.IsMatch(11).Should().BeFalse();
        matcher.IsMatch(-1).Should().BeFalse();
    }

    // ── Default ───────────────────────────────────────────────────────────────

    [Fact]
    public void Default_MatchesZero()
    {
        ExitCodeMatcher.Default.IsMatch(0).Should().BeTrue();
    }

    [Fact]
    public void Default_DoesNotMatchOne()
    {
        ExitCodeMatcher.Default.IsMatch(1).Should().BeFalse();
    }

    [Fact]
    public void Default_DoesNotMatchNegativeOne()
    {
        ExitCodeMatcher.Default.IsMatch(-1).Should().BeFalse();
    }

    [Fact]
    public void Default_NullInput_ReturnsSameInstance()
    {
        ExitCodeMatcher.TryParse(null, out var matcher, out _);

        matcher.Should().BeSameAs(ExitCodeMatcher.Default);
    }

    [Fact]
    public void Default_EmptyArray_ReturnsSameInstance()
    {
        ExitCodeMatcher.TryParse([], out var matcher, out _);

        matcher.Should().BeSameAs(ExitCodeMatcher.Default);
    }

    // ── ToString ──────────────────────────────────────────────────────────────

    [Fact]
    public void ToString_SingleValue_ReturnsExpectedFormat()
    {
        ExitCodeMatcher.TryParse(["0"], out var matcher, out _);

        matcher.ToString().Should().Be("[0]");
    }

    [Fact]
    public void ToString_MultipleExpressions_ReturnsExpectedFormat()
    {
        ExitCodeMatcher.TryParse(["0", "1..5", "10.."], out var matcher, out _);

        matcher.ToString().Should().Be("[0, 1..5, 10..]");
    }

    [Fact]
    public void ToString_Default_ReturnsZeroFormat()
    {
        ExitCodeMatcher.Default.ToString().Should().Be("[0]");
    }

    [Fact]
    public void ToString_OpenEndedDown_ReturnsExpectedFormat()
    {
        ExitCodeMatcher.TryParse(["..-1"], out var matcher, out _);

        matcher.ToString().Should().Be("[..-1]");
    }

    [Fact]
    public void ToString_NegativeRange_ReturnsExpectedFormat()
    {
        ExitCodeMatcher.TryParse(["-10..-1"], out var matcher, out _);

        matcher.ToString().Should().Be("[-10..-1]");
    }
}
