using AwesomeAssertions;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Services;
using Raycoon.RayMigrator.Tests.Unit.Helpers;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// #9: update-hash compares the three stored up-hashes of a record with the file on disk. A file without a
/// TOML block has a null config hash on disk and in the repository (the insert templates store NULL), so
/// the comparison is a plain ordinal one and "" is a different value from null.
/// </summary>
public class HashComparisonTests
{
    [Fact]
    public void HashesDiffer_TomlLessFileAndNullRecordHash_ReturnsFalse()
    {
        var file = TestFactories.CreateMigrationFile(hash: "up", blocksHash: "blocks");
        file.FileUpConfigHash = null;
        var record = TestFactories.CreateMigrationRecord(hash: "up", blocksHash: "blocks");
        record.FileUpConfigHash = null;

        MigrationService.HashesDiffer(record, file).Should().BeFalse("null on disk and NULL in the repository both mean 'no TOML block' (#9)");
    }

    [Theory]
    [InlineData("up-changed", "blocks", null)]
    [InlineData("up", "blocks-changed", null)]
    [InlineData("up", "blocks", "cfg-changed")]
    [InlineData("up", "blocks", "")]
    public void HashesDiffer_AnyOfTheThreeHashesChanged_ReturnsTrue(string fileUpHash, string blocksHash, string? configHash)
    {
        var file = TestFactories.CreateMigrationFile(hash: fileUpHash, blocksHash: blocksHash);
        file.FileUpConfigHash = configHash;
        var record = TestFactories.CreateMigrationRecord(hash: "up", blocksHash: "blocks");
        record.FileUpConfigHash = null;

        MigrationService.HashesDiffer(record, file).Should().BeTrue();
    }

    [Theory]
    [InlineData("cfg", "CFG")]
    [InlineData("cfg-a", "cfg-b")]
    [InlineData("cfg", null)]
    public void HashesDiffer_ConfigHashComparedOrdinally_ReturnsTrue(string? stored, string? current)
    {
        var file = TestFactories.CreateMigrationFile(hash: "up", blocksHash: "blocks");
        file.FileUpConfigHash = current;
        var record = TestFactories.CreateMigrationRecord(hash: "up", blocksHash: "blocks");
        record.FileUpConfigHash = stored;

        MigrationService.HashesDiffer(record, file).Should().BeTrue();
    }

    [Fact]
    public void HashesDiffer_AllHashesEqual_ReturnsFalse()
    {
        var file = TestFactories.CreateMigrationFile(hash: "up", blocksHash: "blocks");
        file.FileUpConfigHash = "cfg";
        var record = TestFactories.CreateMigrationRecord(hash: "up", blocksHash: "blocks", status: MigrationStatus.Migrated);
        record.FileUpConfigHash = "cfg";

        MigrationService.HashesDiffer(record, file).Should().BeFalse();
    }
}
