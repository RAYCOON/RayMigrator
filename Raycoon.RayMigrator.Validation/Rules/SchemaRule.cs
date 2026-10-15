using Raycoon.RayMigrator.Validation.Messages;
using Raycoon.RayMigrator.Validation.Models;

namespace Raycoon.RayMigrator.Validation.Rules;

/// <summary>
/// Database-schema rules:
/// <list type="bullet">
/// <item><see cref="RuleIds.RULE_4_1"/> — SchemaName on schemaless DB (Sqlite) is a Warning</item>
/// <item><see cref="RuleIds.RULE_4_2"/> — SchemaName is required for SqlServer and PostgreSQL (Error)</item>
/// <item><see cref="RuleIds.RULE_4_3"/> — TableBaseName must be lowercase for PostgreSQL, MariaDB, MySQL (Error)</item>
/// <item><see cref="RuleIds.RULE_4_4"/> — TableBaseName has at most <see cref="MaxTableBaseNameLength"/> bytes in UTF-8 (Error)</item>
/// <item><see cref="RuleIds.RULE_4_5"/> — TableBaseName consists of ASCII letters, digits and underscores (Error)</item>
/// </list>
/// </summary>
internal sealed class SchemaRule : IValidationRule
{
    /// <summary>
    /// The longest prefixed name of the repository, <c>ix_{prefix}migration_record_history_migration_record_id</c>,
    /// has 47 characters plus the prefix; 16 keeps it within the 63 bytes PostgreSQL allows (MySQL and MariaDB: 64 characters).
    /// </summary>
    public const int MaxTableBaseNameLength = 16;

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

        if (!string.IsNullOrEmpty(repo.TableBaseName) && !repo.TableBaseName.All(IsIdentifierCharacter))
        {
            report.AddError(
                RuleIds.RULE_4_5,
                $"{path} > TableBaseName",
                ValidationMessages.Format(ValidationMessages.TableBaseNameInvalidCharacters, repo.TableBaseName));
        }

        if (repo.TableBaseName is { } prefix && System.Text.Encoding.UTF8.GetByteCount(prefix) > MaxTableBaseNameLength)
        {
            report.AddError(
                RuleIds.RULE_4_4,
                $"{path} > TableBaseName",
                ValidationMessages.Format(ValidationMessages.TableBaseNameTooLong, prefix, System.Text.Encoding.UTF8.GetByteCount(prefix), MaxTableBaseNameLength));
        }
    }

    /// <summary>The characters every engine accepts in an unquoted identifier: the prefix is written without quotes or brackets.</summary>
    private static bool IsIdentifierCharacter(char c) =>
        c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '_';
}
