using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
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
    /// Class for storing data for variable replacements.
    /// </summary>
    public class VariableReplacement()
    {
        public string VariableName { get; set; } = null!;
        public string? VariableValue { get; set; }
        public string? ReplacedValue { get; set; }
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
