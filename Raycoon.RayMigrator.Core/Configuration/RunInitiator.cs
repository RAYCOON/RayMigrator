using Raycoon.RayMigrator.Core.Configuration.Enums;

namespace Raycoon.RayMigrator.Core.Configuration;

/// <summary>
/// What the command line knows about the initiator of a run: the source (<see cref="MigrationRunSource.Cli"/>, or
/// <see cref="MigrationRunSource.Ci"/> when a CI/CD pipeline variable is set), the user and the machine. The values go
/// into <see cref="Options.RayMigratorConsoleOptions"/> and from there into the <c>MigrationRun</c> row. A host such as
/// RayMigrator Studio sets its own values instead.
/// </summary>
public static class RunInitiator
{
    /// <summary>
    /// The environment variables that mark a CI/CD pipeline, in the order they are checked: the generic <c>CI</c>,
    /// then GitHub Actions, GitLab CI, Azure Pipelines, Jenkins and TeamCity.
    /// </summary>
    public static readonly IReadOnlyList<string> CiVariables =
        ["CI", "GITHUB_ACTIONS", "GITLAB_CI", "TF_BUILD", "JENKINS_URL", "TEAMCITY_VERSION"];

    /// <summary>
    /// <see cref="MigrationRunSource.Ci"/> when one of the <see cref="CiVariables"/> is set to a value other than
    /// <c>false</c> or <c>0</c>, otherwise <see cref="MigrationRunSource.Cli"/>.
    /// </summary>
    public static MigrationRunSource DetectSource() => DetectSource(Environment.GetEnvironmentVariable);

    /// <summary>
    /// The variant of <see cref="DetectSource()"/> that reads the variables through <paramref name="getEnvironmentVariable"/>.
    /// </summary>
    public static MigrationRunSource DetectSource(Func<string, string?> getEnvironmentVariable)
    {
        ArgumentNullException.ThrowIfNull(getEnvironmentVariable);

        foreach (var variable in CiVariables)
        {
            var value = getEnvironmentVariable(variable)?.Trim();
            if (string.IsNullOrEmpty(value))
                continue;
            if (value.Equals("false", StringComparison.OrdinalIgnoreCase) || value == "0")
                continue;
            return MigrationRunSource.Ci;
        }

        return MigrationRunSource.Cli;
    }

    /// <summary>The name of the user running the process, or null when the platform does not provide one.</summary>
    public static string? CurrentUserName
    {
        get
        {
            var name = Environment.UserName;
            return string.IsNullOrWhiteSpace(name) ? null : name;
        }
    }

    /// <summary>The name of the machine running the process, or null when the platform does not provide one.</summary>
    public static string? CurrentHost
    {
        get
        {
            try
            {
                var name = Environment.MachineName;
                return string.IsNullOrWhiteSpace(name) ? null : name;
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }
    }
}
