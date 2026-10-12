using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Core.Configuration.Options;

namespace Raycoon.RayMigrator.Core.Configuration;

/// <summary>
/// Takes the target environment from the console argument [-Environment] and cross-checks it against the
/// DOTNET_ENVIRONMENT variable. The variable never replaces the argument: the rest of the run reads the raw console
/// option, so a value that only the resolver knew would leave the run with an empty environment (#32).
/// Shared between Engine CLI and Management CLI.
/// </summary>
public static class EnvironmentResolver
{
    /// <summary>
    /// Resolves the target environment from console options and the DOTNET_ENVIRONMENT variable.
    /// Prints the banner and an error message to the console on failure.
    /// </summary>
    /// <returns>
    /// On success: environment and environmentOrigin are set, errorCode is null.
    /// On failure: errorCode is set (2 = argument and variable differ, 3 = empty argument).
    /// </returns>
    public static (string? environment, string? environmentOrigin, int? errorCode) Resolve(
        RayMigratorConsoleOptions consoleOptions, string assemblyInfo)
    {
        string? dotNetEnvironmentVariable = Environment.GetEnvironmentVariable(ConfigurationConstants.DotNetEnvironmentVariableName);

        var (environment, environmentOrigin, errorCode, errorMessage) =
            Evaluate(consoleOptions.Environment, dotNetEnvironmentVariable, consoleOptions.Command);

        if (errorMessage != null)
        {
            Console.WriteLine(assemblyInfo);
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(errorMessage);
            Console.ResetColor();
        }

        return (environment, environmentOrigin, errorCode);
    }

    /// <summary>
    /// Decision behind <see cref="Resolve"/> without console output: an empty or whitespace console argument is refused
    /// with code 3 whether or not the variable is set, a variable that differs from the argument is a conflict with
    /// code 2, otherwise the argument is the environment.
    /// </summary>
    /// <returns>
    /// On success: environment and environmentOrigin are set, errorCode and errorMessage are null.
    /// On failure: errorCode and errorMessage are set, environment and environmentOrigin are null.
    /// </returns>
    internal static (string? environment, string? environmentOrigin, int? errorCode, string? errorMessage) Evaluate(
        string? consoleEnvironment, string? dotNetEnvironmentVariable, MigrationCommand command)
    {
        bool variableIsSet = !string.IsNullOrWhiteSpace(dotNetEnvironmentVariable);

        if (string.IsNullOrWhiteSpace(consoleEnvironment))
        {
            string message = $"No environment was provided for {command} command: the console argument [-Environment] is empty. " +
                             "Please provide a non-empty environment name.";

            if (variableIsSet)
            {
                message += Environment.NewLine +
                           $"The environment variable [{ConfigurationConstants.DotNetEnvironmentVariableName}] is set to value [{dotNetEnvironmentVariable}], " +
                           "but it is only cross-checked against the console argument and does not replace it.";
            }

            return (null, null, 3, message);
        }

        if (variableIsSet && !string.Equals(consoleEnvironment, dotNetEnvironmentVariable, StringComparison.Ordinal))
        {
            string message = $"Environment variable from console argument [-Environment] is set to value [{consoleEnvironment}]. " +
                             $"Environment variable [{ConfigurationConstants.DotNetEnvironmentVariableName}] is set to value [{dotNetEnvironmentVariable}]." + Environment.NewLine +
                             "Please set an identical value for both or unset the environment variable.";

            return (null, null, 2, message);
        }

        return (consoleEnvironment, "console parameter [-Environment]", null, null);
    }
}
