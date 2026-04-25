using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Core.Configuration.Options;

namespace Raycoon.RayMigrator.Core.Configuration;

/// <summary>
/// Resolves the target environment from console arguments and environment variables.
/// Shared between Engine CLI and Management CLI.
/// </summary>
public static class EnvironmentResolver
{
    /// <summary>
    /// Resolves the target environment from console options and the DOTNET_ENVIRONMENT variable.
    /// </summary>
    /// <returns>
    /// On success: environment and environmentOrigin are set, errorCode is null.
    /// On failure: errorCode is set (2 = conflict, 3 = missing).
    /// </returns>
    public static (string? environment, string? environmentOrigin, int? errorCode) Resolve(
        RayMigratorConsoleOptions consoleOptions, string assemblyInfo)
    {
        string? consoleEnvironment = consoleOptions.Environment;
        string? dotNetEnvironmentVariable = Environment.GetEnvironmentVariable(ConfigurationConstants.DotNetEnvironmentVariableName);

        if (!string.IsNullOrWhiteSpace(consoleEnvironment))
        {
            if (!string.IsNullOrWhiteSpace(dotNetEnvironmentVariable) && consoleEnvironment != dotNetEnvironmentVariable)
            {
                Console.WriteLine(assemblyInfo);
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Environment variable from console argument [-Environment] is set to value [{consoleEnvironment}]. " +
                                  $"Environment variable [{ConfigurationConstants.DotNetEnvironmentVariableName}] is set to value [{dotNetEnvironmentVariable}].\n" +
                                  $"Please use only one way to specify the current environment or set an identical value for both.");
                Console.ResetColor();

                return (null, null, 2);
            }

            return (consoleEnvironment, "console parameter [-Environment]", null);
        }

        if (!string.IsNullOrWhiteSpace(dotNetEnvironmentVariable))
        {
            return (dotNetEnvironmentVariable, $"environment variable [{ConfigurationConstants.DotNetEnvironmentVariableName}]", null);
        }

        Console.WriteLine(assemblyInfo);
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"No environment was provided for {consoleOptions.Command} command. " +
                          $"Please provide either a command line argument [-Environment] or set the environment variable [{ConfigurationConstants.DotNetEnvironmentVariableName}].");
        Console.ResetColor();

        return (null, null, 3);
    }
}
