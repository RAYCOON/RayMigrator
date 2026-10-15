using System.Collections;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Shared.Constants;
using Raycoon.RayMigrator.Core.Configuration.Validation;

namespace Raycoon.RayMigrator.Core.Configuration.Replacer;

public static class EnvironmentVariableReplacer
{
    private static readonly Regex EnvVariablePattern = 
        new Regex(@"\" + ConfigurationConstants.EnvironmentVariablePrefix + @"(\w+)\" + ConfigurationConstants.EnvironmentVariablePostfix, RegexOptions.Compiled);
    
    /// <summary>
    /// Recursively replace all section property-values with their corresponding environment variable values.
    /// </summary>
    /// <param name="section">The IConfigurationSection to check for environment-variable replacements.</param>
    public static List<EnvironmentVariableWithMetadata> ReplaceWithEnvironmentVariables(IConfigurationSection section)
    {
        var replacedEnvironmentVariables = new List<EnvironmentVariableWithMetadata>();

        foreach (var child in section.GetChildren())
        {
            if (TryReplaceStringContainingEnvironmentVariableReferences(child.Value, out var replacedString, out var variableReplacements))
            {
                section[child.Key] = replacedString;
                
                foreach (var variableReplacement in variableReplacements)
                {
                    var envVar = new EnvironmentVariableWithMetadata
                    {
                        Path = child.Path,
                        ConfigurationKey = child.Key,
                        ConfigurationValue = child.Value,
                        ConfigurationValueReplaced = variableReplacement.ReplacedValue,
                        EnvironmentVariableName = variableReplacement.VariableName,
                        EnvironmentVariableValue = variableReplacement.VariableValue
                    };
                    
                    replacedEnvironmentVariables.Add(envVar);
                }
            }
            else
            {
                replacedEnvironmentVariables.AddRange(ReplaceWithEnvironmentVariables(child));
            }
        }
        
        return replacedEnvironmentVariables;
    }

    /// <summary>
    /// Replaces the <c>{ENV:VARIABLE}</c> placeholders in every string value of an options object in place:
    /// string properties, string arrays and string dictionaries, recursively through the nested options classes
    /// and their lists. The paths in the result follow the configuration keys (<c>RayMigrator:Products:0:TargetGroups:1:Targets:0:ConnectionString</c>).
    /// A placeholder whose variable is not set is replaced by an empty string and reported with a null
    /// <see cref="EnvironmentVariableWithMetadata.EnvironmentVariableValue"/>; the caller decides whether that is an error.
    /// </summary>
    public static List<EnvironmentVariableWithMetadata> ReplaceWithEnvironmentVariables(RayMigratorOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var replaced = new List<EnvironmentVariableWithMetadata>();
        ReplaceInObject(options, InternalConstants.RayMigratorSectionName, replaced);
        return replaced;
    }

    private static void ReplaceInObject(object target, string path, List<EnvironmentVariableWithMetadata> replaced)
    {
        foreach (var property in target.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.CanRead || !property.CanWrite || property.GetIndexParameters().Length > 0)
            {
                continue;
            }

            string propertyPath = $"{path}:{property.Name}";

            if (property.PropertyType == typeof(string))
            {
                var value = (string?)property.GetValue(target);
                if (TryReplaceValue(value, propertyPath, replaced, out var replacedValue))
                {
                    property.SetValue(target, replacedValue);
                }

                continue;
            }

            if (property.PropertyType == typeof(string[]))
            {
                var array = (string[]?)property.GetValue(target);
                for (int i = 0; array != null && i < array.Length; i++)
                {
                    if (TryReplaceValue(array[i], $"{propertyPath}:{i}", replaced, out var replacedValue))
                    {
                        array[i] = replacedValue!;
                    }
                }

                continue;
            }

            if (typeof(IDictionary<string, string>).IsAssignableFrom(property.PropertyType))
            {
                var dictionary = (IDictionary<string, string>?)property.GetValue(target);
                foreach (var key in dictionary?.Keys.ToList() ?? [])
                {
                    if (TryReplaceValue(dictionary![key], $"{propertyPath}:{key}", replaced, out var replacedValue))
                    {
                        dictionary[key] = replacedValue!;
                    }
                }

                continue;
            }

            if (IsOptionsClass(property.PropertyType))
            {
                var nested = property.GetValue(target);
                if (nested != null)
                {
                    ReplaceInObject(nested, propertyPath, replaced);
                }

                continue;
            }

            if (property.GetValue(target) is IEnumerable list && property.PropertyType != typeof(string))
            {
                int index = 0;
                foreach (var item in list)
                {
                    if (item != null && IsOptionsClass(item.GetType()))
                    {
                        ReplaceInObject(item, $"{propertyPath}:{index}", replaced);
                    }

                    index++;
                }
            }
        }
    }

    private static bool IsOptionsClass(Type type)
        => type.IsClass && type.Namespace == typeof(RayMigratorOptions).Namespace;

    private static bool TryReplaceValue(string? value, string path, List<EnvironmentVariableWithMetadata> replaced, out string? replacedValue)
    {
        if (!TryReplaceStringContainingEnvironmentVariableReferences(value, out replacedValue, out var variableReplacements))
        {
            return false;
        }

        int separator = path.LastIndexOf(':');
        foreach (var variableReplacement in variableReplacements)
        {
            replaced.Add(new EnvironmentVariableWithMetadata
            {
                Path = path,
                ConfigurationKey = separator < 0 ? path : path[(separator + 1)..],
                ConfigurationValue = value,
                ConfigurationValueReplaced = variableReplacement.ReplacedValue,
                EnvironmentVariableName = variableReplacement.VariableName,
                EnvironmentVariableValue = variableReplacement.VariableValue
            });
        }

        return true;
    }

    /// <summary>
    /// Class for storing data for variable replacements.
    /// </summary>
    public class VariableReplacement()
    {
        public string VariableName { get; set; } = null!;
        public string? VariableValue { get; set; }
        public string? ReplacedValue { get; set; }
    }
    
    /// <summary>
    /// Returns <paramref name="value"/> with every <c>{ENV:VARIABLE}</c> placeholder replaced when every variable it
    /// names is set, otherwise the value as written. For comparing a configured text, such as a product alias, with
    /// a value from the command line before the configuration itself is resolved: the configuration keeps its
    /// placeholders, and a variable that is not set is reported where the configuration is resolved.
    /// </summary>
    public static string ResolveForComparison(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (!TryReplaceStringContainingEnvironmentVariableReferences(value, out var replaced, out var replacements))
            return value;

        return replacements.All(r => !string.IsNullOrWhiteSpace(r.VariableValue)) ? replaced! : value;
    }

    /// <summary>
    /// Tries to replace an input string that contains one or multiple environment-variable references
    /// by their corresponding environment-variable values.
    /// </summary>
    /// <param name="inputString">The original input string optionally containing environment-variable references.</param>
    /// <param name="outputString">The modified input string, either original when no environment-variables found or replaced with corresponding environment-variable values.</param>
    /// <param name="variableReplacements">A List of variables that were tried to be replaced from environment.</param>
    /// <returns>True if any environment-variable reference like {ENV:EnvVariable} has been found within the <see cref="inputString"/>, otherwise false.</returns>
    public static bool TryReplaceStringContainingEnvironmentVariableReferences(string? inputString, out string? outputString, out List<VariableReplacement> variableReplacements)
    {
        variableReplacements = new List<VariableReplacement>();
        outputString = inputString;
        
        if (string.IsNullOrWhiteSpace(inputString))
        {
            return false;
        }
        
        var matches = EnvVariablePattern.Matches(inputString);

        if (matches.Count == 0)
        {
            return false;
        }

        string replacedStringTemp = inputString;

        foreach (Match match in matches)
        {
            string variableName = match.Groups[1].Value;
            string? variableValue = Environment.GetEnvironmentVariable(variableName);
            replacedStringTemp = replacedStringTemp.Replace(match.Value, variableValue);

            var envVar = new VariableReplacement
            {
                VariableName = variableName,
                VariableValue = variableValue,
                ReplacedValue = replacedStringTemp
            };

            variableReplacements.Add(envVar);
        }

        if (variableReplacements.Count > 0)
        {
            outputString = replacedStringTemp;
            return true;
        }

        return false;
    }
}
