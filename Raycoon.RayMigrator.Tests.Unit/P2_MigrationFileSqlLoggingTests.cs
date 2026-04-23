// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

using FluentAssertions;
using Microsoft.Extensions.Logging;
using Raycoon.RayMigrator.Core.Configuration;
using Raycoon.RayMigrator.Tests.Unit.Helpers;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P2: Tests for migration file SQL content logging behavior.
/// Verifies that ENV var replacement logs are at the correct log level
/// and that SensitiveDataMasker is applied to SQL content in logs.
/// </summary>
[Collection("SensitiveDataMasker")]
public class MigrationFileSqlLoggingTests : IDisposable
{
    private const string TestPrefix = "RAYMIGRATOR_LOGTEST_";
    private readonly List<string> _setVariables = new();

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
        SensitiveDataMasker.Reset();
    }

    [Fact]
    public void EnvVarReplacement_Success_LogsAtTraceLevel()
    {
        var (service, logger) = TestFactories.CreateMigrationServiceWithCapturingLogger();
        SetEnvVar($"{TestPrefix}DB_NAME", "production_db");

        string sql = $"USE {{ENV:{TestPrefix}DB_NAME}}";
        service.ReplaceEnvironmentVariablesInSqlBlock(sql, "10_Create.sql", 1, 1);

        var traceEntries = logger.Entries.Where(e => e.LogLevel == LogLevel.Trace).ToList();
        traceEntries.Should().ContainSingle();
        traceEntries[0].Message.Should().Contain($"{TestPrefix}DB_NAME");
    }

    [Fact]
    public void EnvVarReplacement_Success_DoesNotLogAtDebugLevel()
    {
        var (service, logger) = TestFactories.CreateMigrationServiceWithCapturingLogger();
        SetEnvVar($"{TestPrefix}SCHEMA", "dbo");

        string sql = $"SELECT * FROM {{ENV:{TestPrefix}SCHEMA}}.Users";
        service.ReplaceEnvironmentVariablesInSqlBlock(sql, "10_Query.sql", 1, 1);

        var debugEntries = logger.Entries.Where(e => e.LogLevel == LogLevel.Debug).ToList();
        debugEntries.Should().BeEmpty("ENV var replacement was demoted from Debug to Trace");
    }

    [Fact]
    public void EnvVarReplacement_MissingVariable_LogsWarning()
    {
        var (service, logger) = TestFactories.CreateMigrationServiceWithCapturingLogger();
        string varName = $"{TestPrefix}MISSING_{Guid.NewGuid():N}";

        string sql = $"USE {{ENV:{varName}}}";
        service.ReplaceEnvironmentVariablesInSqlBlock(sql, "10_Create.sql", 1, 1);

        var warningEntries = logger.Entries.Where(e => e.LogLevel == LogLevel.Warning).ToList();
        warningEntries.Should().ContainSingle();
        warningEntries[0].Message.Should().Contain(varName);
        warningEntries[0].Message.Should().Contain("is not set");
    }

    [Fact]
    public void EnvVarReplacement_MixedExistingAndMissing_CorrectLogLevels()
    {
        var (service, logger) = TestFactories.CreateMigrationServiceWithCapturingLogger();
        SetEnvVar($"{TestPrefix}HOST", "localhost");
        string missingVar = $"{TestPrefix}MISSING_{Guid.NewGuid():N}";

        string sql = $"Server={{ENV:{TestPrefix}HOST}};Database={{ENV:{missingVar}}}";
        service.ReplaceEnvironmentVariablesInSqlBlock(sql, "10_Config.sql", 1, 1);

        logger.Entries.Where(e => e.LogLevel == LogLevel.Trace).Should().ContainSingle(
            "existing ENV var replacement should be logged at Trace");
        logger.Entries.Where(e => e.LogLevel == LogLevel.Warning).Should().ContainSingle(
            "missing ENV var should be logged at Warning");
        logger.Entries.Where(e => e.LogLevel == LogLevel.Debug).Should().BeEmpty(
            "no Debug-level logs should be emitted for ENV var replacements");
    }

    [Fact]
    public void EnvVarReplacement_NoVariables_NoLogEntries()
    {
        var (service, logger) = TestFactories.CreateMigrationServiceWithCapturingLogger();

        string sql = "CREATE TABLE Users (Id INT PRIMARY KEY)";
        service.ReplaceEnvironmentVariablesInSqlBlock(sql, "10_Create.sql", 1, 1);

        logger.Entries.Should().BeEmpty("no ENV vars means no replacement logging");
    }

    [Fact]
    public void SensitiveDataMasker_MasksSensitiveValuesInSqlContent()
    {
        SensitiveDataMasker.Initialize(revealSensitiveData: false);
        SensitiveDataMasker.RegisterSensitiveValue("SuperSecret123");

        string sqlWithSecret = "INSERT INTO Config (ConnStr) VALUES ('Server=db;Password=SuperSecret123')";
        var masked = SensitiveDataMasker.Mask(sqlWithSecret);

        masked.Should().NotContain("SuperSecret123");
        masked.Should().Contain(SensitiveDataMasker.MaskString);
        masked.Should().Contain("INSERT INTO Config");
    }

    [Fact]
    public void SensitiveDataMasker_RevealSensitiveData_DoesNotMask()
    {
        SensitiveDataMasker.Initialize(revealSensitiveData: true);
        SensitiveDataMasker.RegisterSensitiveValue("SuperSecret123");

        string sqlWithSecret = "INSERT INTO Config (ConnStr) VALUES ('Server=db;Password=SuperSecret123')";
        var masked = SensitiveDataMasker.Mask(sqlWithSecret);

        masked.Should().Contain("SuperSecret123");
    }

    [Fact]
    public void EnvVarReplacement_MultipleVariables_AllLoggedAtTrace()
    {
        var (service, logger) = TestFactories.CreateMigrationServiceWithCapturingLogger();
        SetEnvVar($"{TestPrefix}VAR_A", "valueA");
        SetEnvVar($"{TestPrefix}VAR_B", "valueB");

        string sql = $"INSERT INTO T (A, B) VALUES ('{{ENV:{TestPrefix}VAR_A}}', '{{ENV:{TestPrefix}VAR_B}}')";
        service.ReplaceEnvironmentVariablesInSqlBlock(sql, "20_Seed.sql", 1, 1);

        var traceEntries = logger.Entries.Where(e => e.LogLevel == LogLevel.Trace).ToList();
        traceEntries.Should().HaveCount(2);
        traceEntries.Should().Contain(e => e.Message.Contains($"{TestPrefix}VAR_A"));
        traceEntries.Should().Contain(e => e.Message.Contains($"{TestPrefix}VAR_B"));
    }
}
