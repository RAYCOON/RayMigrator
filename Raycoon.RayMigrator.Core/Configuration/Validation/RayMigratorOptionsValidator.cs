using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Core.Configuration.Sources;
using Raycoon.RayMigrator.Validation;
using Raycoon.RayMigrator.Validation.Models;
using Serilog;

namespace Raycoon.RayMigrator.Core.Configuration.Validation;

/// <summary>
/// Custom <see cref="IValidateOptions{TOptions}"/> implementation for <see cref="RayMigratorOptions"/>.
/// Delegates all structural / cross-field rules to <see cref="RuleCatalog"/> via
/// <see cref="OptionsValidationInputAdapter"/>. Warnings are emitted through the static
/// <see cref="Log"/> channel; errors fail validation.
/// </summary>
public class RayMigratorOptionsValidator : IValidateOptions<RayMigratorOptions>
{
    private readonly IConfigurationSection? _sectionAsWritten;

    /// <summary>Validates the bound values only; connection strings are checked as bound.</summary>
    public RayMigratorOptionsValidator() : this(sectionAsWritten: null)
    {
    }

    /// <summary>
    /// Creates a validator that checks connection strings as they are written in the configuration.
    /// </summary>
    /// <param name="sectionAsWritten">
    /// The RayMigrator section before <c>{ENV:...}</c> placeholders were resolved
    /// (<see cref="OptionsSourceResult.RayMigratorConfigSectionAsWritten"/>). RULE_7_3 inspects the connection
    /// strings as written there, so a credential supplied through a placeholder is not reported as hardcoded
    /// (#30). Null checks the bound values as they are.
    /// </param>
    public RayMigratorOptionsValidator(IConfigurationSection? sectionAsWritten)
    {
        _sectionAsWritten = sectionAsWritten;
    }

    public ValidateOptionsResult Validate(string? name, RayMigratorOptions options)
    {
        if (options is null)
        {
            return ValidateOptionsResult.Fail("RayMigratorOptions instance is null.");
        }

        var input = OptionsValidationInputAdapter.ToInput(options, _sectionAsWritten);
        var report = RuleCatalog.RunAll(input);

        foreach (var warning in report.Warnings)
        {
            Log.Warning(
                "Config warning [{RuleCode}] at {Path}: {Message}",
                warning.Code,
                warning.Path,
                warning.Message);
        }

        if (report.IsValid)
        {
            return ValidateOptionsResult.Success;
        }

        var errorLines = report.Errors
            .Select(e => $"[{e.Code}] {e.Path}: {e.Message}");

        var failureMessage = string.Join(Environment.NewLine, errorLines);

        Log.Error(
            "Configuration validation failed with {ErrorCount} error(s). See details below:{NewLine}{Failures}",
            report.Errors.Count(),
            Environment.NewLine,
            failureMessage);

        return ValidateOptionsResult.Fail(failureMessage);
    }

    /// <summary>
    /// The full rule-catalog report (warnings and errors with code, severity and path) of prepared options,
    /// without mapping to <see cref="ValidateOptionsResult"/>; for hosts that show findings instead of failing.
    /// <paramref name="sectionAsWritten"/> has the meaning of the constructor parameter.
    /// </summary>
    public static ValidationReport GetReport(RayMigratorOptions options, IConfigurationSection? sectionAsWritten = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        var input = OptionsValidationInputAdapter.ToInput(options, sectionAsWritten);
        return RuleCatalog.RunAll(input);
    }
}
