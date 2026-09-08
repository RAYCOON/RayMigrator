using System.Collections.Concurrent;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Database.Common;
using Raycoon.RayMigrator.Shared.Exceptions;

namespace Raycoon.RayMigrator.Core;

/// <summary>
/// Class that stores all configurations and the current, dynamic (!) migration context including ID's, etc.
/// </summary>
public class MigrationContext
{
    /// <summary>
    /// Public constructor.
    /// </summary>
    /// <param name="rayMigratorOptions"></param>
    /// <param name="rayMigratorConsoleOptions"></param>
    /// <param name="rayMigratorVersion"></param>
    /// <param name="migrationState"></param>
    /// <exception cref="ConfigurationValidationException">
    /// <paramref name="rayMigratorConsoleOptions"/> still carries the "not set" sentinels <see cref="MigrationCommand.None"/>
    /// or <see cref="MigrationRunMode.Undefined"/>. A context built with them can only produce wrong stamps and wrong side effects (#6).
    /// </exception>
    public MigrationContext(RayMigratorOptions rayMigratorOptions, RayMigratorConsoleOptions rayMigratorConsoleOptions, string rayMigratorVersion, MigrationState? migrationState = null)
    {
        if (rayMigratorConsoleOptions.Command == MigrationCommand.None)
        {
            throw new ConfigurationValidationException($"Cannot build a MigrationContext: Command is [{MigrationCommand.None}]. Set the command before creating the context.");
        }

        if (rayMigratorConsoleOptions.RunMode == MigrationRunMode.Undefined)
        {
            throw new ConfigurationValidationException($"Cannot build a MigrationContext: RunMode is [{MigrationRunMode.Undefined}]. Set the run mode (Migrate for every command except migrate-up/migrate-down with --run-mode) before creating the context.");
        }

        RayMigratorOptions = rayMigratorOptions;
        RayMigratorConsoleOptions = rayMigratorConsoleOptions;
        RayMigratorVersion = rayMigratorVersion;

        ProductTargetGroupOptionsEnumerable = RayMigratorOptions.Products!.First(p => p.Alias == RayMigratorConsoleOptions.Product).TargetGroups;

        // Create or set MigrationState
        if (migrationState == null)
        {
            MigrationState = new MigrationState();
        }
        else
        {
            MigrationState = new MigrationState
            {
                MigrationEvent = migrationState.MigrationEvent,
                MigratorMetaId = migrationState.MigratorMetaId,
                ProductId = migrationState.ProductId,
                EnvironmentId = migrationState.EnvironmentId,
                MigrationRunId = migrationState.MigrationRunId,
                MigrationRecordId = migrationState.MigrationRecordId,
                ReleaseVersionFromFileNameWithPath = migrationState.ReleaseVersionFromFileNameWithPath,
                FilenameWithRelativePath = migrationState.FilenameWithRelativePath,
                FileOrderId = migrationState.FileOrderId,
                FileBlockId = migrationState.FileBlockId,
                MigrationRunResult = migrationState.MigrationRunResult,
                MigrationOperation = migrationState.MigrationOperation,
                MigrationStatus = migrationState.MigrationStatus,
                TargetGroupAlias = migrationState.TargetGroupAlias,
                HashValidationScope = migrationState.HashValidationScope,
                TargetAlias = migrationState.TargetAlias
            };
        }
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public override string ToString()
    {
        return string.Empty;
    }
    
    /// <summary>
    /// All configured RayMigrator-settings via appsettings.json.
    /// </summary>
    public RayMigratorOptions RayMigratorOptions { get; set; }

    /// <summary>
    /// Console parameter.
    /// </summary>
    public RayMigratorConsoleOptions RayMigratorConsoleOptions { get; set; }

    /// <summary>
    /// Version string of RayMigrator.
    /// </summary>
    public string RayMigratorVersion { get; set; }

    /// <summary>
    /// Shortcut for TargetGroupOptions of current product.
    /// </summary>
    public IEnumerable<TargetGroupOptions>? ProductTargetGroupOptionsEnumerable { get; init; }

    /// <summary>
    /// Current MigrationState.
    /// </summary>
    public MigrationState MigrationState { get; set; }
    
    /// <summary>
    /// Dictionary containing DAL-specific properties for parsing migration scripts, etc.
    /// </summary>
    public ConcurrentDictionary<string, DalSpecificProperties> DalSpecificPropertiesDictionary { get; set; } = new();

    /// <summary>
    /// Getting a deep copy regarding MigrationState for logging purposes.
    /// </summary>
    public MigrationContext Clone
    {
        get { return new MigrationContext(this.RayMigratorOptions, this.RayMigratorConsoleOptions, this.RayMigratorVersion, this.MigrationState); }
    }
}