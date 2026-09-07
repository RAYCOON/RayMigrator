using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Pipeline;
using Raycoon.RayMigrator.Services.Abstractions;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// Pins the exit-code and logging contract of the validate-hash CLI wrapper (#5).
/// <see cref="ValidationResult.Success"/> means "no hash issues", not "the command ran": a result with
/// Modified/Missing files has Success = false and no ErrorMessage and must be reported as a summary plus
/// one warning per issue (exit 1), not logged as a command failure. A genuine failure carries an ErrorMessage.
/// </summary>
public class ValidateHashExitCodeTests
{
    /// <summary>Minimal logger that records (level, rendered message) pairs.</summary>
    private sealed class RecordingLogger : ILogger<RayMigratorService>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Entries.Add((logLevel, formatter(state, exception)));
    }

    private static (RayMigratorService Wrapper, RecordingLogger Logger, IMigrationService Service) CreateWrapper(ValidationResult result)
    {
        var migrationService = Substitute.For<IMigrationService>();
        migrationService.ValidateHashAsync(Arg.Any<ValidateHashRequest>()).Returns(Task.FromResult(result));

        var consoleOptions = new RayMigratorConsoleOptions
        {
            Command = MigrationCommand.ValidateHash,
            Product = "P",
            Environment = "Dev",
            RunMode = MigrationRunMode.Validate,
            ShowStartupInfo = false,
            RevealSensitiveData = false
        };

        var logger = new RecordingLogger();
        return (new RayMigratorService(logger, consoleOptions, migrationService), logger, migrationService);
    }

    [Fact]
    public async Task DoWorkAsync_AllValid_ReturnsZeroAndLogsSummary()
    {
        var (wrapper, logger, _) = CreateWrapper(new ValidationResult
        {
            Success = true, ProductAlias = "P", TotalFiles = 2, ValidFiles = 2
        });

        var exitCode = await wrapper.DoWorkAsync(null!);

        exitCode.Should().Be(0);
        logger.Entries.Should().NotContain(e => e.Level == LogLevel.Error);
        logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Information && e.Message.Contains("Validate-Hash completed"));
        logger.Entries.Should().NotContain(e => e.Level == LogLevel.Warning, "there are no issues to list");
    }

    [Fact]
    public async Task DoWorkAsync_NewFilesOnly_ReturnsZeroAndListsNewFileAsWarning()
    {
        var (wrapper, logger, _) = CreateWrapper(new ValidationResult
        {
            Success = true, ProductAlias = "P", TotalFiles = 2, ValidFiles = 1,
            Issues = { new HashValidationIssue { FileName = "002_new.sql", IssueType = "New", Details = "not migrated yet" } }
        });

        var exitCode = await wrapper.DoWorkAsync(null!);

        exitCode.Should().Be(0, "files that are not migrated yet are informational only");
        logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Warning && e.Message.Contains("002_new.sql") && e.Message.Contains("New"));
    }

    [Fact]
    public async Task DoWorkAsync_ModifiedFile_ReturnsOneAndListsIssueInsteadOfFailing()
    {
        var (wrapper, logger, service) = CreateWrapper(new ValidationResult
        {
            Success = false, ErrorMessage = null, ProductAlias = "P", TotalFiles = 1, InvalidFiles = 1,
            Issues = { new HashValidationIssue { FileName = "001.sql", IssueType = "Modified", Details = "hash mismatch" } }
        });

        var exitCode = await wrapper.DoWorkAsync(null!);

        exitCode.Should().Be(1, "a modified migrated file must fail the CI gate");
        await service.Received(1).ValidateHashAsync(Arg.Any<ValidateHashRequest>());
        logger.Entries.Should().NotContain(e => e.Level == LogLevel.Error,
            "hash issues are a validation outcome, not a command failure (#5)");
        logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Information && e.Message.Contains("Validate-Hash completed") && e.Message.Contains("Invalid: 1"));
        logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Warning && e.Message.Contains("001.sql") && e.Message.Contains("Modified"));
    }

    [Fact]
    public async Task DoWorkAsync_MissingFile_ReturnsOneAndListsIssueInsteadOfFailing()
    {
        var (wrapper, logger, _) = CreateWrapper(new ValidationResult
        {
            Success = false, ErrorMessage = null, ProductAlias = "P", TotalFiles = 0, MissingFiles = 1,
            Issues = { new HashValidationIssue { FileName = "001.sql", IssueType = "Missing", Details = "deleted" } }
        });

        var exitCode = await wrapper.DoWorkAsync(null!);

        exitCode.Should().Be(1);
        logger.Entries.Should().NotContain(e => e.Level == LogLevel.Error);
        logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Warning && e.Message.Contains("001.sql") && e.Message.Contains("Missing"));
    }

    [Fact]
    public async Task DoWorkAsync_CommandFailure_ReturnsOneAndLogsError()
    {
        var (wrapper, logger, _) = CreateWrapper(new ValidationResult
        {
            Success = false, ErrorMessage = "repository unreachable", ProductAlias = "P"
        });

        var exitCode = await wrapper.DoWorkAsync(null!);

        exitCode.Should().Be(1);
        logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Error && e.Message.Contains("repository unreachable"));
        logger.Entries.Should().NotContain(e => e.Level == LogLevel.Information && e.Message.Contains("Validate-Hash completed"),
            "a failed command has no validation summary");
    }
}
