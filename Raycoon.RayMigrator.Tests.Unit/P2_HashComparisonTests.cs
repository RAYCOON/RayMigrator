using AwesomeAssertions;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Services;
using Raycoon.RayMigrator.Tests.Unit.Helpers;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// #9: update-hash compared the stored config hash ("" for files without a TOML block) with the parsed
/// file's config hash (null) using != and therefore updated every TOML-less file on every run.
/// The comparison helpers treat null and "" as the same value.
/// </summary>
public class HashComparisonTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("cfg", "cfg")]
    public void ConfigHashesEqual_SameMeaning_ReturnsTrue(string? stored, string? current)
    {
        MigrationService.ConfigHashesEqual(stored, current).Should().BeTrue();
    }

    [Theory]
    [InlineData("cfg", null)]
    [InlineData(null, "cfg")]
    [InlineData("cfg", "")]
    [InlineData("cfg-a", "cfg-b")]
    [InlineData("cfg", "CFG")]
    public void ConfigHashesEqual_DifferentValues_ReturnsFalse(string? stored, string? current)
    {
        MigrationService.ConfigHashesEqual(stored, current).Should().BeFalse();
    }

    [Fact]
    public void HashesDiffer_RecordStoresEmptyConfigHashForTomlLessFile_ReturnsFalse()
    {
        var file = TestFactories.CreateMigrationFile(hash: "up", blocksHash: "blocks");
        file.FileUpConfigHash = null;
        var record = TestFactories.CreateMigrationRecord(hash: "up", blocksHash: "blocks");
        record.FileUpConfigHash = "";

        MigrationService.HashesDiffer(record, file).Should().BeFalse("\"\" in the repository and null on disk both mean 'no TOML block' (#9)");
    }

    [Theory]
    [InlineData("up-changed", "blocks", null)]
    [InlineData("up", "blocks-changed", null)]
    [InlineData("up", "blocks", "cfg-changed")]
    public void HashesDiffer_AnyOfTheThreeHashesChanged_ReturnsTrue(string fileUpHash, string blocksHash, string? configHash)
    {
        var file = TestFactories.CreateMigrationFile(hash: fileUpHash, blocksHash: blocksHash);
        file.FileUpConfigHash = configHash;
        var record = TestFactories.CreateMigrationRecord(hash: "up", blocksHash: "blocks");
        record.FileUpConfigHash = null;

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
