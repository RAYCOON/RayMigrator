using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using DataAnnotationResult = System.ComponentModel.DataAnnotations.ValidationResult;
using Microsoft.Extensions.Logging;
using Raycoon.RayMigrator.Core.Configuration;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Core.Configuration.Replacer;
using Raycoon.RayMigrator.Core.Configuration.Validation;
using Raycoon.RayMigrator.Core.Templates;
using Raycoon.RayMigrator.Database;
using Raycoon.RayMigrator.Database.Common;
using Raycoon.RayMigrator.Shared.Exceptions;
using Raycoon.RayMigrator.Validation;
using Raycoon.RayMigrator.Validation.Models;

namespace Raycoon.RayMigrator.Services;

/// <summary>
/// The way a host reaches a repository from its options alone, shared by the repository services
/// (<see cref="RepositorySchemaService"/>, <see cref="RepositoryMaintenanceService"/>): the options are validated the way
/// the context factory validates the repository of a run, their secrets are registered with the masker of the current
/// scope, and the DAL of the repository is created. A repository a service accepts passes the validation of every run.
/// </summary>
internal static class RepositoryAccess
{
    /// <summary>
    /// Resolves the <c>{ENV:VAR}</c> placeholders, applies the data annotations with their defaults (a null
    /// <c>TableBaseName</c> becomes the empty prefix), checks the database type against the templates of the process and
    /// the rules of the catalog that judge a repository on its own, checks the schema name against the DAL and registers
    /// the connection string, the schema name and the prefix with the masker. The caller opens the masking scope.
    /// </summary>
    /// <returns>The DAL of the repository's database type and connection string.</returns>
    /// <exception cref="ConfigurationValidationException">The options are incomplete or invalid, a placeholder names a
    /// variable that is not set, the database type is unknown or has no templates, or the schema name is missing where
    /// the engine needs one.</exception>
    internal static IDal Prepare(RepositoryOptions repository, TemplateCache templateCache, ILogger logger)
    {
        ResolvePlaceholders(repository);
        Validate(repository);

        string databaseType = repository.DatabaseType!;
        var available = templateCache.GetAvailableDatabaseTypes();
        if (!available.Contains(databaseType, StringComparer.Ordinal))
        {
            string hint = available.FirstOrDefault(t => t.Equals(databaseType, StringComparison.OrdinalIgnoreCase)) is { } near
                ? $" Did you mean [{near}]? The DAL is looked up by the exact name."
                : string.Empty;
            throw new ConfigurationValidationException(
                $"Unknown DatabaseType [{databaseType}] for the repository. Templates exist for [{string.Join(", ", available)}].{hint}");
        }

        ValidateWithCatalog(repository);

        DalFactory.TryGetDal(databaseType, repository.ConnectionString!, out IDal? dal);
        var dalProperties = new ConcurrentDictionary<string, DalSpecificProperties>();
        dalProperties.TryAdd(databaseType, dal!.DalSpecificProperties);
        SchemaNameValidator.ValidateSchemaName(dalProperties, databaseType, repository.SchemaName, "Repository", logger);

        SensitiveDataMasker.RegisterSensitiveValues([repository.ConnectionString, repository.SchemaName, repository.TableBaseName]);

        return dal;
    }

    /// <summary>
    /// The rules of the catalog that judge a repository on its own: the schema name where the engine needs one
    /// (RULE_4_2), a lowercase prefix on the engines that fold identifiers (RULE_4_3), the length of the prefix
    /// (RULE_4_4) and its characters (RULE_4_5). A repository a service accepts must pass the validation of every
    /// later run.
    /// </summary>
    private static void ValidateWithCatalog(RepositoryOptions repository)
    {
        var report = RuleCatalog.RunAll(new ValidationInput
        {
            Repository = new RepositoryInput
            {
                DatabaseType = repository.DatabaseType,
                ConnectionString = repository.ConnectionString,
                SchemaName = repository.SchemaName,
                TableBaseName = repository.TableBaseName
            },
            Products = [],
            CliTools = [],
            Defaults = new ProductDefaultsInput()
        });
        var errors = report.Errors.Where(i => i.Path.StartsWith("Repository", StringComparison.Ordinal)).ToList();
        if (errors.Count > 0)
        {
            throw new ConfigurationValidationException(
                "The repository options are not valid: " + string.Join(" ", errors.Select(e => $"[{e.Code}] {e.Path}: {e.Message}")));
        }
    }

    /// <summary>
    /// Resolves the <c>{ENV:VAR}</c> placeholders of the options with the environment of this process, as the
    /// options preparer resolves them before a run, and refuses a variable that is not set: a host that hands the
    /// options over as written must not end up with a database named after a placeholder.
    /// </summary>
    private static void ResolvePlaceholders(RepositoryOptions repository)
    {
        var replaced = EnvironmentVariableReplacer.ReplaceWithEnvironmentVariables(new RayMigratorOptions { Repository = repository });
        var missing = replaced
            .Where(r => string.IsNullOrWhiteSpace(r.EnvironmentVariableValue))
            .Select(r => $"[{r.EnvironmentVariableName}] at [{r.Path}]")
            .Distinct()
            .ToList();
        if (missing.Count > 0)
        {
            throw new ConfigurationValidationException(
                "The repository options reference environment variables that do not exist, are empty or contain whitespace only: " + string.Join(", ", missing));
        }

        SensitiveDataMasker.RegisterSensitiveValues(replaced.Select(r => r.EnvironmentVariableValue));
    }

    /// <summary>
    /// The data annotations of <see cref="RepositoryOptions"/>: the required database type, the connection string
    /// syntax and the documented defaults of the command settings, which the attributes write into null properties.
    /// </summary>
    private static void Validate(RepositoryOptions repository)
    {
        var findings = new List<DataAnnotationResult>();
        Validator.TryValidateObject(repository, new ValidationContext(repository), findings, validateAllProperties: true);
        if (string.IsNullOrWhiteSpace(repository.ConnectionString))
            findings.Add(new DataAnnotationResult("The ConnectionString of the repository is required.", [nameof(RepositoryOptions.ConnectionString)]));

        if (findings.Count > 0)
        {
            throw new ConfigurationValidationException(
                "The repository options are not valid: " + string.Join(" ", findings.Select(f => f.ErrorMessage)));
        }

        // The templates substitute the prefix and refuse a missing value; no prefix is the empty string.
        repository.TableBaseName ??= string.Empty;
    }
}
