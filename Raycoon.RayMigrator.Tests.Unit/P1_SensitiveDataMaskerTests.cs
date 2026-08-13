using AwesomeAssertions;
using Raycoon.RayMigrator.Core.Configuration;
using Raycoon.RayMigrator.Core.Configuration.Options;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1-13: SensitiveDataMasker tests.
/// Ensures sensitive values are properly masked in log output to prevent data leakage.
/// </summary>
[Collection("SensitiveDataMasker")]
public class SensitiveDataMaskerTests : IDisposable
{
    public SensitiveDataMaskerTests()
    {
        SensitiveDataMasker.Reset();
    }

    public void Dispose()
    {
        SensitiveDataMasker.Reset();
    }

    [Fact]
    public void Mask_WhenRevealFalse_ReplacesSensitiveValues()
    {
        SensitiveDataMasker.Initialize(revealSensitiveData: false);
        SensitiveDataMasker.RegisterSensitiveValue("Server=prod;Password=secret123");

        var result = SensitiveDataMasker.Mask("Connection: Server=prod;Password=secret123 established");

        result.Should().Be($"Connection: {SensitiveDataMasker.MaskString} established");
    }

    [Fact]
    public void Mask_WhenRevealTrue_ReturnsInputUnchanged()
    {
        SensitiveDataMasker.Initialize(revealSensitiveData: true);
        SensitiveDataMasker.RegisterSensitiveValue("secret-password");

        var result = SensitiveDataMasker.Mask("The secret-password is here");

        result.Should().Be("The secret-password is here");
    }

    [Fact]
    public void Mask_WhenNotInitialized_ReturnsInputUnchanged()
    {
        // Do not call Initialize
        var result = SensitiveDataMasker.Mask("some sensitive content");

        result.Should().Be("some sensitive content");
    }

    [Fact]
    public void Mask_MultipleSensitiveValues_AllReplaced()
    {
        SensitiveDataMasker.Initialize(revealSensitiveData: false);
        SensitiveDataMasker.RegisterSensitiveValue("password123");
        SensitiveDataMasker.RegisterSensitiveValue("secret-key-456");

        var result = SensitiveDataMasker.Mask("auth=password123 key=secret-key-456");

        result.Should().Be($"auth={SensitiveDataMasker.MaskString} key={SensitiveDataMasker.MaskString}");
    }

    [Fact]
    public void Mask_OverlappingValues_LongerValueReplacedFirst()
    {
        SensitiveDataMasker.Initialize(revealSensitiveData: false);
        SensitiveDataMasker.RegisterSensitiveValue("secret");
        SensitiveDataMasker.RegisterSensitiveValue("secret-long-value");

        var result = SensitiveDataMasker.Mask("data=secret-long-value");

        // The longer value should be replaced first, resulting in a single mask
        result.Should().Be($"data={SensitiveDataMasker.MaskString}");
    }

    [Fact]
    public void RegisterSensitiveValues_BatchRegistration_AllMasked()
    {
        SensitiveDataMasker.Initialize(revealSensitiveData: false);
        SensitiveDataMasker.RegisterSensitiveValues(new[] { "value1", "value2", "value3" });

        var result = SensitiveDataMasker.Mask("a=value1 b=value2 c=value3");

        result.Should().Be($"a={SensitiveDataMasker.MaskString} b={SensitiveDataMasker.MaskString} c={SensitiveDataMasker.MaskString}");
    }

    [Fact]
    public void Mask_NullInput_ReturnsEmptyString()
    {
        SensitiveDataMasker.Initialize(revealSensitiveData: false);

        var result = SensitiveDataMasker.Mask(null);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Mask_EmptyInput_ReturnsEmptyString()
    {
        SensitiveDataMasker.Initialize(revealSensitiveData: false);

        var result = SensitiveDataMasker.Mask(string.Empty);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Mask_NoSensitiveValuesRegistered_ReturnsInputUnchanged()
    {
        SensitiveDataMasker.Initialize(revealSensitiveData: false);

        var result = SensitiveDataMasker.Mask("some content without sensitive data");

        result.Should().Be("some content without sensitive data");
    }

    [Fact]
    public void Mask_InputContainsNoSensitiveData_ReturnsInputUnchanged()
    {
        SensitiveDataMasker.Initialize(revealSensitiveData: false);
        SensitiveDataMasker.RegisterSensitiveValue("secret-password");

        var result = SensitiveDataMasker.Mask("this text has no matching values");

        result.Should().Be("this text has no matching values");
    }

    [Fact]
    public void RegisterSensitiveValue_NullValue_IsIgnored()
    {
        SensitiveDataMasker.Initialize(revealSensitiveData: false);

        SensitiveDataMasker.RegisterSensitiveValue(null);

        SensitiveDataMasker.RegisteredValueCount.Should().Be(0);
    }

    [Fact]
    public void RegisterSensitiveValue_EmptyValue_IsIgnored()
    {
        SensitiveDataMasker.Initialize(revealSensitiveData: false);

        SensitiveDataMasker.RegisterSensitiveValue(string.Empty);

        SensitiveDataMasker.RegisteredValueCount.Should().Be(0);
    }

    [Fact]
    public void RegisterSensitiveValue_WhitespaceOnly_IsIgnored()
    {
        SensitiveDataMasker.Initialize(revealSensitiveData: false);

        SensitiveDataMasker.RegisterSensitiveValue("   ");

        SensitiveDataMasker.RegisteredValueCount.Should().Be(0);
    }

    [Fact]
    public void RegisterSensitiveValue_DuplicateValue_RegisteredOnce()
    {
        SensitiveDataMasker.Initialize(revealSensitiveData: false);

        SensitiveDataMasker.RegisterSensitiveValue("same-value");
        SensitiveDataMasker.RegisterSensitiveValue("same-value");

        SensitiveDataMasker.RegisteredValueCount.Should().Be(1);
    }

    [Fact]
    public void Reset_ClearsAllState()
    {
        SensitiveDataMasker.Initialize(revealSensitiveData: false);
        SensitiveDataMasker.RegisterSensitiveValue("secret");
        SensitiveDataMasker.RegisteredValueCount.Should().Be(1);

        SensitiveDataMasker.Reset();

        SensitiveDataMasker.RegisteredValueCount.Should().Be(0);
        // After reset, masker is not initialized, so it returns input unchanged (fail-open)
        SensitiveDataMasker.Mask("secret").Should().Be("secret");
    }

    [Fact]
    public void Mask_MultilineSqlContent_SensitiveValuesMasked()
    {
        SensitiveDataMasker.Initialize(revealSensitiveData: false);
        SensitiveDataMasker.RegisterSensitiveValue("Server=prod-db;Database=mydb;User=admin;Password=P@ss!");

        var sqlContent = """
            -- Template: CreateRepository
            -- ConnectionString: Server=prod-db;Database=mydb;User=admin;Password=P@ss!
            CREATE SCHEMA IF NOT EXISTS ray;
            CREATE TABLE ray.MigrationRun (
                Id INT PRIMARY KEY
            );
            """;

        var result = SensitiveDataMasker.Mask(sqlContent);

        result.Should().NotContain("Server=prod-db");
        result.Should().NotContain("P@ss!");
        result.Should().Contain(SensitiveDataMasker.MaskString);
        result.Should().Contain("CREATE SCHEMA IF NOT EXISTS ray;");
    }

    // --- Scope Tests (API mode) ---

    [Fact]
    public void BeginScope_MasksWithinScope()
    {
        SensitiveDataMasker.Initialize(revealSensitiveData: true); // Global: reveal

        using (SensitiveDataMasker.BeginScope(revealSensitiveData: false))
        {
            SensitiveDataMasker.RegisterSensitiveValue("my-secret");
            var result = SensitiveDataMasker.Mask("data=my-secret");
            result.Should().Be($"data={SensitiveDataMasker.MaskString}");
        }
    }

    [Fact]
    public void BeginScope_RevealsWithinScope()
    {
        SensitiveDataMasker.Initialize(revealSensitiveData: false); // Global: mask

        using (SensitiveDataMasker.BeginScope(revealSensitiveData: true))
        {
            SensitiveDataMasker.RegisterSensitiveValue("my-secret");
            var result = SensitiveDataMasker.Mask("data=my-secret");
            result.Should().Be("data=my-secret");
        }
    }

    [Fact]
    public void BeginScope_DisposeCleansUp()
    {
        SensitiveDataMasker.Initialize(revealSensitiveData: false);
        SensitiveDataMasker.RegisterSensitiveValue("global-secret");

        using (SensitiveDataMasker.BeginScope(revealSensitiveData: true))
        {
            // Within scope: reveal mode
            SensitiveDataMasker.Mask("data=global-secret").Should().Be("data=global-secret");
        }

        // After dispose: global state resumes (mask mode)
        SensitiveDataMasker.Mask("data=global-secret").Should().Be($"data={SensitiveDataMasker.MaskString}");
    }

    [Fact]
    public void BeginScope_NestedScopes_RestoresPreviousScope()
    {
        SensitiveDataMasker.Initialize(revealSensitiveData: false);

        using (SensitiveDataMasker.BeginScope(revealSensitiveData: false))
        {
            SensitiveDataMasker.RegisterSensitiveValue("outer-secret");

            using (SensitiveDataMasker.BeginScope(revealSensitiveData: true))
            {
                // Inner scope: reveal mode
                SensitiveDataMasker.RegisterSensitiveValue("inner-secret");
                SensitiveDataMasker.Mask("data=inner-secret").Should().Be("data=inner-secret");
            }

            // Outer scope restored: mask mode, but inner-secret was registered on inner scope (gone now)
            SensitiveDataMasker.Mask("data=outer-secret").Should().Be($"data={SensitiveDataMasker.MaskString}");
        }
    }

    [Fact]
    public async Task BeginScope_ParallelScopes_AreIsolated()
    {
        SensitiveDataMasker.Initialize(revealSensitiveData: false);

        var task1 = Task.Run(() =>
        {
            using var scope = SensitiveDataMasker.BeginScope(revealSensitiveData: false);
            SensitiveDataMasker.RegisterSensitiveValue("task1-secret");
            Thread.Sleep(50); // Ensure overlap
            return SensitiveDataMasker.Mask("data=task1-secret and task2-secret");
        });

        var task2 = Task.Run(() =>
        {
            using var scope = SensitiveDataMasker.BeginScope(revealSensitiveData: true);
            SensitiveDataMasker.RegisterSensitiveValue("task2-secret");
            Thread.Sleep(50); // Ensure overlap
            return SensitiveDataMasker.Mask("data=task1-secret and task2-secret");
        });

        var results = await Task.WhenAll(task1, task2);

        // Task1 (mask mode): should mask task1-secret, but NOT task2-secret (different scope)
        results[0].Should().Be($"data={SensitiveDataMasker.MaskString} and task2-secret");

        // Task2 (reveal mode): should return input unchanged
        results[1].Should().Be("data=task1-secret and task2-secret");
    }

    [Fact]
    public void RegisterSensitiveData_RegistersAllConnectionStrings()
    {
        SensitiveDataMasker.Initialize(revealSensitiveData: false);

        var options = new RayMigratorOptions
        {
            Repository = new RepositoryOptions
            {
                ConnectionString = "Server=repo;Password=repoPass",
                SchemaName = "ray",
                TableBaseName = "Migration"
            },
            DatabaseLogging = new DatabaseLoggingOptions
            {
                ConnectionString = "Server=log;Password=logPass"
            },
            Products = new List<ProductOptions>
            {
                new(migrationRollbackFilesPreExtension: "rollback")
                {
                    MigrationFilesRootDirectory = "/secret/path",
                    TargetGroups = new List<TargetGroupOptions>
                    {
                        new()
                        {
                            Targets = new List<TargetOptions>
                            {
                                new() { ConnectionString = "Server=target1;Password=t1Pass" },
                                new() { ConnectionString = "Server=target2;Password=t2Pass" }
                            }
                        }
                    }
                }
            }
        };

        SensitiveDataMasker.RegisterSensitiveData(options);

        // All values should be masked
        SensitiveDataMasker.Mask("Server=repo;Password=repoPass").Should().Be(SensitiveDataMasker.MaskString);
        SensitiveDataMasker.Mask("ray").Should().Be(SensitiveDataMasker.MaskString);
        SensitiveDataMasker.Mask("Migration").Should().Be(SensitiveDataMasker.MaskString);
        SensitiveDataMasker.Mask("Server=log;Password=logPass").Should().Be(SensitiveDataMasker.MaskString);
        SensitiveDataMasker.Mask("/secret/path").Should().Be(SensitiveDataMasker.MaskString);
        SensitiveDataMasker.Mask("Server=target1;Password=t1Pass").Should().Be(SensitiveDataMasker.MaskString);
        SensitiveDataMasker.Mask("Server=target2;Password=t2Pass").Should().Be(SensitiveDataMasker.MaskString);
    }
}
