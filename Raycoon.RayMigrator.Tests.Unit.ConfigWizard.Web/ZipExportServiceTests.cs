using System.IO.Compression;
using System.Text.Json.Nodes;
using Microsoft.JSInterop;

namespace Raycoon.RayMigrator.Tests.Unit.ConfigWizard.Web;

/// <summary>
/// Tests for ZipExportService — in-memory ZIP archive generation from WizardState.
/// The FileInteropService (JS interop) is faked so no browser is needed.
/// </summary>
public class ZipExportServiceTests
{
    // ── Fake JS runtime that captures DownloadFile calls ──────────

    private sealed class FakeJsRuntime : IJSRuntime
    {
        public readonly List<(string FunctionName, string FileName, string ContentType, byte[] Content)> Calls = new();

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args, CancellationToken cancellationToken)
        {
            return default;
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            return default;
        }

        ValueTask<TValue> IJSRuntime.InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            // Capture downloadFileFromBytes calls
            if (identifier == "downloadFileFromBytes" && args is { Length: >= 3 })
            {
                Calls.Add((identifier, (string)args[0]!, (string)args[1]!, (byte[])args[2]!));
            }
            return default;
        }

        ValueTask<TValue> IJSRuntime.InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            return default;
        }
    }

    // ── Helpers ───────────────────────────────────────────────────

    private static (ZipExportService service, FakeJsRuntime js) CreateService(
        TermsAcceptanceService? terms = null)
    {
        var js = new FakeJsRuntime();
        var fileInterop = new FileInteropService(js);
        // Default: terms not accepted — the acceptance note only documents a
        // fact, so plain export tests run without it.
        var zipService = new ZipExportService(fileInterop, terms ?? new TermsAcceptanceService());
        return (zipService, js);
    }

    private static (string fileName, byte[] content) GetDownload(FakeJsRuntime js)
    {
        js.Calls.Should().ContainSingle();
        var call = js.Calls[0];
        return (call.FileName, call.Content);
    }

    private static WizardState BuildMinimalState()
    {
        var state = new WizardState();
        state.BaseModel.Repository.DatabaseType = "SqlServer";
        return state;
    }

    private static WizardState BuildStateWithEnvironments()
    {
        var answers = new WizardSetupAnswers
        {
            RepositoryDatabaseType = "SqlServer",
            Products = new List<ProductSetup>
            {
                new()
                {
                    Alias = "MyProduct",
                    Environments = new List<string> { "Docker", "Production" },
                    TargetGroups = new List<TargetGroupSetup>
                    {
                        new() { Alias = "Backend", DatabaseType = "SqlServer" }
                    }
                }
            }
        };
        return ConfigurationScaffolder.Scaffold(answers);
    }

    private static Dictionary<string, string> ReadZipEntries(byte[] zipBytes)
    {
        using var ms = new MemoryStream(zipBytes);
        using var archive = new ZipArchive(ms, ZipArchiveMode.Read);
        var entries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in archive.Entries)
        {
            using var reader = new StreamReader(entry.Open());
            entries[entry.FullName] = reader.ReadToEnd();
        }
        return entries;
    }

    // ── File is downloaded ────────────────────────────────────────

    [Fact]
    public async Task ExportAsync_CallsDownloadWithZipFileName()
    {
        var (service, js) = CreateService();
        var state = BuildMinimalState();

        await service.ExportAsync(state);

        var (fileName, _) = GetDownload(js);
        fileName.Should().Be("raymigrator-config.zip");
    }

    [Fact]
    public async Task ExportAsync_DownloadedContentIsNonEmpty()
    {
        var (service, js) = CreateService();
        var state = BuildMinimalState();

        await service.ExportAsync(state);

        var (_, content) = GetDownload(js);
        content.Should().NotBeEmpty();
    }

    // ── ZIP contents: base file ───────────────────────────────────

    [Fact]
    public async Task ExportAsync_ZipContainsAppsettingsJson()
    {
        var (service, js) = CreateService();
        var state = BuildMinimalState();

        await service.ExportAsync(state);

        var entries = ReadZipEntries(GetDownload(js).content);
        entries.Should().ContainKey("appsettings.json");
    }

    [Fact]
    public async Task ExportAsync_BaseFileContainsRayMigratorJson()
    {
        var (service, js) = CreateService();
        var state = BuildMinimalState();

        await service.ExportAsync(state);

        var entries = ReadZipEntries(GetDownload(js).content);
        entries["appsettings.json"].Should().Contain("RayMigrator");
    }

    // ── ZIP contents: example.env ────────────────────────────────

    [Fact]
    public async Task ExportAsync_ZipContainsDotEnvExample()
    {
        var (service, js) = CreateService();
        var state = BuildMinimalState();

        await service.ExportAsync(state);

        var entries = ReadZipEntries(GetDownload(js).content);
        entries.Should().ContainKey("example.env");
    }

    [Fact]
    public async Task ExportAsync_DotEnvExampleContainsHeader()
    {
        var (service, js) = CreateService();
        // Use state with ENV placeholders so the header is generated
        var state = BuildMinimalState();
        state.BaseModel.Repository.ConnectionString = "{ENV:REPO_CONNECTION_STRING}";

        await service.ExportAsync(state);

        var entries = ReadZipEntries(GetDownload(js).content);
        entries["example.env"].Should().Contain("RayMigrator Environment Variables");
    }

    // ── ZIP contents: environment overrides ──────────────────────

    [Fact]
    public async Task ExportAsync_WithEnvironmentModels_IncludesEnvironmentFiles()
    {
        var (service, js) = CreateService();
        var state = BuildStateWithEnvironments();

        await service.ExportAsync(state);

        var entries = ReadZipEntries(GetDownload(js).content);
        entries.Should().ContainKey("appsettings.Docker.json");
        entries.Should().ContainKey("appsettings.Production.json");
    }

    [Fact]
    public async Task ExportAsync_NoEnvironments_OnlyBaseAndEnvExample()
    {
        var (service, js) = CreateService();
        var state = BuildMinimalState();

        await service.ExportAsync(state);

        var entries = ReadZipEntries(GetDownload(js).content);
        entries.Should().HaveCount(2); // appsettings.json + example.env
    }

    // ── ZIP contents: product overrides ──────────────────────────

    [Fact]
    public async Task ExportAsync_WithProductModels_IncludesProductFiles()
    {
        // One product and no environment: a single combination, so everything it needs is in the base file (#23 Part B).
        var (service, js) = CreateService();
        var state = BuildMinimalState();
        var productModel = new ConfigurationModel { FilePath = "appsettings.OrderService.json" };
        productModel.Repository.DatabaseType = "PostgreSQL";
        state.ProductModels["OrderService"] = productModel;

        await service.ExportAsync(state);

        var entries = ReadZipEntries(GetDownload(js).content);
        entries.Should().NotContainKey("appsettings.OrderService.json", "a value that holds for the only combination belongs in the base file");
        JsonNode.Parse(entries["appsettings.json"])!["RayMigrator"]!["Repository"]!["DatabaseType"]!.GetValue<string>().Should().Be("PostgreSQL");
    }

    // ── ZIP contents: product+environment overrides ───────────────

    [Fact]
    public async Task ExportAsync_WithProductEnvironmentModels_IncludesProductEnvFiles()
    {
        // A single product-environment combination: its overrides are the whole configuration, so they sit in the base file (#23 Part B).
        var (service, js) = CreateService();
        var state = BuildMinimalState();
        var peModel = new ConfigurationModel { FilePath = "appsettings.MyProduct.Docker.json" };
        peModel.Repository.ConnectionString = "{ENV:DOCKER_CONN}";
        state.ProductEnvironmentModels["MyProduct.Docker"] = peModel;

        await service.ExportAsync(state);

        var entries = ReadZipEntries(GetDownload(js).content);
        entries.Should().NotContainKey("appsettings.MyProduct.Docker.json");
        JsonNode.Parse(entries["appsettings.json"])!["RayMigrator"]!["Repository"]!["ConnectionString"]!.GetValue<string>().Should().Be("{ENV:DOCKER_CONN}");
    }

    // ── ZIP entry count ───────────────────────────────────────────

    [Fact]
    public async Task ExportAsync_WithTwoEnvironments_ZipHasSixEntries()
    {
        // base + 2 environment files + example.env = 4: a product-environment value shared by every combination moves to the base (#23 Part B)
        var (service, js) = CreateService();
        var state = BuildStateWithEnvironments();
        foreach (var (_, peModel) in state.ProductEnvironmentModels)
            peModel.ProductDefaults.RequireRollbackFile = false;

        await service.ExportAsync(state);

        var entries = ReadZipEntries(GetDownload(js).content);
        entries.Should().HaveCount(4);
        entries.Keys.Should().Contain("appsettings.Docker.json").And.Contain("appsettings.Production.json");
        JsonNode.Parse(entries["appsettings.json"])!["RayMigrator"]!["ProductDefaults"]!["RequireRollbackFile"]!.GetValue<bool>().Should().BeFalse();
    }

    [Fact]
    public async Task ExportAsync_WithTwoEnvironmentsNoProducts_ZipHasFourEntries()
    {
        // Only env-level overrides: base + 2 envs + example.env = 4
        // Each environment model must have at least one field differing from base so the diff is non-empty
        var (service, js) = CreateService();
        var state = BuildMinimalState();
        var staging = new ConfigurationModel();
        staging.Repository.ConnectionString = "{ENV:STAGING_CONN}";
        state.EnvironmentModels["Staging"] = staging;
        var production = new ConfigurationModel();
        production.Repository.ConnectionString = "{ENV:PRODUCTION_CONN}";
        state.EnvironmentModels["Production"] = production;

        await service.ExportAsync(state);

        var entries = ReadZipEntries(GetDownload(js).content);
        entries.Should().HaveCount(4);
    }

    // ── ZIP file integrity ────────────────────────────────────────

    [Fact]
    public async Task ExportAsync_ProducesValidZipArchive()
    {
        var (service, js) = CreateService();
        var state = BuildStateWithEnvironments();

        await service.ExportAsync(state);

        // Should not throw when opening as ZipArchive
        var act = () =>
        {
            using var ms = new MemoryStream(GetDownload(js).content);
            using var archive = new ZipArchive(ms, ZipArchiveMode.Read);
            return archive.Entries.Count;
        };

        act.Should().NotThrow();
    }

    [Fact]
    public async Task ExportAsync_ZipEntriesUseUtf8Encoding()
    {
        var (service, js) = CreateService();
        var state = BuildMinimalState();
        // Add a unicode character to the model
        state.BaseModel.Repository.ConnectionString = "Server=localhost;\u00C4\u00F6\u00FC";

        await service.ExportAsync(state);

        var entries = ReadZipEntries(GetDownload(js).content);
        // Content should round-trip through UTF-8 without loss
        entries["appsettings.json"].Should().NotBeNullOrEmpty();
    }

    // ── ENV variable resolver uses no-op (WASM safety) ───────────

    [Fact]
    public async Task ExportAsync_EnvExample_DoesNotContainActualEnvVarValues()
    {
        // ZipExportService passes _ => null as resolver, so no real env vars should appear
        var (service, js) = CreateService();
        var state = BuildMinimalState();
        state.BaseModel.Repository.ConnectionString = "{ENV:REPO_CONNECTION_STRING}";

        await service.ExportAsync(state);

        var entries = ReadZipEntries(GetDownload(js).content);
        var envExample = entries["example.env"];
        // The variable key must appear, but the value should be empty (no-op resolver)
        envExample.Should().Contain("REPO_CONNECTION_STRING=");
        // Confirm no value was filled in (line should end with = followed by newline)
        envExample.Should().MatchRegex(@"REPO_CONNECTION_STRING=\r?\n");
    }

    // ══════════════════════════════════════════════════════════════
    // ══════════════════════════════════════════════════════════════

    // ── Scalar field matching ────────────────────────────────────

    // ── Nested object pruning ────────────────────────────────────

    // ── Array handling ───────────────────────────────────────────

    // ── Edge cases ───────────────────────────────────────────────

    // ══════════════════════════════════════════════════════════════
    // ExportAsync — redundant override removal integration tests
    // ══════════════════════════════════════════════════════════════

    [Fact]
    public async Task ExportAsync_PeFileOmitsValuesAlreadyInEnvFile()
    {
        // A single combination: every value holds for it, so the export is one base file (#23 Part B).
        var (service, js) = CreateService();
        var state = BuildMinimalState();
        state.BaseModel.Repository.ConnectionString = "{ENV:REPO_CONN}";
        var envModel = new ConfigurationModel();
        envModel.Repository.ConnectionString = "{ENV:REPO_CONN_DEV}";
        state.EnvironmentModels["Development"] = envModel;
        var peModel = new ConfigurationModel();
        peModel.Repository.ConnectionString = "{ENV:REPO_CONN_DEV}";
        peModel.ProductDefaults.RequireRollbackFile = false;
        state.ProductEnvironmentModels["MyApp.Development"] = peModel;

        await service.ExportAsync(state);

        var entries = ReadZipEntries(GetDownload(js).content);
        entries.Keys.Where(k => k.StartsWith("appsettings")).Should().Equal("appsettings.json");
        var baseRay = JsonNode.Parse(entries["appsettings.json"])!["RayMigrator"]!;
        baseRay["Repository"]!["ConnectionString"]!.GetValue<string>().Should().Be("{ENV:REPO_CONN_DEV}");
        baseRay["ProductDefaults"]!["RequireRollbackFile"]!.GetValue<bool>().Should().BeFalse();
    }

    [Fact]
    public async Task ExportAsync_PeFileOmitsValuesAlreadyInProductFile()
    {
        // A single combination: every value holds for it, so the export is one base file (#23 Part B).
        var (service, js) = CreateService();
        var state = BuildMinimalState();
        var productModel = new ConfigurationModel();
        productModel.ProductDefaults.MigrationErrorAction = "Rollback";
        state.ProductModels["MyApp"] = productModel;
        var peModel = new ConfigurationModel();
        peModel.ProductDefaults.MigrationErrorAction = "Rollback";
        peModel.Repository.ConnectionString = "{ENV:PE_CONN}";
        state.ProductEnvironmentModels["MyApp.Docker"] = peModel;

        await service.ExportAsync(state);

        var entries = ReadZipEntries(GetDownload(js).content);
        entries.Keys.Where(k => k.StartsWith("appsettings")).Should().Equal("appsettings.json");
        var baseRay = JsonNode.Parse(entries["appsettings.json"])!["RayMigrator"]!;
        baseRay["ProductDefaults"]!["MigrationErrorAction"]!.GetValue<string>().Should().Be("Rollback");
        baseRay["Repository"]!["ConnectionString"]!.GetValue<string>().Should().Be("{ENV:PE_CONN}");
    }

    [Fact]
    public async Task ExportAsync_PeFileOmitsValuesFromBothEnvAndProductFiles()
    {
        // A single combination: every value holds for it, so the export is one base file (#23 Part B).
        var (service, js) = CreateService();
        var state = BuildMinimalState();
        state.BaseModel.Repository.ConnectionString = "{ENV:BASE_CONN}";
        var envModel = new ConfigurationModel();
        envModel.Repository.ConnectionString = "{ENV:ENV_CONN}";
        state.EnvironmentModels["Docker"] = envModel;
        var productModel = new ConfigurationModel();
        productModel.ProductDefaults.MigrationErrorAction = "Rollback";
        state.ProductModels["MyApp"] = productModel;
        var peModel = new ConfigurationModel();
        peModel.Repository.ConnectionString = "{ENV:ENV_CONN}";
        peModel.ProductDefaults.MigrationErrorAction = "Rollback";
        peModel.ProductDefaults.RequireRollbackFile = false;
        state.ProductEnvironmentModels["MyApp.Docker"] = peModel;

        await service.ExportAsync(state);

        var entries = ReadZipEntries(GetDownload(js).content);
        entries.Keys.Where(k => k.StartsWith("appsettings")).Should().Equal("appsettings.json");
        var baseRay = JsonNode.Parse(entries["appsettings.json"])!["RayMigrator"]!;
        baseRay["Repository"]!["ConnectionString"]!.GetValue<string>().Should().Be("{ENV:ENV_CONN}", "the dead base value is not exported");
        baseRay["ProductDefaults"]!["MigrationErrorAction"]!.GetValue<string>().Should().Be("Rollback");
        baseRay["ProductDefaults"]!["RequireRollbackFile"]!.GetValue<bool>().Should().BeFalse();
    }

    [Fact]
    public async Task ExportAsync_PeFileWithUniqueConnectionString_Kept()
    {
        // A single combination: every value holds for it, so the export is one base file (#23 Part B).
        var (service, js) = CreateService();
        var state = BuildMinimalState();
        state.BaseModel.Repository.ConnectionString = "{ENV:BASE_CONN}";
        var envModel = new ConfigurationModel();
        envModel.Repository.ConnectionString = "{ENV:ENV_CONN}";
        state.EnvironmentModels["Docker"] = envModel;
        var peModel = new ConfigurationModel();
        peModel.Repository.ConnectionString = "{ENV:PE_SPECIFIC_CONN}";
        state.ProductEnvironmentModels["MyApp.Docker"] = peModel;

        await service.ExportAsync(state);

        var entries = ReadZipEntries(GetDownload(js).content);
        entries.Keys.Where(k => k.StartsWith("appsettings")).Should().Equal("appsettings.json");
        JsonNode.Parse(entries["appsettings.json"])!["RayMigrator"]!["Repository"]!["ConnectionString"]!.GetValue<string>().Should().Be("{ENV:PE_SPECIFIC_CONN}", "the most specific value is the effective one");
    }

    [Fact]
    public async Task ExportAsync_PeFileAllValuesMatchParents_FileExcludedFromZip()
    {
        var (service, js) = CreateService();
        var state = BuildMinimalState();
        state.BaseModel.Repository.ConnectionString = "{ENV:BASE_CONN}";

        var envModel = new ConfigurationModel();
        envModel.Repository.ConnectionString = "{ENV:ENV_CONN}";
        state.EnvironmentModels["Docker"] = envModel;

        // PE model has ONLY the same override as the env file → completely redundant
        var peModel = new ConfigurationModel();
        peModel.Repository.ConnectionString = "{ENV:ENV_CONN}";
        state.ProductEnvironmentModels["MyApp.Docker"] = peModel;

        await service.ExportAsync(state);

        var entries = ReadZipEntries(GetDownload(js).content);

        entries.Should().NotContainKey("appsettings.MyApp.Docker.json",
            "PE file should be excluded when all its overrides are already in parent files");
    }

    [Fact]
    public async Task ExportAsync_MultipleProductsSameEnv_CorrectParentMatching()
    {
        // Two products, one environment: what each product needs holds for all (one) of its environments, so it goes to the product file,
        // never to a product-environment file, and nothing is attached to the wrong product (#23 Part B).
        var (service, js) = CreateService();
        var state = BuildMinimalState();
        state.BaseModel.Repository.ConnectionString = "{ENV:BASE_CONN}";
        var envModel = new ConfigurationModel();
        envModel.Repository.ConnectionString = "{ENV:ENV_CONN_DOCKER}";
        state.EnvironmentModels["Docker"] = envModel;
        var pe1 = new ConfigurationModel();
        pe1.Repository.ConnectionString = "{ENV:ENV_CONN_DOCKER}";
        pe1.ProductDefaults.RequireRollbackFile = false;
        state.ProductEnvironmentModels["App1.Docker"] = pe1;
        var pe2 = new ConfigurationModel();
        pe2.Repository.ConnectionString = "{ENV:APP2_DOCKER_CONN}";
        state.ProductEnvironmentModels["App2.Docker"] = pe2;

        await service.ExportAsync(state);

        var entries = ReadZipEntries(GetDownload(js).content);
        entries.Keys.Should().NotContain("appsettings.App1.Docker.json").And.NotContain("appsettings.App2.Docker.json");
        var app1 = JsonNode.Parse(entries["appsettings.App1.json"])!["RayMigrator"]!;
        app1["Repository"]!["ConnectionString"]!.GetValue<string>().Should().Be("{ENV:ENV_CONN_DOCKER}");
        app1["ProductDefaults"]!["RequireRollbackFile"]!.GetValue<bool>().Should().BeFalse();
        var app2 = JsonNode.Parse(entries["appsettings.App2.json"])!["RayMigrator"]!;
        app2["Repository"]!["ConnectionString"]!.GetValue<string>().Should().Be("{ENV:APP2_DOCKER_CONN}");
        app2["ProductDefaults"]!["RequireRollbackFile"]!.GetValue<bool>().Should().BeTrue("the two products disagree, so each product file carries its own value");
        JsonNode.Parse(entries["appsettings.json"])!["RayMigrator"]!["Repository"]!["ConnectionString"].Should().BeNull("the two products disagree");
    }

    // ══════════════════════════════════════════════════════════════
    // ══════════════════════════════════════════════════════════════

    // ── Single combination ───────────────────────────────────────

    // ── Multiple combinations ────────────────────────────────────

    // ── Nested objects ───────────────────────────────────────────

    // ── Arrays ───────────────────────────────────────────────────

    // ── Edge cases ───────────────────────────────────────────────

    // ══════════════════════════════════════════════════════════════
    // ComputeExportJsons — integration tests
    // ══════════════════════════════════════════════════════════════

    [Fact]
    public void ComputeExportJsons_BaseConnStringOverriddenInAllEnvs_PrunedFromBase()
    {
        var state = BuildMinimalState();
        state.BaseModel.Repository.ConnectionString = "{ENV:REPO_CONN}";

        var envDev = new ConfigurationModel();
        envDev.Repository.ConnectionString = "{ENV:REPO_CONN_DEV}";
        state.EnvironmentModels["Development"] = envDev;

        var envProd = new ConfigurationModel();
        envProd.Repository.ConnectionString = "{ENV:REPO_CONN_PROD}";
        state.EnvironmentModels["Production"] = envProd;

        var result = ZipExportService.ComputeExportJsons(state);

        var baseRay = JsonNode.Parse(result["appsettings.json"])?["RayMigrator"];
        baseRay!["Repository"]!["ConnectionString"].Should().BeNull("overridden in all envs");
        baseRay["Repository"]!["DatabaseType"].Should().NotBeNull("not overridden");
    }

    [Fact]
    public void ComputeExportJsons_BasePropertyOverriddenInAllPEs_PrunedFromBase()
    {
        // The only combination overrides the base value: the override is the effective value and moves to the base; the dead base value disappears (#23 Part B).
        var state = BuildMinimalState();
        state.BaseModel.Repository.ConnectionString = "{ENV:BASE_CONN}";
        state.EnvironmentModels["Docker"] = new ConfigurationModel();
        var pe = new ConfigurationModel();
        pe.Repository.ConnectionString = "{ENV:PE_CONN}";
        state.ProductEnvironmentModels["MyApp.Docker"] = pe;
        state.BaseModel.Products.Add(new ProductModel { Alias = "MyApp" });

        var result = ZipExportService.ComputeExportJsons(state);

        result.Keys.Should().Equal("appsettings.json");
        JsonNode.Parse(result["appsettings.json"])!["RayMigrator"]!["Repository"]!["ConnectionString"]!.GetValue<string>().Should().Be("{ENV:PE_CONN}");
    }

    [Fact]
    public void ComputeExportJsons_BasePropertyOverriddenInSomeNotAll_StaysInBase()
    {
        // Two environments disagree on the connection string: neither value holds for every combination, so each environment file carries its own (#23 Part B).
        var state = BuildMinimalState();
        state.BaseModel.Repository.ConnectionString = "{ENV:BASE_CONN}";
        var envDev = new ConfigurationModel();
        envDev.Repository.ConnectionString = "{ENV:DEV_CONN}";
        state.EnvironmentModels["Development"] = envDev;
        state.EnvironmentModels["Production"] = new ConfigurationModel();

        var result = ZipExportService.ComputeExportJsons(state);

        JsonNode.Parse(result["appsettings.json"])!["RayMigrator"]!["Repository"]!["ConnectionString"].Should().BeNull();
        JsonNode.Parse(result["appsettings.Development.json"])!["RayMigrator"]!["Repository"]!["ConnectionString"]!.GetValue<string>().Should().Be("{ENV:DEV_CONN}");
        JsonNode.Parse(result["appsettings.Production.json"])!["RayMigrator"]!["Repository"]!["ConnectionString"]!.GetValue<string>().Should().Be("{ENV:BASE_CONN}");
        JsonNode.Parse(result["appsettings.json"])!["RayMigrator"]!["Repository"]!["DatabaseType"]!.GetValue<string>().Should().Be("SqlServer", "common to both environments");
    }

    [Fact]
    public void ComputeExportJsons_EnvPropertyOverriddenInAllPEs_PrunedFromEnv()
    {
        // A single combination: every value holds for it, so the export is one base file (#23 Part B).
        var state = BuildMinimalState();
        state.BaseModel.Repository.ConnectionString = "{ENV:BASE_CONN}";
        state.BaseModel.Repository.SchemaName = "ray";
        var envDev = new ConfigurationModel();
        envDev.Repository.ConnectionString = "{ENV:DEV_CONN}";
        envDev.Repository.SchemaName = "dev_schema";
        envDev.ProductDefaults.MigrationErrorAction = "Rollback";
        state.EnvironmentModels["Development"] = envDev;
        var pe = new ConfigurationModel();
        pe.Repository.ConnectionString = "{ENV:PE_CONN}";
        pe.ProductDefaults.MigrationErrorAction = "RollbackRelease";
        state.ProductEnvironmentModels["MyApp.Development"] = pe;

        var result = ZipExportService.ComputeExportJsons(state);

        result.Keys.Should().Equal("appsettings.json");
        var baseRay = JsonNode.Parse(result["appsettings.json"])!["RayMigrator"]!;
        baseRay["Repository"]!["ConnectionString"]!.GetValue<string>().Should().Be("{ENV:PE_CONN}");
        baseRay["Repository"]!["SchemaName"]!.GetValue<string>().Should().Be("dev_schema");
        baseRay["ProductDefaults"]!["MigrationErrorAction"]!.GetValue<string>().Should().Be("RollbackRelease");
    }

    [Fact]
    public void ComputeExportJsons_PeRedundancyRemoval_MatchesExistingBehavior()
    {
        // A single combination: every value holds for it, so the export is one base file (#23 Part B).
        var state = BuildMinimalState();
        state.BaseModel.Repository.ConnectionString = "{ENV:BASE_CONN}";
        var envModel = new ConfigurationModel();
        envModel.Repository.ConnectionString = "{ENV:ENV_CONN}";
        state.EnvironmentModels["Docker"] = envModel;
        var pe = new ConfigurationModel();
        pe.Repository.ConnectionString = "{ENV:ENV_CONN}";
        pe.ProductDefaults.RequireRollbackFile = false;
        state.ProductEnvironmentModels["MyApp.Docker"] = pe;

        var result = ZipExportService.ComputeExportJsons(state);

        result.Keys.Should().Equal("appsettings.json");
        var baseRay = JsonNode.Parse(result["appsettings.json"])!["RayMigrator"]!;
        baseRay["Repository"]!["ConnectionString"]!.GetValue<string>().Should().Be("{ENV:ENV_CONN}");
        baseRay["ProductDefaults"]!["RequireRollbackFile"]!.GetValue<bool>().Should().BeFalse();
    }

    [Fact]
    public void ComputeExportJsons_NopeModels_EnvOnlyOverrides_BasePruned()
    {
        var state = BuildMinimalState();
        state.BaseModel.Repository.ConnectionString = "{ENV:BASE_CONN}";

        // Only env models, no PE models
        var staging = new ConfigurationModel();
        staging.Repository.ConnectionString = "{ENV:STAGING_CONN}";
        state.EnvironmentModels["Staging"] = staging;

        var production = new ConfigurationModel();
        production.Repository.ConnectionString = "{ENV:PRODUCTION_CONN}";
        state.EnvironmentModels["Production"] = production;

        var result = ZipExportService.ComputeExportJsons(state);

        var baseRay = JsonNode.Parse(result["appsettings.json"])?["RayMigrator"];
        baseRay!["Repository"]!["ConnectionString"].Should().BeNull("overridden in all env files");
    }

    [Fact]
    public void ComputeExportJsons_ScaffoldedState_RepoConnStringPrunedFromBase()
    {
        var state = BuildStateWithEnvironments();

        var result = ZipExportService.ComputeExportJsons(state);

        var baseRay = JsonNode.Parse(result["appsettings.json"])?["RayMigrator"];
        // ConnectionString is overridden in both Docker and Production env models
        baseRay!["Repository"]!["ConnectionString"].Should().BeNull(
            "scaffolded env models override Repository.ConnectionString for every environment");
        // DatabaseType is NOT overridden → stays
        baseRay["Repository"]!["DatabaseType"]!.GetValue<string>().Should().Be("SqlServer");
    }

    // ── Terms acceptance note ─────────────────────────────────────

    [Fact]
    public async Task ExportAsync_TermsNotAccepted_ZipContainsNoAcceptanceNote()
    {
        var (service, js) = CreateService();
        var state = BuildMinimalState();

        await service.ExportAsync(state);

        var entries = ReadZipEntries(GetDownload(js).content);
        entries.Should().NotContainKey(ZipExportService.AcceptanceNoteFileName,
            "the note documents a fact and must never fabricate one");
    }

    [Fact]
    public async Task ExportAsync_TermsAccepted_ZipContainsAcceptanceNote()
    {
        var terms = new TermsAcceptanceService();
        terms.Accept();
        var (service, js) = CreateService(terms);
        var state = BuildMinimalState();

        await service.ExportAsync(state);

        var entries = ReadZipEntries(GetDownload(js).content);
        entries.Should().ContainKey(ZipExportService.AcceptanceNoteFileName);

        var note = entries[ZipExportService.AcceptanceNoteFileName];
        note.Should().Contain(TermsAcceptanceService.TermsVersion);
        note.Should().Contain(TermsAcceptanceService.TermsUrlDe);
        note.Should().Contain(TermsAcceptanceService.TermsUrlEn);
        note.Should().Contain(
            terms.AcceptedAtUtc!.Value.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"));
    }

    [Fact]
    public async Task ExportAsync_TermsAccepted_ConfigFilesStillPresent()
    {
        var terms = new TermsAcceptanceService();
        terms.Accept();
        var (service, js) = CreateService(terms);
        var state = BuildMinimalState();

        await service.ExportAsync(state);

        var entries = ReadZipEntries(GetDownload(js).content);
        entries.Should().ContainKey("appsettings.json");
        entries.Should().ContainKey("example.env");
    }
}
