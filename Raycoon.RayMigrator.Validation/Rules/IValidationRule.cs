
using Raycoon.RayMigrator.Validation.Models;

namespace Raycoon.RayMigrator.Validation.Rules;

/// <summary>
/// Single validation rule contract. Implementations are <c>internal sealed</c> and
/// registered by hand in <see cref="RuleCatalog"/> (no reflection discovery).
/// </summary>
internal interface IValidationRule
{
    void Execute(ValidationInput input, ValidationReport report);
}
