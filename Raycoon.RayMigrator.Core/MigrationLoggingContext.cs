
namespace Raycoon.RayMigrator.Core;

/// <summary>
/// Static ambient context for carrying MigrationContext through the async call chain.
/// Used by the Serilog MigrationContextEnricher to add migration-specific properties
/// to every log event without requiring explicit parameter passing.
/// </summary>
public static class MigrationLoggingContext
{
    private static readonly AsyncLocal<MigrationContext?> Current_ = new();

    /// <summary>
    /// Gets or sets the current MigrationContext for the async execution flow.
    /// Set this once when the MigrationContext becomes available.
    /// The enricher reads it automatically for every log event.
    /// </summary>
    public static MigrationContext? Current
    {
        get => Current_.Value;
        set => Current_.Value = value;
    }
}
