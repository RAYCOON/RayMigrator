using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Raycoon.RayMigrator.Database.Common;
using Raycoon.RayMigrator.Shared.Exceptions;

namespace Raycoon.RayMigrator.Core.Configuration.Validation;

public static class SchemaNameValidator
{
    public static void ValidateSchemaName(
        ConcurrentDictionary<string, DalSpecificProperties> dalProperties,
        string databaseType, string? schemaName,
        string contextLabel, ILogger logger)
    {
        if (!dalProperties.TryGetValue(databaseType, out var props))
            return;

        if (props.SupportsSchema && string.IsNullOrWhiteSpace(schemaName))
            throw new ConfigurationValidationException(
                $"SchemaName is required for DatabaseType [{databaseType}] in [{contextLabel}].");

        if (!props.SupportsSchema && !string.IsNullOrWhiteSpace(schemaName))
            logger.LogWarning("SchemaName [{SchemaName}] provided for DatabaseType [{DatabaseType}] " +
                "in [{ContextLabel}] will be ignored — this database type does not support schemas.",
                schemaName, databaseType, contextLabel);
    }
}
