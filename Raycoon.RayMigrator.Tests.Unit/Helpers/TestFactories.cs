
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using Raycoon.RayMigrator.Core;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Core.Models;
using Raycoon.RayMigrator.Database.Common;
using Raycoon.RayMigrator.Services;

namespace Raycoon.RayMigrator.Tests.Unit.Helpers;

/// <summary>
/// Shared factory methods for unit tests.
/// Eliminates duplication across FilterAlreadyMigrated, OutOfOrder, and MigSettings test files.
/// </summary>
internal static class TestFactories
{
    /// <summary>
    /// Creates a MigrationFileInfo with sensible defaults for testing.
    /// Used by FilterAlreadyMigratedFiles and OutOfOrderDetection tests.
    /// </summary>
    internal static MigrationFileInfo CreateMigrationFile(
        string filename = "10_Create.sql",
        string release = "Release 1.0",
        string targetGroup = "Backend",
        string hash = "abc123",
        bool runAlways = false,
        string? blocksHash = null,
        List<string>? sqlBlocks = null)
    {
        return new MigrationFileInfo
        {
            Filename = filename,
            ReleaseVersion = release,
            TargetGroupAlias = targetGroup,
            FileUpHash = hash,
            FileUpBlocksHash = blocksHash ?? hash,
            RunAlways = runAlways,
            SqlBlocks = sqlBlocks ?? new List<string>()
        };
    }

    /// <summary>
    /// Creates a MigrationRecord with sensible defaults for testing.
    /// Used by FilterAlreadyMigratedFiles and OutOfOrderDetection tests.
    /// </summary>
    internal static MigrationRecord CreateMigrationRecord(
        string filename = "10_Create.sql",
        string release = "Release 1.0",
        string targetGroup = "Backend",
        string hash = "abc123",
        MigrationStatus status = MigrationStatus.Migrated,
        int id = 0,
        string targetAlias = "MainDB",
        string? blocksHash = null,
        int fileUpBlocksMigrated = 0,
        int fileUpBlocksTotal = 0,
        string? fileDownHash = null,
        int? fileDownBlocksMigrated = null,
        int? fileDownBlocksTotal = null)
    {
        return new MigrationRecord
        {
            Id = id,
            Filename = filename,
            ReleaseVersion = release,
            TargetGroupAlias = targetGroup,
            TargetAlias = targetAlias,
            FileUpHash = hash,
            FileUpBlocksHash = blocksHash ?? hash,
            MigrationStatusId = status,
            FileUpBlocksMigrated = fileUpBlocksMigrated,
            FileUpBlocksTotal = fileUpBlocksTotal,
            FileDownHash = fileDownHash,
            FileDownBlocksMigrated = fileDownBlocksMigrated,
            FileDownBlocksTotal = fileDownBlocksTotal
        };
    }

    /// <summary>
    /// Creates an uninitialized MigrationService instance with NullLogger injected.
    /// Used by tests that need to invoke internal instance methods via reflection
    /// without requiring the full DI container setup.
    /// </summary>
    internal static MigrationService CreateUninitializedMigrationService()
    {
        var service = (MigrationService)RuntimeHelpers.GetUninitializedObject(typeof(MigrationService));

        var loggerField = typeof(MigrationService).GetField("_logger",
            BindingFlags.NonPublic | BindingFlags.Instance);
        loggerField?.SetValue(service,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<MigrationService>.Instance);

        InjectCtxAccessor(service);

        return service;
    }

    /// <summary>
    /// Creates an uninitialized MigrationService instance with a CapturingLogger injected.
    /// Returns both the service and the logger so tests can assert on log entries.
    /// </summary>
    internal static (MigrationService Service, CapturingLogger<MigrationService> Logger)
        CreateMigrationServiceWithCapturingLogger()
    {
        var service = (MigrationService)RuntimeHelpers.GetUninitializedObject(typeof(MigrationService));
        var logger = new CapturingLogger<MigrationService>();

        var loggerField = typeof(MigrationService).GetField("_logger",
            BindingFlags.NonPublic | BindingFlags.Instance);
        loggerField?.SetValue(service, logger);

        InjectCtxAccessor(service);

        return (service, logger);
    }

    /// <summary>
    /// Injects a minimal IMigrationContextAccessor with a pre-populated DalSpecificPropertiesDictionary.
    /// Required because LogMigrationSafetyWarnings accesses _ctxAccessor.Current.DalSpecificPropertiesDictionary.
    /// </summary>
    private static void InjectCtxAccessor(MigrationService service)
    {
        var accessor = new SingletonMigrationContextAccessor();
        var ctx = (MigrationContext)RuntimeHelpers.GetUninitializedObject(typeof(MigrationContext));

        // GetUninitializedObject skips field initializers, so DalSpecificPropertiesDictionary is null.
        // Set it explicitly.
        ctx.DalSpecificPropertiesDictionary = new ConcurrentDictionary<string, DalSpecificProperties>();
        ctx.DalSpecificPropertiesDictionary.TryAdd("SqlServer", new DalSpecificProperties { SupportsTransactionalDdl = true });
        ctx.DalSpecificPropertiesDictionary.TryAdd("PostgreSQL", new DalSpecificProperties { SupportsTransactionalDdl = true });
        ctx.DalSpecificPropertiesDictionary.TryAdd("MariaDb", new DalSpecificProperties { SupportsTransactionalDdl = false });
        ctx.DalSpecificPropertiesDictionary.TryAdd("MySql", new DalSpecificProperties { SupportsTransactionalDdl = false });
        ctx.DalSpecificPropertiesDictionary.TryAdd("Sqlite", new DalSpecificProperties { SupportsTransactionalDdl = true });

        accessor.Current = ctx;

        var ctxAccessorField = typeof(MigrationService).GetField("_ctxAccessor",
            BindingFlags.NonPublic | BindingFlags.Instance);
        ctxAccessorField?.SetValue(service, accessor);
    }
}
