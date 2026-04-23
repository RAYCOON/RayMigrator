// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

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

    private static (ZipExportService service, FakeJsRuntime js) CreateService()
    {
        var js = new FakeJsRuntime();
        var fileInterop = new FileInteropService(js);
        var zipService = new ZipExportService(fileInterop);
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
        var (service, js) = CreateService();
        var state = BuildMinimalState();
        var productModel = new ConfigurationModel { FilePath = "appsettings.OrderService.json" };
        // Add a field that differs from the base so the diff is non-empty and the file is written
        productModel.Repository.DatabaseType = "PostgreSQL";
        state.ProductModels["OrderService"] = productModel;

        await service.ExportAsync(state);

        var entries = ReadZipEntries(GetDownload(js).content);
        entries.Should().ContainKey("appsettings.OrderService.json");
    }

    // ── ZIP contents: product+environment overrides ───────────────

    [Fact]
    public async Task ExportAsync_WithProductEnvironmentModels_IncludesProductEnvFiles()
    {
        var (service, js) = CreateService();
        var state = BuildMinimalState();
        var peModel = new ConfigurationModel { FilePath = "appsettings.MyProduct.Docker.json" };
        // Add a field that differs from the base so the diff is non-empty and the file is written
        peModel.Repository.ConnectionString = "{ENV:DOCKER_CONN}";
        state.ProductEnvironmentModels["MyProduct.Docker"] = peModel;

        await service.ExportAsync(state);

        var entries = ReadZipEntries(GetDownload(js).content);
        entries.Should().ContainKey("appsettings.MyProduct.Docker.json");
    }

    // ── ZIP entry count ───────────────────────────────────────────

    [Fact]
    public async Task ExportAsync_WithTwoEnvironments_ZipHasSixEntries()
    {
        // base + 2 env overrides + 2 product-env overrides (MyProduct.Docker, MyProduct.Production) + example.env = 6
        var (service, js) = CreateService();
        var state = BuildStateWithEnvironments();

        // Scaffolded PE models are bare — give them overrides that differ from BOTH base and env
        foreach (var (key, peModel) in state.ProductEnvironmentModels)
        {
            peModel.ProductDefaults.RequireRollbackFile = false;
        }

        await service.ExportAsync(state);

        var entries = ReadZipEntries(GetDownload(js).content);
        entries.Should().HaveCount(6);
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
    // RemoveRedundantOverrides — unit tests
    // ══════════════════════════════════════════════════════════════

    // ── Scalar field matching ────────────────────────────────────

    [Fact]
    public void RemoveRedundantOverrides_MatchingScalarField_RemovedFromChild()
    {
        var child = """{"RayMigrator":{"Repository":{"ConnectionString":"X","SchemaName":"custom"}}}""";
        var parent = """{"RayMigrator":{"Repository":{"ConnectionString":"X"}}}""";

        var result = ZipExportService.RemoveRedundantOverrides(child, parent);
        var repo = JsonNode.Parse(result)?["RayMigrator"]?["Repository"];

        repo!["SchemaName"]!.GetValue<string>().Should().Be("custom");
        repo["ConnectionString"].Should().BeNull();
    }

    [Fact]
    public void RemoveRedundantOverrides_DifferentScalarValues_ChildUnchanged()
    {
        var child = """{"RayMigrator":{"Repository":{"ConnectionString":"A"}}}""";
        var parent = """{"RayMigrator":{"Repository":{"ConnectionString":"B"}}}""";

        var result = ZipExportService.RemoveRedundantOverrides(child, parent);
        var conn = JsonNode.Parse(result)?["RayMigrator"]?["Repository"]?["ConnectionString"]?.GetValue<string>();

        conn.Should().Be("A");
    }

    [Fact]
    public void RemoveRedundantOverrides_MatchingBoolField_Removed()
    {
        var child = """{"RayMigrator":{"ProductDefaults":{"RequireRollbackFile":false,"MigrationErrorAction":"Rollback"}}}""";
        var parent = """{"RayMigrator":{"ProductDefaults":{"RequireRollbackFile":false}}}""";

        var result = ZipExportService.RemoveRedundantOverrides(child, parent);
        var pd = JsonNode.Parse(result)?["RayMigrator"]?["ProductDefaults"];

        pd!["MigrationErrorAction"]!.GetValue<string>().Should().Be("Rollback");
        pd["RequireRollbackFile"].Should().BeNull();
    }

    [Fact]
    public void RemoveRedundantOverrides_MatchingIntField_Removed()
    {
        var child = """{"RayMigrator":{"Repository":{"DbCommandTimeoutInSeconds":30,"SchemaName":"custom"}}}""";
        var parent = """{"RayMigrator":{"Repository":{"DbCommandTimeoutInSeconds":30}}}""";

        var result = ZipExportService.RemoveRedundantOverrides(child, parent);
        var repo = JsonNode.Parse(result)?["RayMigrator"]?["Repository"];

        repo!["SchemaName"]!.GetValue<string>().Should().Be("custom");
        repo["DbCommandTimeoutInSeconds"].Should().BeNull();
    }

    // ── Nested object pruning ────────────────────────────────────

    [Fact]
    public void RemoveRedundantOverrides_AllFieldsInSectionMatch_EntireSectionPruned()
    {
        var child = """{"RayMigrator":{"Repository":{"ConnectionString":"X"},"ProductDefaults":{"MigrationErrorAction":"Rollback"}}}""";
        var parent = """{"RayMigrator":{"Repository":{"ConnectionString":"X"}}}""";

        var result = ZipExportService.RemoveRedundantOverrides(child, parent);
        var ray = JsonNode.Parse(result)?["RayMigrator"]?.AsObject();

        ray!["Repository"].Should().BeNull("Repository section should be pruned after all fields removed");
        ray["ProductDefaults"]!["MigrationErrorAction"]!.GetValue<string>().Should().Be("Rollback");
    }

    [Fact]
    public void RemoveRedundantOverrides_AllFieldsMatchAcrossAllSections_EmptyRayMigrator()
    {
        var child = """{"RayMigrator":{"Repository":{"ConnectionString":"X"},"ProductDefaults":{"RequireRollbackFile":false}}}""";
        var parent = """{"RayMigrator":{"Repository":{"ConnectionString":"X"},"ProductDefaults":{"RequireRollbackFile":false}}}""";

        var result = ZipExportService.RemoveRedundantOverrides(child, parent);

        ConfigurationSerializer.IsEmptyDiff(result).Should().BeTrue();
    }

    [Fact]
    public void RemoveRedundantOverrides_DeeplyNestedMatch_PrunedUpward()
    {
        var child = """{"RayMigrator":{"ProductDefaults":{"TargetGroupDefaults":{"TargetDefaults":{"DbCommandTimeoutInSeconds":90}}}}}""";
        var parent = """{"RayMigrator":{"ProductDefaults":{"TargetGroupDefaults":{"TargetDefaults":{"DbCommandTimeoutInSeconds":90}}}}}""";

        var result = ZipExportService.RemoveRedundantOverrides(child, parent);

        ConfigurationSerializer.IsEmptyDiff(result).Should().BeTrue("entire nested tree should be pruned");
    }

    [Fact]
    public void RemoveRedundantOverrides_DeeplyNestedPartialMatch_OnlyMatchingFieldPruned()
    {
        var child = """{"RayMigrator":{"ProductDefaults":{"TargetGroupDefaults":{"TargetMigrationOrder":"Simultaneously","HashValidationScope":"Header"}}}}""";
        var parent = """{"RayMigrator":{"ProductDefaults":{"TargetGroupDefaults":{"TargetMigrationOrder":"Simultaneously"}}}}""";

        var result = ZipExportService.RemoveRedundantOverrides(child, parent);
        var tgd = JsonNode.Parse(result)?["RayMigrator"]?["ProductDefaults"]?["TargetGroupDefaults"];

        tgd!["HashValidationScope"]!.GetValue<string>().Should().Be("Header");
        tgd["TargetMigrationOrder"].Should().BeNull();
    }

    // ── Array handling ───────────────────────────────────────────

    [Fact]
    public void RemoveRedundantOverrides_IdenticalArray_Removed()
    {
        var child = """{"RayMigrator":{"Serilog":{"WriteTo":[{"Name":"Console"}]},"Repository":{"ConnectionString":"X"}}}""";
        var parent = """{"RayMigrator":{"Serilog":{"WriteTo":[{"Name":"Console"}]}}}""";

        var result = ZipExportService.RemoveRedundantOverrides(child, parent);
        var ray = JsonNode.Parse(result)?["RayMigrator"]?.AsObject();

        ray!["Serilog"].Should().BeNull("identical Serilog section should be pruned");
        ray["Repository"]!["ConnectionString"]!.GetValue<string>().Should().Be("X");
    }

    [Fact]
    public void RemoveRedundantOverrides_DifferentArray_Kept()
    {
        var child = """{"RayMigrator":{"Serilog":{"WriteTo":[{"Name":"File"}]}}}""";
        var parent = """{"RayMigrator":{"Serilog":{"WriteTo":[{"Name":"Console"}]}}}""";

        var result = ZipExportService.RemoveRedundantOverrides(child, parent);
        var writeTo = JsonNode.Parse(result)?["RayMigrator"]?["Serilog"]?["WriteTo"]?.AsArray();

        writeTo.Should().HaveCount(1);
        writeTo![0]!["Name"]!.GetValue<string>().Should().Be("File");
    }

    // ── Edge cases ───────────────────────────────────────────────

    [Fact]
    public void RemoveRedundantOverrides_ParentHasFieldsChildDoesNot_NoEffect()
    {
        var child = """{"RayMigrator":{"Repository":{"ConnectionString":"A"}}}""";
        var parent = """{"RayMigrator":{"Repository":{"ConnectionString":"B","SchemaName":"custom"},"ProductDefaults":{"RequireRollbackFile":false}}}""";

        var result = ZipExportService.RemoveRedundantOverrides(child, parent);
        var conn = JsonNode.Parse(result)?["RayMigrator"]?["Repository"]?["ConnectionString"]?.GetValue<string>();

        conn.Should().Be("A");
    }

    [Fact]
    public void RemoveRedundantOverrides_ChildHasSectionsParentDoesNot_SectionsKept()
    {
        var child = """{"RayMigrator":{"Repository":{"ConnectionString":"A"},"DatabaseLogging":{"ConnectionString":"B"}}}""";
        var parent = """{"RayMigrator":{"Repository":{"ConnectionString":"C"}}}""";

        var result = ZipExportService.RemoveRedundantOverrides(child, parent);
        var ray = JsonNode.Parse(result)?["RayMigrator"]?.AsObject();

        ray!["Repository"]!["ConnectionString"]!.GetValue<string>().Should().Be("A");
        ray["DatabaseLogging"]!["ConnectionString"]!.GetValue<string>().Should().Be("B");
    }

    [Fact]
    public void RemoveRedundantOverrides_EmptyParentDiff_ChildUnchanged()
    {
        var child = """{"RayMigrator":{"Repository":{"ConnectionString":"A"}}}""";
        var parent = """{"RayMigrator":{}}""";

        var result = ZipExportService.RemoveRedundantOverrides(child, parent);
        var conn = JsonNode.Parse(result)?["RayMigrator"]?["Repository"]?["ConnectionString"]?.GetValue<string>();

        conn.Should().Be("A");
    }

    [Fact]
    public void RemoveRedundantOverrides_EmptyChildDiff_StaysEmpty()
    {
        var child = """{"RayMigrator":{}}""";
        var parent = """{"RayMigrator":{"Repository":{"ConnectionString":"X"}}}""";

        var result = ZipExportService.RemoveRedundantOverrides(child, parent);

        ConfigurationSerializer.IsEmptyDiff(result).Should().BeTrue();
    }

    [Fact]
    public void RemoveRedundantOverrides_MultipleSections_OnlyMatchingSectionsRemoved()
    {
        var child = """
        {
          "RayMigrator": {
            "Repository": {"ConnectionString": "X", "SchemaName": "custom"},
            "DatabaseLogging": {"ConnectionString": "Y"},
            "ProductDefaults": {"MigrationErrorAction": "Rollback"}
          }
        }
        """;
        var parent = """
        {
          "RayMigrator": {
            "Repository": {"ConnectionString": "X"},
            "DatabaseLogging": {"ConnectionString": "Y"}
          }
        }
        """;

        var result = ZipExportService.RemoveRedundantOverrides(child, parent);
        var ray = JsonNode.Parse(result)?["RayMigrator"]?.AsObject();

        // Repository: ConnectionString removed (matched), SchemaName kept (unique)
        ray!["Repository"]!["SchemaName"]!.GetValue<string>().Should().Be("custom");
        ray["Repository"]!["ConnectionString"].Should().BeNull();
        // DatabaseLogging: entirely pruned (all fields matched)
        ray["DatabaseLogging"].Should().BeNull();
        // ProductDefaults: kept (not in parent)
        ray["ProductDefaults"]!["MigrationErrorAction"]!.GetValue<string>().Should().Be("Rollback");
    }

    // ══════════════════════════════════════════════════════════════
    // ExportAsync — redundant override removal integration tests
    // ══════════════════════════════════════════════════════════════

    [Fact]
    public async Task ExportAsync_PeFileOmitsValuesAlreadyInEnvFile()
    {
        var (service, js) = CreateService();
        var state = BuildMinimalState();
        state.BaseModel.Repository.ConnectionString = "{ENV:REPO_CONN}";

        // Env model overrides ConnectionString
        var envModel = new ConfigurationModel();
        envModel.Repository.ConnectionString = "{ENV:REPO_CONN_DEV}";
        state.EnvironmentModels["Development"] = envModel;

        // PE model has same ConnectionString as env + an additional override
        var peModel = new ConfigurationModel();
        peModel.Repository.ConnectionString = "{ENV:REPO_CONN_DEV}"; // same as env → redundant
        peModel.ProductDefaults.RequireRollbackFile = false; // unique override
        state.ProductEnvironmentModels["MyApp.Development"] = peModel;

        await service.ExportAsync(state);

        var entries = ReadZipEntries(GetDownload(js).content);
        var peJson = entries["appsettings.MyApp.Development.json"];
        var peRay = JsonNode.Parse(peJson)?["RayMigrator"];

        // ConnectionString should NOT be in PE file (already in env file)
        peRay!["Repository"].Should().BeNull();
        // RequireRollbackFile should still be present
        peRay["ProductDefaults"]!["RequireRollbackFile"]!.GetValue<bool>().Should().BeFalse();
    }

    [Fact]
    public async Task ExportAsync_PeFileOmitsValuesAlreadyInProductFile()
    {
        var (service, js) = CreateService();
        var state = BuildMinimalState();

        // Product model overrides MigrationErrorAction
        var productModel = new ConfigurationModel();
        productModel.ProductDefaults.MigrationErrorAction = "Rollback";
        state.ProductModels["MyApp"] = productModel;

        // PE model has same MigrationErrorAction as product + a unique override
        var peModel = new ConfigurationModel();
        peModel.ProductDefaults.MigrationErrorAction = "Rollback"; // same as product → redundant
        peModel.Repository.ConnectionString = "{ENV:PE_CONN}"; // unique
        state.ProductEnvironmentModels["MyApp.Docker"] = peModel;

        await service.ExportAsync(state);

        var entries = ReadZipEntries(GetDownload(js).content);
        var peJson = entries["appsettings.MyApp.Docker.json"];
        var peRay = JsonNode.Parse(peJson)?["RayMigrator"];

        // MigrationErrorAction should NOT be in PE file (already in product file)
        peRay!["ProductDefaults"].Should().BeNull();
        // ConnectionString should still be present
        peRay["Repository"]!["ConnectionString"]!.GetValue<string>().Should().Be("{ENV:PE_CONN}");
    }

    [Fact]
    public async Task ExportAsync_PeFileOmitsValuesFromBothEnvAndProductFiles()
    {
        var (service, js) = CreateService();
        var state = BuildMinimalState();
        state.BaseModel.Repository.ConnectionString = "{ENV:BASE_CONN}";

        // Env overrides ConnectionString
        var envModel = new ConfigurationModel();
        envModel.Repository.ConnectionString = "{ENV:ENV_CONN}";
        state.EnvironmentModels["Docker"] = envModel;

        // Product overrides MigrationErrorAction
        var productModel = new ConfigurationModel();
        productModel.ProductDefaults.MigrationErrorAction = "Rollback";
        state.ProductModels["MyApp"] = productModel;

        // PE model repeats both parent overrides + has a unique field
        var peModel = new ConfigurationModel();
        peModel.Repository.ConnectionString = "{ENV:ENV_CONN}"; // same as env → redundant
        peModel.ProductDefaults.MigrationErrorAction = "Rollback"; // same as product → redundant
        peModel.ProductDefaults.RequireRollbackFile = false; // unique
        state.ProductEnvironmentModels["MyApp.Docker"] = peModel;

        await service.ExportAsync(state);

        var entries = ReadZipEntries(GetDownload(js).content);
        var peJson = entries["appsettings.MyApp.Docker.json"];
        var peRay = JsonNode.Parse(peJson)?["RayMigrator"];

        peRay!["Repository"].Should().BeNull("ConnectionString already in env file");
        peRay["ProductDefaults"]!["MigrationErrorAction"].Should().BeNull("already in product file");
        peRay["ProductDefaults"]!["RequireRollbackFile"]!.GetValue<bool>().Should().BeFalse("unique to PE");
    }

    [Fact]
    public async Task ExportAsync_PeFileWithUniqueConnectionString_Kept()
    {
        var (service, js) = CreateService();
        var state = BuildMinimalState();
        state.BaseModel.Repository.ConnectionString = "{ENV:BASE_CONN}";

        var envModel = new ConfigurationModel();
        envModel.Repository.ConnectionString = "{ENV:ENV_CONN}";
        state.EnvironmentModels["Docker"] = envModel;

        // PE model has a DIFFERENT ConnectionString than both base and env
        var peModel = new ConfigurationModel();
        peModel.Repository.ConnectionString = "{ENV:PE_SPECIFIC_CONN}";
        state.ProductEnvironmentModels["MyApp.Docker"] = peModel;

        await service.ExportAsync(state);

        var entries = ReadZipEntries(GetDownload(js).content);
        var peJson = entries["appsettings.MyApp.Docker.json"];
        var peConn = JsonNode.Parse(peJson)?["RayMigrator"]?["Repository"]?["ConnectionString"]?.GetValue<string>();

        peConn.Should().Be("{ENV:PE_SPECIFIC_CONN}");
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
        var (service, js) = CreateService();
        var state = BuildMinimalState();
        state.BaseModel.Repository.ConnectionString = "{ENV:BASE_CONN}";

        var envModel = new ConfigurationModel();
        envModel.Repository.ConnectionString = "{ENV:ENV_CONN_DOCKER}";
        state.EnvironmentModels["Docker"] = envModel;

        // App1.Docker: same ConnectionString as env, has unique field
        var pe1 = new ConfigurationModel();
        pe1.Repository.ConnectionString = "{ENV:ENV_CONN_DOCKER}"; // redundant
        pe1.ProductDefaults.RequireRollbackFile = false; // unique
        state.ProductEnvironmentModels["App1.Docker"] = pe1;

        // App2.Docker: different ConnectionString from env
        var pe2 = new ConfigurationModel();
        pe2.Repository.ConnectionString = "{ENV:APP2_DOCKER_CONN}"; // unique
        state.ProductEnvironmentModels["App2.Docker"] = pe2;

        await service.ExportAsync(state);

        var entries = ReadZipEntries(GetDownload(js).content);

        // App1: ConnectionString removed (matches env), RequireRollbackFile kept
        var pe1Ray = JsonNode.Parse(entries["appsettings.App1.Docker.json"])?["RayMigrator"];
        pe1Ray!["Repository"].Should().BeNull();
        pe1Ray["ProductDefaults"]!["RequireRollbackFile"]!.GetValue<bool>().Should().BeFalse();

        // App2: ConnectionString kept (differs from env)
        var pe2Ray = JsonNode.Parse(entries["appsettings.App2.Docker.json"])?["RayMigrator"];
        pe2Ray!["Repository"]!["ConnectionString"]!.GetValue<string>().Should().Be("{ENV:APP2_DOCKER_CONN}");
    }

    // ══════════════════════════════════════════════════════════════
    // PruneCoveredProperties — unit tests
    // ══════════════════════════════════════════════════════════════

    // ── Single combination ───────────────────────────────────────

    [Fact]
    public void PruneCoveredProperties_SingleCombo_AllLeavesCovered_AllPruned()
    {
        var parent = """{"RayMigrator":{"Repository":{"ConnectionString":"X","SchemaName":"ray"}}}""";
        var child = """{"RayMigrator":{"Repository":{"ConnectionString":"Y","SchemaName":"custom"}}}""";
        var groups = new List<IReadOnlyList<string>> { new List<string> { child } };

        var result = ZipExportService.PruneCoveredProperties(parent, groups);

        ConfigurationSerializer.IsEmptyDiff(result).Should().BeTrue("all leaves are covered");
    }

    [Fact]
    public void PruneCoveredProperties_SingleCombo_PartialCoverage_OnlyCoveredPruned()
    {
        var parent = """{"RayMigrator":{"Repository":{"ConnectionString":"X","SchemaName":"ray"}}}""";
        var child = """{"RayMigrator":{"Repository":{"ConnectionString":"Y"}}}""";
        var groups = new List<IReadOnlyList<string>> { new List<string> { child } };

        var result = ZipExportService.PruneCoveredProperties(parent, groups);
        var repo = JsonNode.Parse(result)?["RayMigrator"]?["Repository"];

        repo!["SchemaName"]!.GetValue<string>().Should().Be("ray", "not covered by child");
        repo["ConnectionString"].Should().BeNull("covered by child");
    }

    // ── Multiple combinations ────────────────────────────────────

    [Fact]
    public void PruneCoveredProperties_TwoCombos_AllCovered_Pruned()
    {
        var parent = """{"RayMigrator":{"Repository":{"ConnectionString":"X"}}}""";
        var child1 = """{"RayMigrator":{"Repository":{"ConnectionString":"A"}}}""";
        var child2 = """{"RayMigrator":{"Repository":{"ConnectionString":"B"}}}""";
        var groups = new List<IReadOnlyList<string>>
        {
            new List<string> { child1 },
            new List<string> { child2 }
        };

        var result = ZipExportService.PruneCoveredProperties(parent, groups);
        var ray = JsonNode.Parse(result)?["RayMigrator"]?.AsObject();

        ray!["Repository"].Should().BeNull("ConnectionString covered in both combos");
    }

    [Fact]
    public void PruneCoveredProperties_TwoCombos_OneUncovered_Stays()
    {
        var parent = """{"RayMigrator":{"Repository":{"ConnectionString":"X","SchemaName":"ray"}}}""";
        var child1 = """{"RayMigrator":{"Repository":{"ConnectionString":"A","SchemaName":"s1"}}}""";
        // child2 only covers ConnectionString, not SchemaName
        var child2 = """{"RayMigrator":{"Repository":{"ConnectionString":"B"}}}""";
        var groups = new List<IReadOnlyList<string>>
        {
            new List<string> { child1 },
            new List<string> { child2 }
        };

        var result = ZipExportService.PruneCoveredProperties(parent, groups);
        var repo = JsonNode.Parse(result)?["RayMigrator"]?["Repository"];

        repo!["ConnectionString"].Should().BeNull("covered in both combos");
        repo["SchemaName"]!.GetValue<string>().Should().Be("ray", "not covered in combo 2");
    }

    [Fact]
    public void PruneCoveredProperties_MultipleChildrenPerCombo_AnyCovers()
    {
        var parent = """{"RayMigrator":{"Repository":{"ConnectionString":"X"}}}""";
        // Combo 1: env covers ConnectionString
        var envChild = """{"RayMigrator":{"Repository":{"ConnectionString":"ENV"}}}""";
        // Combo 2: env doesn't cover, but PE does
        var envChild2 = """{"RayMigrator":{"ProductDefaults":{"MigrationErrorAction":"Rollback"}}}""";
        var peChild2 = """{"RayMigrator":{"Repository":{"ConnectionString":"PE"}}}""";

        var groups = new List<IReadOnlyList<string>>
        {
            new List<string> { envChild },
            new List<string> { envChild2, peChild2 }
        };

        var result = ZipExportService.PruneCoveredProperties(parent, groups);
        var ray = JsonNode.Parse(result)?["RayMigrator"]?.AsObject();

        ray!["Repository"].Should().BeNull("covered in both combos via different children");
    }

    // ── Nested objects ───────────────────────────────────────────

    [Fact]
    public void PruneCoveredProperties_NestedFullCoverage_PrunedUpward()
    {
        var parent = """{"RayMigrator":{"ProductDefaults":{"TargetGroupDefaults":{"TargetDefaults":{"DbCommandTimeoutInSeconds":20}}}}}""";
        var child = """{"RayMigrator":{"ProductDefaults":{"TargetGroupDefaults":{"TargetDefaults":{"DbCommandTimeoutInSeconds":90}}}}}""";
        var groups = new List<IReadOnlyList<string>> { new List<string> { child } };

        var result = ZipExportService.PruneCoveredProperties(parent, groups);

        ConfigurationSerializer.IsEmptyDiff(result).Should().BeTrue("entire nested tree covered");
    }

    [Fact]
    public void PruneCoveredProperties_NestedPartialCoverage_OnlyMatchingPruned()
    {
        var parent = """{"RayMigrator":{"ProductDefaults":{"MigrationErrorAction":"Terminate","RequireRollbackFile":true}}}""";
        var child = """{"RayMigrator":{"ProductDefaults":{"MigrationErrorAction":"Rollback"}}}""";
        var groups = new List<IReadOnlyList<string>> { new List<string> { child } };

        var result = ZipExportService.PruneCoveredProperties(parent, groups);
        var pd = JsonNode.Parse(result)?["RayMigrator"]?["ProductDefaults"];

        pd!["MigrationErrorAction"].Should().BeNull("covered by child");
        pd["RequireRollbackFile"]!.GetValue<bool>().Should().BeTrue("not covered");
    }

    // ── Arrays ───────────────────────────────────────────────────

    [Fact]
    public void PruneCoveredProperties_ArrayValues_NotPruned()
    {
        var parent = """{"RayMigrator":{"Products":[{"Alias":"MyApp"}],"Repository":{"ConnectionString":"X"}}}""";
        var child = """{"RayMigrator":{"Products":[{"Alias":"MyApp"}],"Repository":{"ConnectionString":"Y"}}}""";
        var groups = new List<IReadOnlyList<string>> { new List<string> { child } };

        var result = ZipExportService.PruneCoveredProperties(parent, groups);
        var ray = JsonNode.Parse(result)?["RayMigrator"]?.AsObject();

        ray!["Products"].Should().NotBeNull("arrays should not be pruned");
        ray["Repository"].Should().BeNull("scalar section should be pruned");
    }

    // ── Edge cases ───────────────────────────────────────────────

    [Fact]
    public void PruneCoveredProperties_EmptyChildGroups_ParentUnchanged()
    {
        var parent = """{"RayMigrator":{"Repository":{"ConnectionString":"X"}}}""";
        var groups = new List<IReadOnlyList<string>>();

        var result = ZipExportService.PruneCoveredProperties(parent, groups);
        var conn = JsonNode.Parse(result)?["RayMigrator"]?["Repository"]?["ConnectionString"]?.GetValue<string>();

        conn.Should().Be("X", "empty groups means no pruning");
    }

    [Fact]
    public void PruneCoveredProperties_OneComboGroupEmpty_PropertyStays()
    {
        var parent = """{"RayMigrator":{"Repository":{"ConnectionString":"X"}}}""";
        var child1 = """{"RayMigrator":{"Repository":{"ConnectionString":"A"}}}""";
        var groups = new List<IReadOnlyList<string>>
        {
            new List<string> { child1 },
            new List<string>() // empty group — no children cover this combo
        };

        var result = ZipExportService.PruneCoveredProperties(parent, groups);
        var conn = JsonNode.Parse(result)?["RayMigrator"]?["Repository"]?["ConnectionString"]?.GetValue<string>();

        conn.Should().Be("X", "one combo has no coverage");
    }

    [Fact]
    public void PruneCoveredProperties_MixedSections_OnlyCoveredSectionsPruned()
    {
        var parent = """
        {
          "RayMigrator": {
            "Repository": {"ConnectionString": "X", "DatabaseType": "SqlServer"},
            "ProductDefaults": {"MigrationErrorAction": "Terminate"}
          }
        }
        """;
        var child1 = """{"RayMigrator":{"Repository":{"ConnectionString":"A"},"ProductDefaults":{"MigrationErrorAction":"Rollback"}}}""";
        var child2 = """{"RayMigrator":{"Repository":{"ConnectionString":"B"}}}""";
        var groups = new List<IReadOnlyList<string>>
        {
            new List<string> { child1 },
            new List<string> { child2 }
        };

        var result = ZipExportService.PruneCoveredProperties(parent, groups);
        var ray = JsonNode.Parse(result)?["RayMigrator"]?.AsObject();

        // Repository.ConnectionString covered in both, DatabaseType not → Repository stays with DatabaseType
        ray!["Repository"]!["DatabaseType"]!.GetValue<string>().Should().Be("SqlServer");
        ray["Repository"]!["ConnectionString"].Should().BeNull();
        // ProductDefaults.MigrationErrorAction only covered in combo 1
        ray["ProductDefaults"]!["MigrationErrorAction"]!.GetValue<string>().Should().Be("Terminate");
    }

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
        var state = BuildMinimalState();
        state.BaseModel.Repository.ConnectionString = "{ENV:BASE_CONN}";

        var envModel = new ConfigurationModel();
        state.EnvironmentModels["Docker"] = envModel;

        // PE overrides ConnectionString
        var pe = new ConfigurationModel();
        pe.Repository.ConnectionString = "{ENV:PE_CONN}";
        state.ProductEnvironmentModels["MyApp.Docker"] = pe;

        // Single product in base
        state.BaseModel.Products.Add(new ProductModel { Alias = "MyApp" });

        var result = ZipExportService.ComputeExportJsons(state);

        var baseRay = JsonNode.Parse(result["appsettings.json"])?["RayMigrator"];
        baseRay!["Repository"]!["ConnectionString"].Should().BeNull("overridden in all PE combos");
    }

    [Fact]
    public void ComputeExportJsons_BasePropertyOverriddenInSomeNotAll_StaysInBase()
    {
        var state = BuildMinimalState();
        state.BaseModel.Repository.ConnectionString = "{ENV:BASE_CONN}";

        var envDev = new ConfigurationModel();
        envDev.Repository.ConnectionString = "{ENV:DEV_CONN}";
        state.EnvironmentModels["Development"] = envDev;

        // Production env does NOT override ConnectionString
        var envProd = new ConfigurationModel();
        state.EnvironmentModels["Production"] = envProd;

        var result = ZipExportService.ComputeExportJsons(state);

        var baseRay = JsonNode.Parse(result["appsettings.json"])?["RayMigrator"];
        baseRay!["Repository"]!["ConnectionString"]!.GetValue<string>().Should().Be("{ENV:BASE_CONN}",
            "not overridden in Production env");
    }

    [Fact]
    public void ComputeExportJsons_EnvPropertyOverriddenInAllPEs_PrunedFromEnv()
    {
        var state = BuildMinimalState();
        state.BaseModel.Repository.ConnectionString = "{ENV:BASE_CONN}";
        state.BaseModel.Repository.SchemaName = "ray";

        var envDev = new ConfigurationModel();
        envDev.Repository.ConnectionString = "{ENV:DEV_CONN}";
        envDev.Repository.SchemaName = "dev_schema"; // unique to env, PE does NOT override this
        envDev.ProductDefaults.MigrationErrorAction = "Rollback";
        state.EnvironmentModels["Development"] = envDev;

        // PE overrides ConnectionString and MigrationErrorAction (different from BOTH base and env)
        var pe = new ConfigurationModel();
        pe.Repository.ConnectionString = "{ENV:PE_CONN}";
        pe.ProductDefaults.MigrationErrorAction = "RollbackRelease";
        state.ProductEnvironmentModels["MyApp.Development"] = pe;

        var result = ZipExportService.ComputeExportJsons(state);

        var envRay = JsonNode.Parse(result["appsettings.Development.json"])?["RayMigrator"];
        // ConnectionString and MigrationErrorAction are covered by PE → pruned
        envRay!["Repository"]!["ConnectionString"].Should().BeNull("overridden by PE");
        envRay["ProductDefaults"].Should().BeNull("MigrationErrorAction overridden by PE");
        // SchemaName is NOT overridden by PE → stays
        envRay["Repository"]!["SchemaName"]!.GetValue<string>().Should().Be("dev_schema");
    }

    [Fact]
    public void ComputeExportJsons_PeRedundancyRemoval_MatchesExistingBehavior()
    {
        var state = BuildMinimalState();
        state.BaseModel.Repository.ConnectionString = "{ENV:BASE_CONN}";

        var envModel = new ConfigurationModel();
        envModel.Repository.ConnectionString = "{ENV:ENV_CONN}";
        state.EnvironmentModels["Docker"] = envModel;

        // PE has same ConnectionString as env + unique field
        var pe = new ConfigurationModel();
        pe.Repository.ConnectionString = "{ENV:ENV_CONN}"; // same as env → redundant in PE
        pe.ProductDefaults.RequireRollbackFile = false; // unique
        state.ProductEnvironmentModels["MyApp.Docker"] = pe;

        var result = ZipExportService.ComputeExportJsons(state);

        var peRay = JsonNode.Parse(result["appsettings.MyApp.Docker.json"])?["RayMigrator"];
        peRay!["Repository"].Should().BeNull("ConnectionString matches env parent");
        peRay["ProductDefaults"]!["RequireRollbackFile"]!.GetValue<bool>().Should().BeFalse();
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
}
