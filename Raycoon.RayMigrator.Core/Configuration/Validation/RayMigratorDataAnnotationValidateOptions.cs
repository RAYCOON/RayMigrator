using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Raycoon.RayMigrator.Core.Configuration.Options;

namespace Raycoon.RayMigrator.Core.Configuration.Validation;

/// <summary>
/// Runs the data annotations of <see cref="RayMigratorOptions"/> as the options framework runs them and names the
/// elements of a failure by alias. The framework names a list element by its position
/// (<c>RayMigratorOptions.Products[0].TargetGroups[0].Targets[0]</c>); since a run binds its own product only
/// (ADR-026), the position no longer tells which product of the files failed, the alias does
/// (<c>RayMigratorOptions.Products[Crm].TargetGroups[Backend].Targets[Main]</c>). An element without an alias keeps
/// its position. The CLI pipeline, the engine test host and <see cref="RayMigratorOptionsPreparer"/> use it in place
/// of <c>ValidateDataAnnotations()</c>.
/// </summary>
public sealed class RayMigratorDataAnnotationValidateOptions : IValidateOptions<RayMigratorOptions>
{
    private static readonly Regex ElementPath = new(
        @"RayMigratorOptions\.(?<list>Products|CliTools)\[(?<first>\d+)\](?:\.TargetGroups\[(?<group>\d+)\](?:\.Targets\[(?<target>\d+)\])?)?",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly DataAnnotationValidateOptions<RayMigratorOptions> _annotations = new(null);

    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, RayMigratorOptions options)
    {
        var result = _annotations.Validate(name, options);
        if (!result.Failed || result.Failures is null)
        {
            return result;
        }

        return ValidateOptionsResult.Fail(result.Failures.Select(failure => NameElements(failure, options)).ToList());
    }

    /// <summary>
    /// Rewrites every element position of <paramref name="failure"/> (<c>Products[0]</c>, <c>TargetGroups[0]</c>,
    /// <c>Targets[0]</c>, <c>CliTools[0]</c>) to the alias of that element of <paramref name="options"/>; an element
    /// without an alias, or a position the options do not have, keeps its number.
    /// </summary>
    public static string NameElements(string failure, RayMigratorOptions options)
    {
        ArgumentNullException.ThrowIfNull(failure);
        ArgumentNullException.ThrowIfNull(options);

        return ElementPath.Replace(failure, match => Rewrite(match, options));
    }

    private static string Rewrite(Match match, RayMigratorOptions options)
    {
        int first = Index(match.Groups["first"]);
        if (match.Groups["list"].Value == nameof(RayMigratorOptions.CliTools))
        {
            return $"RayMigratorOptions.CliTools[{Selector(At(options.CliTools, first)?.Alias, first)}]";
        }

        var product = At(options.Products, first);
        var text = new StringBuilder("RayMigratorOptions.Products[").Append(Selector(product?.Alias, first)).Append(']');
        if (match.Groups["group"].Success)
        {
            int groupIndex = Index(match.Groups["group"]);
            var group = At(product?.TargetGroups, groupIndex);
            text.Append(".TargetGroups[").Append(Selector(group?.Alias, groupIndex)).Append(']');
            if (match.Groups["target"].Success)
            {
                int targetIndex = Index(match.Groups["target"]);
                text.Append(".Targets[").Append(Selector(At(group?.Targets, targetIndex)?.Alias, targetIndex)).Append(']');
            }
        }

        return text.ToString();
    }

    private static int Index(Group group) => int.Parse(group.Value, CultureInfo.InvariantCulture);

    private static T? At<T>(IList<T>? list, int index) where T : class =>
        list is not null && index >= 0 && index < list.Count ? list[index] : null;

    private static string Selector(string? alias, int index) =>
        string.IsNullOrWhiteSpace(alias) ? index.ToString(CultureInfo.InvariantCulture) : alias;
}
