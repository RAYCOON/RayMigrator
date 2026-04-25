using FluentAssertions;
using Microsoft.Extensions.Logging;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Core.Models;
using Raycoon.RayMigrator.Services;
using Raycoon.RayMigrator.Tests.Unit.Helpers;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1: Behavioral tests for HandleMigrationError — verifies that each MigrationErrorAction
/// produces the correct log output and does not throw for non-rollback actions.
/// Rollback/RollbackErrorOnly/RollbackRelease require full DI context and are tested via integration tests.
/// </summary>
public class HandleMigrationErrorBehaviorTests
{
    private static async Task InvokeHandleMigrationError(
        MigrationService service,
        ProductOptions productOptions,
        MigrationFileInfo file,
        int failedMigrationRecordId,
        List<(MigrationFileInfo File, int MigrationRecordId, string TargetAlias)> successRecords)
    {
        var method = typeof(MigrationService).GetMethod("HandleMigrationError",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        method.Should().NotBeNull("HandleMigrationError should exist");

        var task = (Task)method!.Invoke(service, new object[] { productOptions, file, failedMigrationRecordId, successRecords })!;
        await task;
    }

    [Fact]
    public async Task Terminate_ProductDefault_LogsErrorAndDoesNotThrow()
    {
        var (service, logger) = TestFactories.CreateMigrationServiceWithCapturingLogger();

        var productOptions = new ProductOptions(null) { MigrationErrorAction = "Terminate" };
        var file = new MigrationFileInfo
        {
            Filename = "10_Schema.sql",
            ReleaseVersion = "Release 1.0",
            TargetGroupAlias = "Backend",
            MigrationErrorActionOverride = null
        };

        var successRecords = new List<(MigrationFileInfo File, int MigrationRecordId, string TargetAlias)>();

        await InvokeHandleMigrationError(service, productOptions, file, 42, successRecords);

        logger.Entries.Should().Contain(e =>
            e.Message.Contains("Terminate") && e.Message.Contains("No rollback"));
    }

    [Fact]
    public async Task Terminate_ProductDefault_NoRollbackLogMessages()
    {
        var (service, logger) = TestFactories.CreateMigrationServiceWithCapturingLogger();

        var productOptions = new ProductOptions(null) { MigrationErrorAction = "Terminate" };
        var file = new MigrationFileInfo
        {
            Filename = "10_Schema.sql",
            ReleaseVersion = "Release 1.0",
            TargetGroupAlias = "Backend",
            MigrationErrorActionOverride = null
        };

        var successRecords = new List<(MigrationFileInfo File, int MigrationRecordId, string TargetAlias)>
        {
            (new MigrationFileInfo { Filename = "05_Init.sql", ReleaseVersion = "Release 1.0", TargetGroupAlias = "Backend" }, 1, "T1")
        };

        await InvokeHandleMigrationError(service, productOptions, file, 42, successRecords);

        logger.Entries.Should().NotContain(e =>
            e.Message.Contains("Rolling back"));
    }

    [Fact]
    public async Task Terminate_FileOverride_TakesPrecedenceOverProduct()
    {
        var (service, logger) = TestFactories.CreateMigrationServiceWithCapturingLogger();

        var productOptions = new ProductOptions(null) { MigrationErrorAction = "Rollback" };
        var file = new MigrationFileInfo
        {
            Filename = "10_Test.sql",
            ReleaseVersion = "Release 1.0",
            TargetGroupAlias = "Backend",
            MigrationErrorActionOverride = MigrationErrorAction.Terminate
        };

        var successRecords = new List<(MigrationFileInfo File, int MigrationRecordId, string TargetAlias)>();

        await InvokeHandleMigrationError(service, productOptions, file, 42, successRecords);

        logger.Entries.Should().Contain(e =>
            e.Message.Contains("Terminate") && e.Message.Contains("No rollback"));
        logger.Entries.Should().Contain(e =>
            e.Message.Contains("file-level MigrationErrorAction override"));
    }

    [Fact]
    public async Task Ignore_ProductDefault_LogsDebugAndDoesNotThrow()
    {
        var (service, logger) = TestFactories.CreateMigrationServiceWithCapturingLogger();

        var productOptions = new ProductOptions(null) { MigrationErrorAction = "Ignore" };
        var file = new MigrationFileInfo
        {
            Filename = "10_Test.sql",
            ReleaseVersion = "Release 1.0",
            TargetGroupAlias = "Backend",
            MigrationErrorActionOverride = null
        };

        var successRecords = new List<(MigrationFileInfo File, int MigrationRecordId, string TargetAlias)>();

        await InvokeHandleMigrationError(service, productOptions, file, 42, successRecords);

        logger.Entries.Should().Contain(e =>
            e.Message.Contains("Ignore") && e.Message.Contains("10_Test.sql"));
    }

    [Fact]
    public async Task Ignore_FileOverride_TakesPrecedenceOverProduct()
    {
        var (service, logger) = TestFactories.CreateMigrationServiceWithCapturingLogger();

        var productOptions = new ProductOptions(null) { MigrationErrorAction = "Terminate" };
        var file = new MigrationFileInfo
        {
            Filename = "20_Data.sql",
            ReleaseVersion = "Release 1.0",
            TargetGroupAlias = "Backend",
            MigrationErrorActionOverride = MigrationErrorAction.Ignore
        };

        var successRecords = new List<(MigrationFileInfo File, int MigrationRecordId, string TargetAlias)>();

        await InvokeHandleMigrationError(service, productOptions, file, 42, successRecords);

        logger.Entries.Should().Contain(e =>
            e.Message.Contains("Ignore") && e.Message.Contains("20_Data.sql"));
        logger.Entries.Should().Contain(e =>
            e.Message.Contains("file-level MigrationErrorAction override"));
    }

    [Fact]
    public async Task UnknownAction_LogsWarning()
    {
        var (service, logger) = TestFactories.CreateMigrationServiceWithCapturingLogger();

        var productOptions = new ProductOptions(null) { MigrationErrorAction = "Terminate" };
        var file = new MigrationFileInfo
        {
            Filename = "10_Test.sql",
            ReleaseVersion = "Release 1.0",
            TargetGroupAlias = "Backend",
            MigrationErrorActionOverride = (MigrationErrorAction)99
        };

        var successRecords = new List<(MigrationFileInfo File, int MigrationRecordId, string TargetAlias)>();

        await InvokeHandleMigrationError(service, productOptions, file, 42, successRecords);

        logger.Entries.Should().Contain(e =>
            e.Message.Contains("Unknown") && e.LogLevel == LogLevel.Warning);
    }
}
