namespace Raycoon.RayMigrator.ConfigWizard.Core.Models;

/// <summary>
/// Flags that indicate which validation capabilities are available in the current host.
/// Structural rules (flag value 0) are always available and WASM-safe.
/// </summary>
[Flags]
public enum ValidationCapability
{
    /// <summary>Pure in-memory / cross-field rules. Always available, including in WASM.</summary>
    Structural = 0,

    /// <summary>Rules that need filesystem access (e.g. path existence checks).</summary>
    Filesystem = 1,

    /// <summary>Rules that need ADO.NET connection string parsing.</summary>
    AdoNetParsing = 2
}
