
namespace Raycoon.RayMigrator.Validation.Models;

public sealed class CliToolInput
{
    public string? Alias { get; init; }
    public string? ExecutablePath { get; init; }
    public string? ArgumentTemplate { get; init; }
    public string? InputMode { get; init; }
    public IReadOnlyList<string>? SuccessExitCodes { get; init; }
    public int? CliToolTimeoutInSeconds { get; init; }
}
