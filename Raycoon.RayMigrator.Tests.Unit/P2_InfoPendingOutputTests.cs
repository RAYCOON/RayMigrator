using AwesomeAssertions;
using NSubstitute;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Pipeline;
using Raycoon.RayMigrator.Services.Abstractions;
using Raycoon.RayMigrator.Tests.Unit.Helpers;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P2: info prints the pending migrations right under their count, as a table in the order of the next migrate-up with
/// release, target group, target, file and reason, a resumed file with the last block that committed. More than
/// <see cref="RayMigratorService.PendingListLimit"/> entries end with "… and n more". Every target group line names its
/// pending count and the end of its Migrated record that finished last.
/// </summary>
public class InfoPendingOutputTests
{
    private static RayMigratorConsoleOptions Options() => new()
    {
        Command = MigrationCommand.Info,
        Product = "P",
        Environment = "Dev",
        RunMode = MigrationRunMode.Migrate,
        ShowStartupInfo = false,
        RevealSensitiveData = false
    };

    private static PendingMigrationInfo Pending(
        string release, string file, int fileOrderId, PendingMigrationReason reason = PendingMigrationReason.NotMigrated,
        int? resumeFromBlock = null, string? target = "MainDB")
        => new(release, "Backend", target, file, fileOrderId, reason, resumeFromBlock);

    /// <summary>Runs info against a service that returns <paramref name="status"/> and no runs; returns the messages logged.</summary>
    private static async Task<List<string>> RunInfoAsync(MigrationStatusInfo status)
    {
        var service = Substitute.For<IMigrationService>();
        service.GetStatusAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(status);
        service.GetHistoryAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(new MigrationHistory());
        var logger = new CapturingLogger<RayMigratorService>();
        using var cancellation = new RunCancellation();

        int exitCode = await new RayMigratorService(logger, Options(), service).DoWorkAsync(null!, cancellation);

        exitCode.Should().Be(0);
        return logger.Entries.Select(e => e.Message).ToList();
    }

    private static string[] Lines(string message) => message.Split(["\r\n", "\n"], StringSplitOptions.None);

    [Fact]
    public async Task Info_WithPendingMigrations_ListsThemUnderTheirCount()
    {
        // Arrange
        var status = new MigrationStatusInfo
        {
            ProductAlias = "P",
            CurrentRelease = "Release_1.0",
            TotalMigrationsExecuted = 2,
            PendingMigrations = 3,
            PendingFiles =
            [
                Pending("Release_1.0", "03_SeedDataA.sql", 3, PendingMigrationReason.Resumable, 1),
                Pending("Release_2.0", "01_CreateTableC.sql", 4, PendingMigrationReason.Failed),
                Pending("Release_2.0", "02_CreateTableD.sql", 5, PendingMigrationReason.RunAlways, target: null)
            ]
        };

        // Act
        var messages = await RunInfoAsync(status);

        // Assert
        int count = messages.FindIndex(m => m.Contains("Pending Migrations:  3"));
        count.Should().BePositive();
        messages[count - 1].Should().Contain("Total Executed:      2", because: "the summary ends with the pending count, so that the list follows it");
        var table = Lines(messages[count + 1]);
        table[0].Should().BeEmpty(because: "the table starts on a line of its own, like the run history");
        string header = table[1];
        header.Should().MatchRegex(@"^\s+Release\s+Target group\s+Target\s+File\s+Reason$");
        table[2].Trim().Should().MatchRegex("^[─ ]+$");
        table.Should().HaveCount(6, because: "an empty line, the header, the rule and the three entries");
        table[3].Should().MatchRegex(@"^\s+Release_1\.0\s+Backend\s+MainDB\s+03_SeedDataA\.sql\s+Resumable after block 1$");
        table[4].Should().MatchRegex(@"^\s+Release_2\.0\s+Backend\s+MainDB\s+01_CreateTableC\.sql\s+Failed$");
        table[5].Should().MatchRegex(@"^\s+Release_2\.0\s+Backend\s+—\s+02_CreateTableD\.sql\s+RunAlways$",
            because: "an entry without a target shows a dash");
        foreach (var (row, file) in new[] { (table[3], "03_SeedDataA.sql"), (table[4], "01_CreateTableC.sql"), (table[5], "02_CreateTableD.sql") })
        {
            row.IndexOf(file, StringComparison.Ordinal).Should().Be(header.IndexOf("File", StringComparison.Ordinal),
                because: "the columns are aligned");
        }
    }

    [Fact]
    public async Task Info_WithMoreThanTheLimit_CutsTheListWithTheRest()
    {
        // Arrange
        int total = RayMigratorService.PendingListLimit + 3;
        var pending = Enumerable.Range(1, total).Select(i => Pending("Release_1.0", $"{i:000}_File.sql", i)).ToList();
        var status = new MigrationStatusInfo { ProductAlias = "P", PendingMigrations = total, PendingFiles = pending };

        // Act
        var messages = await RunInfoAsync(status);

        // Assert
        messages.Should().Contain(m => m.Contains($"Pending Migrations:  {total}"), because: "the count is always complete");
        var table = Lines(messages.Single(m => m.Contains("Target group")));
        table.Should().HaveCount(1 + 2 + RayMigratorService.PendingListLimit + 1, because: "an empty line, the header, the rule, the shown entries and the rest");
        table[^2].Should().Contain($"{RayMigratorService.PendingListLimit:000}_File.sql");
        table.Should().NotContain(line => line.Contains($"{RayMigratorService.PendingListLimit + 1:000}_File.sql"));
        table[^1].Should().Be("    … and 3 more");
    }

    [Fact]
    public void FormatPendingMigrations_AtTheLimit_ListsEveryEntry()
    {
        // Arrange
        var pending = Enumerable.Range(1, 4).Select(i => Pending("Release_1.0", $"0{i}_File.sql", i)).ToList();

        // Act
        string table = RayMigratorService.FormatPendingMigrations(pending, 4);

        // Assert
        Lines(table).Should().HaveCount(2 + 4, because: "the header, the rule and the four entries");
        table.Should().NotContain("more");
    }

    [Fact]
    public async Task Info_WithoutPendingMigrations_PrintsNoList()
    {
        // Arrange
        var status = new MigrationStatusInfo { ProductAlias = "P", PendingMigrations = 0 };

        // Act
        var messages = await RunInfoAsync(status);

        // Assert
        messages.Should().Contain(m => m.Contains("Pending Migrations:  0"));
        messages.Should().NotContain(m => m.Contains("Target group"), because: "an empty list prints no table");
    }

    [Fact]
    public async Task Info_TargetGroups_NameTheirPendingCountAndLastMigration()
    {
        // Arrange
        var status = new MigrationStatusInfo
        {
            ProductAlias = "P",
            TargetGroups = new Dictionary<string, TargetGroupStatus>
            {
                ["Backend"] = new()
                {
                    Alias = "Backend", DatabaseType = "Sqlite", CurrentRelease = "Release_1.0", ExecutedMigrations = 2,
                    PendingMigrations = 3, LastMigrationDate = new DateTime(2026, 10, 16, 8, 0, 5), Targets = ["MainDB"]
                },
                ["Frontend"] = new()
                {
                    Alias = "Frontend", DatabaseType = "Sqlite", CurrentRelease = "None", PendingMigrations = 8, Targets = ["FrontendDB"]
                }
            }
        };

        // Act
        var messages = await RunInfoAsync(status);

        // Assert
        messages.Should().Contain("  [Backend] Type=Sqlite, Release=Release_1.0, Executed=2, Pending=3, LastMigration=2026-10-16 08:00:05, Targets=[MainDB]");
        messages.Should().Contain("  [Frontend] Type=Sqlite, Release=None, Executed=0, Pending=8, LastMigration=—, Targets=[FrontendDB]",
            because: "a target group without a migrated record shows a dash");
    }

    [Theory]
    [InlineData(PendingMigrationReason.NotMigrated, null, "NotMigrated")]
    [InlineData(PendingMigrationReason.Failed, null, "Failed")]
    [InlineData(PendingMigrationReason.RunAlways, null, "RunAlways")]
    [InlineData(PendingMigrationReason.HashMismatch, null, "HashMismatch")]
    [InlineData(PendingMigrationReason.Resumable, 2, "Resumable after block 2")]
    public void DescribeReason_NamesTheReasonAndTheLastCommittedBlock(PendingMigrationReason reason, int? resumeFromBlock, string expected)
    {
        // Act
        string text = RayMigratorService.DescribeReason(Pending("Release_1.0", "01_A.sql", 1, reason, resumeFromBlock));

        // Assert
        text.Should().Be(expected);
    }

    [Fact]
    public void DescribeReason_RollbackIncomplete_NamesTheRollbackBlocksAndMigrateDown()
    {
        // Arrange
        var pending = new PendingMigrationInfo("Release_2.0", "Backend", "MainDB", "03_SeedDataB.sql", 6, PendingMigrationReason.RollbackIncomplete, null, 1, 3);

        // Act
        string text = RayMigratorService.DescribeReason(pending);

        // Assert
        text.Should().Be("RollbackIncomplete after 1 of 3 rollback blocks, run migrate-down",
            because: "migrate-up refuses the file until migrate-down continues its rollback with block 2");
    }
}
