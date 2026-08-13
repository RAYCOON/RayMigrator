using AwesomeAssertions;
using Raycoon.RayMigrator.Services;
using Raycoon.RayMigrator.Tests.Unit.Helpers;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1: Tests for environment variable replacement in SQL blocks.
/// Ensures {ENV:VARIABLE_NAME} placeholders are correctly replaced at execution time.
/// </summary>
public class SqlBlockEnvironmentVariableTests : IDisposable
{
    private const string TestPrefix = "RAYMIGRATOR_TEST_";
    private readonly List<string> _setVariables = new();
    private readonly MigrationService _service;

    public SqlBlockEnvironmentVariableTests()
    {
        _service = TestFactories.CreateUninitializedMigrationService();
    }

    private void SetEnvVar(string name, string value)
    {
        Environment.SetEnvironmentVariable(name, value);
        _setVariables.Add(name);
    }

    public void Dispose()
    {
        foreach (var name in _setVariables)
        {
            Environment.SetEnvironmentVariable(name, null);
        }
    }

    [Fact]
    public void SingleEnvVar_ReplacedCorrectly()
    {
        SetEnvVar($"{TestPrefix}ADMIN", "admin_user");
        string sqlInput = $"INSERT INTO Users (Name) VALUES ('{{ENV:{TestPrefix}ADMIN}}')";

        var result = _service.ReplaceEnvironmentVariablesInSqlBlock(sqlInput, "10_Create.sql", 1, 1);

        result.Should().Be("INSERT INTO Users (Name) VALUES ('admin_user')");
    }

    [Fact]
    public void MultipleEnvVars_AllReplacedCorrectly()
    {
        SetEnvVar($"{TestPrefix}SCHEMA", "dbo");
        SetEnvVar($"{TestPrefix}TABLE", "Users");

        string sqlInput = $"SELECT * FROM {{ENV:{TestPrefix}SCHEMA}}.{{ENV:{TestPrefix}TABLE}}";

        var result = _service.ReplaceEnvironmentVariablesInSqlBlock(sqlInput, "10_Query.sql", 1, 1);

        result.Should().Be("SELECT * FROM dbo.Users");
    }

    [Fact]
    public void NonExistentEnvVar_ReplacedWithEmptyString()
    {
        // Ensure variable does not exist
        string varName = $"{TestPrefix}NONEXISTENT_{Guid.NewGuid():N}";
        Environment.SetEnvironmentVariable(varName, null);

        string sqlInput = $"INSERT INTO Config (Value) VALUES ('{{ENV:{varName}}}')";

        var result = _service.ReplaceEnvironmentVariablesInSqlBlock(sqlInput, "10_Config.sql", 1, 1);

        result.Should().Be("INSERT INTO Config (Value) VALUES ('')");
    }

    [Fact]
    public void NoEnvVars_SqlUnchanged()
    {
        string sqlInput = "CREATE TABLE Users (Id INT PRIMARY KEY, Name NVARCHAR(100))";

        var result = _service.ReplaceEnvironmentVariablesInSqlBlock(sqlInput, "10_Create.sql", 1, 1);

        result.Should().Be(sqlInput);
    }

    [Fact]
    public void MixedExistingAndNonExisting_PartialReplacement()
    {
        SetEnvVar($"{TestPrefix}EXISTING", "real_value");
        string nonExistentVar = $"{TestPrefix}MISSING_{Guid.NewGuid():N}";
        Environment.SetEnvironmentVariable(nonExistentVar, null);

        string sqlInput = $"INSERT INTO T (A, B) VALUES ('{{ENV:{TestPrefix}EXISTING}}', '{{ENV:{nonExistentVar}}}')";

        var result = _service.ReplaceEnvironmentVariablesInSqlBlock(sqlInput, "10_Mixed.sql", 1, 2);

        result.Should().Be("INSERT INTO T (A, B) VALUES ('real_value', '')");
    }

    [Fact]
    public void EnvVarInInsertStatement_ReplacedCorrectly()
    {
        SetEnvVar($"{TestPrefix}ADMIN_USER", "superadmin");
        SetEnvVar($"{TestPrefix}ADMIN_EMAIL", "admin@example.com");

        string sqlInput = $@"INSERT INTO Users (Username, Email, IsActive)
VALUES ('{{ENV:{TestPrefix}ADMIN_USER}}', '{{ENV:{TestPrefix}ADMIN_EMAIL}}', 1)";

        var result = _service.ReplaceEnvironmentVariablesInSqlBlock(sqlInput, "20_SeedAdmin.sql", 1, 1);

        result.Should().Be(@"INSERT INTO Users (Username, Email, IsActive)
VALUES ('superadmin', 'admin@example.com', 1)");
    }
}
