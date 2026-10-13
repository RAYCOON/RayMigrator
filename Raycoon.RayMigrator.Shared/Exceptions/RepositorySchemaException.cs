namespace Raycoon.RayMigrator.Shared.Exceptions;

/// <summary>
/// The repository cannot be used by this engine: it has repository tables but no schema version, because a
/// RayMigrator before 0.17.0 created it, or its schema version is newer than the engine supports (ADR-024).
/// Raised before anything is written to the repository.
/// </summary>
public class RepositorySchemaException : Exception
{
    private const string AbortMessage = "RayMigrator aborted because the repository schema cannot be used. ";

    /// <summary>The schema version found in the repository; 0 when it has none.</summary>
    public int RepositoryVersion { get; }

    /// <summary>The schema version this engine supports.</summary>
    public int SupportedVersion { get; }

    public RepositorySchemaException(string message, int repositoryVersion, int supportedVersion) : base(AbortMessage + message)
    {
        RepositoryVersion = repositoryVersion;
        SupportedVersion = supportedVersion;
    }
}
