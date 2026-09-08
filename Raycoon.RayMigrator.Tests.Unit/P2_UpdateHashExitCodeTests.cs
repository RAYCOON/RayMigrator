using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Pipeline;
using Raycoon.RayMigrator.Services.Abstractions;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// Pins the exit-code and logging contract of the update-hash CLI wrapper (#9): success prints the file
/// and record counts, a failed command logs the error and returns 1.
/// </summary>
public class UpdateHashExitCodeTests
{
    private sealed class RecordingLogger : ILogger<RayMigratorService>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = new();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Entries.Add((logLevel, formatter(state, exception)));
    }

    private static (RayMigratorService Wrapper, RecordingLogger Logger) CreateWrapper(HashUpdateResult result)
    {
        var migrationService = Substitute.For<IMigrationService>();
        migrationService.UpdateHashAsync(Arg.Any<UpdateHashRequest>()).Returns(Task.FromResult(result));

        var consoleOptions = new RayMigratorConsoleOptions
        {
            Command = MigrationCommand.UpdateHash,
            Product = "P",
            Environment = "Dev",
            RunMode = MigrationRunMode.Migrate,
            ShowStartupInfo = false,
            RevealSensitiveData = false
        };

        var logger = new RecordingLogger();
        return (new RayMigratorService(logger, consoleOptions, migrationService), logger);
    }

    [Fact]
    public async Task DoWorkAsync_UpdateHashSucceeded_ReturnsZeroAndLogsFilesAndRecords()
    {
        var (wrapper, logger) = CreateWrapper(new HashUpdateResult
        {
            Success = true, ProductAlias = "P", UpdatedFiles = 1, UpdatedRecords = 2, NewFiles = 3, RemovedFiles = 0
        });

        var exitCode = await wrapper.DoWorkAsync(null!);

        exitCode.Should().Be(0);
        logger.Entries.Should().NotContain(e => e.Level == LogLevel.Error);
        logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Information
            && e.Message.Contains("Updated: 1 file(s) / 2 record(s)") && e.Message.Contains("New: 3") && e.Message.Contains("Removed: 0"),
            "the summary must report files and records separately (#9)");
    }

    [Fact]
    public async Task DoWorkAsync_UpdateHashFailed_ReturnsOneAndLogsError()
    {
        var (wrapper, logger) = CreateWrapper(new HashUpdateResult
        {
            Success = false, ErrorMessage = "repository unreachable", ProductAlias = "P"
        });

        var exitCode = await wrapper.DoWorkAsync(null!);

        exitCode.Should().Be(1);
        logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Error && e.Message.Contains("repository unreachable"));
        logger.Entries.Should().NotContain(e => e.Level == LogLevel.Information && e.Message.Contains("Update-Hash completed"));
    }
}
