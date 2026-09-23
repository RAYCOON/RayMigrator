using System.Text.Json.Nodes;
using AwesomeAssertions;
using Raycoon.RayMigrator.Pipeline;
using Raycoon.RayMigrator.Shared.Configuration;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1: the file families the Config Wizard exports (golden export cases, #23 Part B) are read back by the
/// engine's loader to the effective configuration of every combination: the shared part and the run's own
/// product element.
/// </summary>
public class ConfigExportCasesTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "RayMigrator_ExportCases_" + Guid.NewGuid().ToString("N"));

    public ConfigExportCasesTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* best effort */ }
    }

    public static TheoryData<string> GoldenExportCases()
    {
        var data = new TheoryData<string>();
        foreach (var dir in Directory.GetDirectories(Path.Combine(AppContext.BaseDirectory, "ConfigExportCases")).OrderBy(d => d))
            data.Add(Path.GetFileName(dir));
        return data;
    }

    [Theory]
    [MemberData(nameof(GoldenExportCases))]
    public void Engine_ReadsExportedFiles_ToTheEffectiveConfiguration(string caseName)
    {
        string source = Path.Combine(AppContext.BaseDirectory, "ConfigExportCases", caseName);
        foreach (var file in Directory.GetFiles(Path.Combine(source, "expected"), "*.json"))
            File.Copy(file, Path.Combine(_dir, Path.GetFileName(file)));
        var effective = JsonNode.Parse(File.ReadAllText(Path.Combine(source, "effective.json")))!.AsObject();

        foreach (var (key, expectedNode) in effective)
        {
            var parts = key.Split('.', 2);
            string product = parts[0], environment = parts[1];
            var diagnostics = new List<(string Filename, bool Found)>();

            var merged = JsonOptionsSource.MergeConfigurationFiles(_dir, product, environment, diagnostics).AsObject();
            var expected = expectedNode!.DeepClone().AsObject();

            var ownExpected = ConfigurationJsonDiff.FindByAlias(expected["RayMigrator"]!["Products"]!.AsArray(), product)!;
            var ownActual = ConfigurationJsonDiff.FindByAlias(merged["RayMigrator"]!["Products"]!.AsArray(), product);
            JsonNode.DeepEquals(ownExpected, ownActual).Should().BeTrue($"{caseName} {key}: own product element expected\n{ownExpected.ToJsonString()}\nbut got\n{ownActual?.ToJsonString()}");

            expected["RayMigrator"]!.AsObject().Remove("Products");
            merged["RayMigrator"]!.AsObject().Remove("Products");
            JsonNode.DeepEquals(expected, merged).Should().BeTrue($"{caseName} {key}: shared part expected\n{expected.ToJsonString()}\nbut got\n{merged.ToJsonString()}");
        }
    }
}
