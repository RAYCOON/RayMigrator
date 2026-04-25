
using FluentAssertions;
using Raycoon.RayMigrator.Core.Configuration.Replacer;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P0-3: Environment Variable Replacement tests.
/// Wrong replacement leads to wrong connection strings (connecting to wrong database = data loss).
/// Tests must not run in parallel due to shared environment variables.
/// </summary>
[Collection("EnvironmentVariableTests")]
public class EnvironmentVariableReplacerTests : IDisposable
{
    private readonly List<string> _setVariables = new();

    private void SetEnv(string name, string? value)
    {
        Environment.SetEnvironmentVariable(name, value);
        _setVariables.Add(name);
    }

    public void Dispose()
    {
        foreach (var name in _setVariables)
        {
            Environment.SetEnvironmentVariable(name, null);
        }
    }

    [Fact]
    public void SimpleVariable_IsReplaced()
    {
        SetEnv("TEST_VAR_SIMPLE", "hello");

        var result = EnvironmentVariableReplacer.TryReplaceStringContainingEnvironmentVariableReferences(
            "{ENV:TEST_VAR_SIMPLE}", out var output, out var replacements);

        result.Should().BeTrue();
        output.Should().Be("hello");
        replacements.Should().HaveCount(1);
    }

    [Fact]
    public void MultipleVariables_AreReplaced()
    {
        SetEnv("TEST_VAR_A", "x");
        SetEnv("TEST_VAR_B", "y");

        var result = EnvironmentVariableReplacer.TryReplaceStringContainingEnvironmentVariableReferences(
            "{ENV:TEST_VAR_A}_{ENV:TEST_VAR_B}", out var output, out var replacements);

        result.Should().BeTrue();
        output.Should().Be("x_y");
        replacements.Should().HaveCount(2);
    }

    [Fact]
    public void NonExistentVariable_ReplacedWithNull()
    {
        // Ensure variable does not exist
        Environment.SetEnvironmentVariable("TEST_VAR_NONEXISTENT_XYZ123", null);

        var result = EnvironmentVariableReplacer.TryReplaceStringContainingEnvironmentVariableReferences(
            "{ENV:TEST_VAR_NONEXISTENT_XYZ123}", out var output, out var replacements);

        result.Should().BeTrue();
        // Known limitation: non-existent variable is replaced with null (empty)
        output.Should().BeEmpty();
    }

    [Fact]
    public void EmptyVariable_ReplacedWithEmptyString()
    {
        SetEnv("TEST_VAR_EMPTY", "");

        var result = EnvironmentVariableReplacer.TryReplaceStringContainingEnvironmentVariableReferences(
            "{ENV:TEST_VAR_EMPTY}", out var output, out var replacements);

        result.Should().BeTrue();
        output.Should().BeEmpty();
    }

    [Fact]
    public void NoPlaceholders_ReturnsFalse()
    {
        var result = EnvironmentVariableReplacer.TryReplaceStringContainingEnvironmentVariableReferences(
            "plain text", out var output, out var replacements);

        result.Should().BeFalse();
        output.Should().Be("plain text");
    }

    [Fact]
    public void NullInput_ReturnsFalse()
    {
        var result = EnvironmentVariableReplacer.TryReplaceStringContainingEnvironmentVariableReferences(
            null, out var output, out var replacements);

        result.Should().BeFalse();
        output.Should().BeNull();
    }

    [Fact]
    public void EmptyString_ReturnsFalse()
    {
        var result = EnvironmentVariableReplacer.TryReplaceStringContainingEnvironmentVariableReferences(
            "", out var output, out var replacements);

        result.Should().BeFalse();
    }

    [Fact]
    public void VariableInConnectionString_ReplacedCorrectly()
    {
        SetEnv("TEST_HOST", "localhost");

        var result = EnvironmentVariableReplacer.TryReplaceStringContainingEnvironmentVariableReferences(
            "Server={ENV:TEST_HOST}", out var output, out _);

        result.Should().BeTrue();
        output.Should().Be("Server=localhost");
    }

    [Fact]
    public void SpecialCharactersInValue_PreservedCorrectly()
    {
        SetEnv("TEST_SPECIAL", "a;b=c");

        var result = EnvironmentVariableReplacer.TryReplaceStringContainingEnvironmentVariableReferences(
            "{ENV:TEST_SPECIAL}", out var output, out _);

        result.Should().BeTrue();
        output.Should().Be("a;b=c");
    }

    [Fact]
    public void WhitespaceOnly_ReturnsFalse()
    {
        var result = EnvironmentVariableReplacer.TryReplaceStringContainingEnvironmentVariableReferences(
            "   ", out var output, out _);

        result.Should().BeFalse();
    }
}
