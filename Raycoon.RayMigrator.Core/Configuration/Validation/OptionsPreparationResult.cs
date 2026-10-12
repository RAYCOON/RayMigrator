using Raycoon.RayMigrator.Core.Configuration.Replacer;

namespace Raycoon.RayMigrator.Core.Configuration.Validation;

/// <summary>
/// What <see cref="RayMigratorOptionsPreparer.Prepare"/> did to an options object.
/// </summary>
/// <param name="ReplacedEnvironmentVariables">
/// Every <c>{ENV:VARIABLE}</c> placeholder that was replaced, with the configuration path it was found at. The
/// values are secrets as far as the engine is concerned; the preparer has registered them with
/// <see cref="SensitiveDataMasker"/>.
/// </param>
public sealed record OptionsPreparationResult(IReadOnlyList<EnvironmentVariableWithMetadata> ReplacedEnvironmentVariables);
