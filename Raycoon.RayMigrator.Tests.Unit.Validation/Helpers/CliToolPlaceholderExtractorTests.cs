using Raycoon.RayMigrator.Validation.Helpers;

namespace Raycoon.RayMigrator.Tests.Unit.Validation.Helpers;

public class CliToolPlaceholderExtractorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyTemplate_ReturnsEmptyList(string? template)
    {
        CliToolPlaceholderExtractor.ExtractParameterKeys(template).Should().BeEmpty();
    }

    [Fact]
    public void ExtractsUserKeys_ExcludesReserved()
    {
        var keys = CliToolPlaceholderExtractor.ExtractParameterKeys("-S {Server} -d {Database} -i {FilePath}");
        keys.Should().BeEquivalentTo(new[] { "Server", "Database" });
    }

    [Fact]
    public void DeduplicatesCaseInsensitive()
    {
        var keys = CliToolPlaceholderExtractor.ExtractParameterKeys("-S {Server} -S {server}");
        keys.Should().HaveCount(1);
    }

    [Fact]
    public void ExtractAllPlaceholders_IncludesReserved()
    {
        var keys = CliToolPlaceholderExtractor.ExtractAllPlaceholders("-S {Server} -i {FilePath}");
        keys.Should().BeEquivalentTo(new[] { "Server", "FilePath" });
    }

    [Fact]
    public void ReservedKeysContainsFilePath()
    {
        CliToolPlaceholderExtractor.ReservedKeys.Should().Contain("FilePath");
    }
}
