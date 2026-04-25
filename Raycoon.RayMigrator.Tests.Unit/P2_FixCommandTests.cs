using FluentAssertions;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Core.Templates;
using Raycoon.RayMigrator.Services.Abstractions;
// Note: CLI parsing tests would require System.CommandLine dependency in test project
// These are covered by integration tests and manual testing instead

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P2: FixIssuesRequest and FixIssuesResult model tests.
/// </summary>
public class FixIssuesRequestModelTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var request = new FixIssuesRequest();

        request.ProductAlias.Should().BeEmpty();
        request.Environment.Should().BeEmpty();
        request.Scope.Should().Be(FixIssues.OrphanedRuns);
        request.OlderThanMinutes.Should().Be(60);
        request.DryRun.Should().BeFalse();
        request.AssumedMigrationStatus.Should().Be(MigrationStatus.NotMigrated);
        request.ShowInfo.Should().BeTrue();
        request.RevealSensitiveData.Should().BeFalse();
    }

    [Fact]
    public void AllProperties_CanBeSet()
    {
        var request = new FixIssuesRequest
        {
            ProductAlias = "TestProduct",
            Environment = "Docker",
            Scope = FixIssues.All,
            OlderThanMinutes = 0,
            DryRun = true,
            AssumedMigrationStatus = MigrationStatus.Migrated,
            ShowInfo = false,
            RevealSensitiveData = true
        };

        request.ProductAlias.Should().Be("TestProduct");
        request.Environment.Should().Be("Docker");
        request.Scope.Should().Be(FixIssues.All);
        request.OlderThanMinutes.Should().Be(0);
        request.DryRun.Should().BeTrue();
        request.AssumedMigrationStatus.Should().Be(MigrationStatus.Migrated);
        request.ShowInfo.Should().BeFalse();
        request.RevealSensitiveData.Should().BeTrue();
    }

    [Fact]
    public void OlderThanMinutes_ZeroMeansImmediate()
    {
        var request = new FixIssuesRequest { OlderThanMinutes = 0 };

        request.OlderThanMinutes.Should().Be(0, "0 means fix immediately regardless of age");
    }
}

/// <summary>
/// P2: FixIssuesResult model tests.
/// </summary>
public class FixIssuesResultModelTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var result = new FixIssuesResult();

        result.ProductAlias.Should().BeEmpty();
        result.Environment.Should().BeEmpty();
        result.WasDryRun.Should().BeFalse();
        result.OrphanedRunsFound.Should().Be(0);
        result.OrphanedRunsFixed.Should().Be(0);
        result.OrphanedRuns.Should().BeEmpty();
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().BeNull();
        result.Messages.Should().BeEmpty();
    }

    [Fact]
    public void InheritsFromOperationResult()
    {
        var result = new FixIssuesResult();

        result.Should().BeAssignableTo<OperationResult>();
    }

    [Fact]
    public void DryRunResult_HasCorrectValues()
    {
        var result = new FixIssuesResult
        {
            Success = true,
            WasDryRun = true,
            OrphanedRunsFound = 2,
            OrphanedRunsFixed = 0,
        };

        result.WasDryRun.Should().BeTrue();
        result.OrphanedRunsFound.Should().Be(2);
        result.OrphanedRunsFixed.Should().Be(0, "dry-run should not fix anything");
    }

    [Fact]
    public void FixedResult_HasCorrectValues()
    {
        var result = new FixIssuesResult
        {
            Success = true,
            WasDryRun = false,
            OrphanedRunsFound = 1,
            OrphanedRunsFixed = 1,
            OrphanedRuns = new List<OrphanedRunInfo>
            {
                new()
                {
                    MigrationRunId = 42,
                    Environment = "Docker",
                    StartedAt = new DateTime(2026, 2, 12, 10, 0, 0),
                    MinutesRunning = 120,
                    MigrationRunModeId = 100,
                    WasFixed = true
                }
            }
        };

        result.OrphanedRunsFixed.Should().Be(1);
        result.OrphanedRuns.Should().HaveCount(1);
        result.OrphanedRuns[0].WasFixed.Should().BeTrue();
        result.OrphanedRuns[0].MigrationRunId.Should().Be(42);
    }
}

/// <summary>
/// P2: OrphanedRunInfo model tests.
/// </summary>
public class OrphanedRunInfoModelTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var info = new OrphanedRunInfo();

        info.MigrationRunId.Should().Be(0);
        info.Environment.Should().BeEmpty();
        info.MinutesRunning.Should().Be(0);
        info.MigrationRunModeId.Should().Be(0);
        info.WasFixed.Should().BeFalse();
    }

    [Fact]
    public void AllProperties_CanBeSet()
    {
        var info = new OrphanedRunInfo
        {
            MigrationRunId = 5,
            Environment = "Production",
            StartedAt = new DateTime(2026, 1, 15, 8, 0, 0),
            MinutesRunning = 90.5,
            MigrationRunModeId = 100,
            WasFixed = true
        };

        info.MigrationRunId.Should().Be(5);
        info.Environment.Should().Be("Production");
        info.MinutesRunning.Should().BeApproximately(90.5, 0.01);
        info.MigrationRunModeId.Should().Be(100);
        info.WasFixed.Should().BeTrue();
    }
}

/// <summary>
/// P2: FixIssues enum tests.
/// </summary>
public class FixIssuesEnumTests
{
    [Fact]
    public void OrphanedRuns_HasExpectedValue()
    {
        ((byte)FixIssues.OrphanedRuns).Should().Be(2);
    }

    [Fact]
    public void All_HasExpectedValue()
    {
        ((byte)FixIssues.All).Should().Be(1);
    }

    [Fact]
    public void Undefined_HasExpectedValue()
    {
        ((byte)FixIssues.Undefined).Should().Be(0);
    }
}

/// <summary>
/// P2: TemplateType enum tests for new Fix command template types.
/// </summary>
public class FixCommandTemplateTypeTests
{
    [Fact]
    public void NewTemplateTypes_AreDefined()
    {
        Enum.IsDefined(typeof(TemplateType), TemplateType.Repository_MigrationRun_SelectOrphaned).Should().BeTrue();
        Enum.IsDefined(typeof(TemplateType), TemplateType.Repository_MigrationRun_FixOrphaned).Should().BeTrue();
        Enum.IsDefined(typeof(TemplateType), TemplateType.Repository_MigrationRecord_FixOrphaned).Should().BeTrue();
    }
}

/// <summary>
/// P2: SQL template structure tests for new Fix command templates.
/// </summary>
public class FixCommandSqlTemplateStructureTests
{
    [Theory]
    [InlineData("SqlServer")]
    [InlineData("PostgreSQL")]
    [InlineData("MariaDb")]
    [InlineData("MySql")]
    public void SelectOrphaned_TemplateExists_AndContainsExpectedContent(string engine)
    {
        var templatePath = GetTemplatePath(engine, "Repository_MigrationRun_SelectOrphaned.sql");
        var content = File.ReadAllText(templatePath);

        content.Should().Contain("MigrationRunId", $"{engine} SelectOrphaned should return MigrationRunId");
        content.Should().Contain("MinutesRunning", $"{engine} SelectOrphaned should calculate MinutesRunning");
        content.Should().Contain("MigrationRunResultId", $"{engine} SelectOrphaned should filter by MigrationRunResultId");
        content.Should().Contain("FinishedAt", $"{engine} SelectOrphaned should filter by FinishedAt IS NULL");
    }

    [Theory]
    [InlineData("SqlServer")]
    [InlineData("PostgreSQL")]
    [InlineData("MariaDb")]
    [InlineData("MySql")]
    public void FixOrphanedRun_TemplateExists_AndContainsExpectedContent(string engine)
    {
        var templatePath = GetTemplatePath(engine, "Repository_MigrationRun_FixOrphaned.sql");
        var content = File.ReadAllText(templatePath);

        content.Should().Contain("MigrationRunResultId", $"{engine} FixOrphanedRun should update MigrationRunResultId");
        content.Should().Contain("FinishedAt", $"{engine} FixOrphanedRun should set FinishedAt");
        content.Should().Contain("DurationInMs", $"{engine} FixOrphanedRun should calculate DurationInMs");
        content.Should().Contain("orphaned run cleanup", $"{engine} FixOrphanedRun should include cleanup message");
    }

    [Theory]
    [InlineData("SqlServer")]
    [InlineData("PostgreSQL")]
    [InlineData("MariaDb")]
    [InlineData("MySql")]
    public void FixOrphanedMigration_TemplateExists_AndContainsExpectedContent(string engine)
    {
        var templatePath = GetTemplatePath(engine, "Repository_MigrationRecord_FixOrphaned.sql");
        var content = File.ReadAllText(templatePath);

        content.Should().Contain("MigrationStatusId", $"{engine} FixOrphanedMigration should update MigrationStatusId");
        content.Should().Contain("MigrationRunId", $"{engine} FixOrphanedMigration should filter by MigrationRunId");
    }

    [Theory]
    [InlineData("SqlServer")]
    [InlineData("PostgreSQL")]
    [InlineData("MariaDb")]
    [InlineData("MySql")]
    public void AllFixTemplates_HaveCorrectTomlHeaders(string engine)
    {
        var templates = new[]
        {
            "Repository_MigrationRun_SelectOrphaned.sql",
            "Repository_MigrationRun_FixOrphaned.sql",
            "Repository_MigrationRecord_FixOrphaned.sql"
        };

        foreach (var templateFile in templates)
        {
            var templatePath = GetTemplatePath(engine, templateFile);
            var content = File.ReadAllText(templatePath);

            content.Should().Contain("[RayMigratorTemplate]", $"{engine}/{templateFile} should have TOML header");
            content.Should().Contain($"DatabaseType   = \"{engine}\"", $"{engine}/{templateFile} should declare correct DatabaseType");
            content.Should().Contain("Version        = \"", $"{engine}/{templateFile} should have a Version field in the TOML header");
        }
    }

    [Theory]
    [InlineData("MariaDb")]
    [InlineData("MySql")]
    public void MariaDbMySql_GetLock_TemplateContainsAdvisoryLock(string engine)
    {
        var templatePath = GetTemplatePath(engine, "Repository_MigrationRun_Insert.sql");
        var content = File.ReadAllText(templatePath);

        content.Should().Contain("GET_LOCK(", $"{engine} MigrationRunInsert should use GET_LOCK()");
        content.Should().Contain("RELEASE_LOCK(", $"{engine} MigrationRunInsert should use RELEASE_LOCK()");
        content.Should().Contain("@v_lock_name", $"{engine} MigrationRunInsert should define lock name variable");
        content.Should().Contain("@v_lock_acquired", $"{engine} MigrationRunInsert should check lock result");
        content.Should().NotContain("FOR UPDATE", $"{engine} MigrationRunInsert should no longer use FOR UPDATE");
    }

    #region Helper Methods

    private static string GetTemplatePath(string engine, string templateFile)
    {
        var assemblyDir = AppDomain.CurrentDomain.BaseDirectory;
        var templatePath = Path.Combine(assemblyDir, "DataAccessLayers", engine, templateFile);

        if (!File.Exists(templatePath))
        {
            var solutionDir = FindSolutionDir(assemblyDir);
            if (solutionDir != null)
            {
                templatePath = Path.Combine(solutionDir, "Raycoon.RayMigrator.Database", "DataAccessLayers", engine, templateFile);
            }
        }

        if (!File.Exists(templatePath))
        {
            throw new FileNotFoundException($"Template file not found: {templatePath}");
        }

        return templatePath;
    }

    private static string? FindSolutionDir(string startDir)
    {
        var dir = startDir;
        while (dir != null)
        {
            if (Directory.GetFiles(dir, "*.sln").Length > 0)
                return dir;
            dir = Directory.GetParent(dir)?.FullName;
        }
        return null;
    }

    #endregion
}

/// <summary>
/// P2: RayMigratorConsoleOptions Fix command property tests.
/// </summary>
public class FixCommandConsoleOptionsTests
{
    [Fact]
    public void NewFixProperties_DefaultToNull()
    {
        var options = new RayMigratorConsoleOptions
        {
            Command = MigrationCommand.FixIssues,
            Product = "Test",
            Environment = "Docker",
            RunMode = MigrationRunMode.Migrate,
            ShowStartupInfo = true,
            RevealSensitiveData = false
        };

        options.FixOlderThanMinutes.Should().BeNull();
        options.FixDryRun.Should().BeNull();
        options.FixAssumedMigrationStatus.Should().BeNull();
    }

    [Fact]
    public void NewFixProperties_CanBeSet()
    {
        var options = new RayMigratorConsoleOptions
        {
            Command = MigrationCommand.FixIssues,
            Product = "Test",
            Environment = "Docker",
            RunMode = MigrationRunMode.Migrate,
            ShowStartupInfo = true,
            RevealSensitiveData = false,
            FixOlderThanMinutes = 30,
            FixDryRun = true,
            FixAssumedMigrationStatus = MigrationStatus.Migrated
        };

        options.FixOlderThanMinutes.Should().Be(30);
        options.FixDryRun.Should().BeTrue();
        options.FixAssumedMigrationStatus.Should().Be(MigrationStatus.Migrated);
    }
}
