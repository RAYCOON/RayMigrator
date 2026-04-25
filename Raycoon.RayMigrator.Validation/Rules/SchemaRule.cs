using Raycoon.RayMigrator.Validation.Messages;
using Raycoon.RayMigrator.Validation.Models;

namespace Raycoon.RayMigrator.Validation.Rules;

/// <summary>
/// Database-schema rules:
/// <list type="bullet">
/// <item><see cref="RuleIds.RULE_4_1"/> — SchemaName on schemaless DB (Sqlite) is a Warning</item>
/// <item><see cref="RuleIds.RULE_4_2"/> — SchemaName is required for SqlServer and PostgreSQL (Error)</item>
/// <item><see cref="RuleIds.RULE_4_3"/> — TableBaseName must be lowercase for PostgreSQL, MariaDB, MySQL (Error)</item>
/// </list>
/// </summary>
internal sealed class SchemaRule : IValidationRule
{
    private static readonly HashSet<string> SchemalessDbTypes = new(StringComparer.OrdinalIgnoreCase) { "Sqlite" };
    private static readonly HashSet<string> SchemaRequiredDbTypes = new(StringComparer.OrdinalIgnoreCase) { "SqlServer", "PostgreSQL" };
    private static readonly HashSet<string> LowercaseIdentifierDbTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "PostgreSQL", "MariaDb", "MySql"
    };

    public void Execute(ValidationInput input, ValidationReport report)
    {
        if (input.Repository is null)
        {
            report.AddError(
                RuleIds.RULE_4_2, // repurposed: missing Repository section
                "Repository",
                "Repository section is missing. Define RayMigrator > Repository with DatabaseType and ConnectionString.");
        }
        else
        {
            CheckRepoPresence(input.Repository, "Repository", report);
            CheckRepo(input.Repository, "Repository", report, required: true);
        }

        if (input.DatabaseLogging is not null)
        {
            CheckRepoPresence(input.DatabaseLogging, "DatabaseLogging", report);
            CheckRepo(input.DatabaseLogging, "DatabaseLogging", report, required: false);
        }
    }

    private static void CheckRepoPresence(RepositoryInput repo, string path, ValidationReport report)
    {
        if (string.IsNullOrWhiteSpace(repo.DatabaseType))
            report.AddError(RuleIds.RULE_4_2, $"{path} > DatabaseType", "DatabaseType is required.");

        if (string.IsNullOrWhiteSpace(repo.ConnectionString))
            report.AddError(RuleIds.RULE_4_2, $"{path} > ConnectionString", "ConnectionString is required.");
    }

    private static void CheckRepo(RepositoryInput repo, string path, ValidationReport report, bool required)
    {
        if (string.IsNullOrWhiteSpace(repo.DatabaseType)) return;

        if (SchemalessDbTypes.Contains(repo.DatabaseType) && !string.IsNullOrWhiteSpace(repo.SchemaName))
        {
            report.AddWarning(
                RuleIds.RULE_4_1,
                $"{path} > SchemaName",
                ValidationMessages.Format(ValidationMessages.SchemaOnSchemalessDb, repo.DatabaseType));
        }

        if (required && SchemaRequiredDbTypes.Contains(repo.DatabaseType) && string.IsNullOrWhiteSpace(repo.SchemaName))
        {
            report.AddError(
                RuleIds.RULE_4_2,
                $"{path} > SchemaName",
                ValidationMessages.Format(ValidationMessages.SchemaMissingForSchemaDb, repo.DatabaseType));
        }

        if (LowercaseIdentifierDbTypes.Contains(repo.DatabaseType)
            && !string.IsNullOrEmpty(repo.TableBaseName)
            && repo.TableBaseName.Any(char.IsUpper))
        {
            report.AddError(
                RuleIds.RULE_4_3,
                $"{path} > TableBaseName",
                ValidationMessages.Format(ValidationMessages.LowercaseTableBaseNameRequired, repo.TableBaseName, repo.DatabaseType));
        }
    }
}
