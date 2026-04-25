using FluentAssertions;
using Raycoon.RayMigrator.Core.Extensions;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1-1: SHA256 Hash Calculation tests.
/// Hash errors lead to missed file changes or unnecessary re-execution.
/// </summary>
public class Sha256Tests
{
    [Fact]
    public void KnownString_ProducesKnownHash()
    {
        // SHA256 of "hello" is well-known
        var result = "hello".GenerateSha256();
        result.Should().Be("2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824");
    }

    [Fact]
    public void IdenticalContent_ProducesSameHash()
    {
        var hash1 = "test content".GenerateSha256();
        var hash2 = "test content".GenerateSha256();

        hash1.Should().Be(hash2);
    }

    [Fact]
    public void DifferentContent_ProducesDifferentHash()
    {
        var hash1 = "content A".GenerateSha256();
        var hash2 = "content B".GenerateSha256();

        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void EmptyString_ProducesValidHash()
    {
        // SHA256 of "" is well-known
        var result = "".GenerateSha256();
        result.Should().Be("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855");
    }

    [Fact]
    public void UnicodeContent_ProducesConsistentHash()
    {
        var hash1 = "Ünïcödé テスト".GenerateSha256();
        var hash2 = "Ünïcödé テスト".GenerateSha256();

        hash1.Should().Be(hash2);
        hash1.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void LongString_DoesNotFail()
    {
        var longString = new string('x', 100_000);
        var result = longString.GenerateSha256();

        result.Should().NotBeNullOrEmpty();
        result.Should().HaveLength(64); // SHA256 = 64 hex chars
    }
}
