using Raycoon.RayMigrator.Core.Configuration.Enums;

namespace Raycoon.RayMigrator.Tests.Engine.Infrastructure;

/// <summary>
/// What a scenario tells the engine about the initiator of its runs; goes into the console options of the test host
/// like a host such as RayMigrator Studio would set them.
/// </summary>
public sealed record RunInitiatorValues(MigrationRunSource? Source, string? InitiatedBy, string? InitiatedBySubject, string? Host);
