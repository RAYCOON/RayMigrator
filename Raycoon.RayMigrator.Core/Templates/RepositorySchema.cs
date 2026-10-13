namespace Raycoon.RayMigrator.Core.Templates;

/// <summary>
/// The version of the repository schema this engine creates and expects (ADR-024). The schema grows in numbered
/// steps: <c>Repository_Upgrade_{n}</c> creates version <c>n</c> on top of version <c>n - 1</c>, step 1 creates
/// the tables of a fresh repository. <c>Repository_CheckCreate</c> reports the version a repository has, and
/// <c>TemplateExecutor.RepositoryCheckCreate</c> applies every missing step up to the target version of the run,
/// which is <see cref="CurrentVersion"/> unless a test pins an older one.
/// </summary>
public static class RepositorySchema
{
    /// <summary>
    /// The newest schema version this engine knows. Every DAL ships <c>Repository_Upgrade_1</c> to
    /// <c>Repository_Upgrade_{CurrentVersion}</c>.
    /// </summary>
    public const int CurrentVersion = 2;

    /// <summary>
    /// The template that creates schema version <paramref name="version"/> on top of the version before it.
    /// </summary>
    /// <param name="version">A version from 1 to <see cref="CurrentVersion"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="version"/> is outside that range.</exception>
    public static TemplateType UpgradeTemplate(int version)
    {
        if (version < 1 || version > CurrentVersion)
            throw new ArgumentOutOfRangeException(nameof(version), version, $"The repository schema has the steps 1 to {CurrentVersion}.");

        return Enum.Parse<TemplateType>($"Repository_Upgrade_{version}");
    }
}
