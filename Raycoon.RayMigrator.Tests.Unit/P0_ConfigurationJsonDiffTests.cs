using System.Text.Json.Nodes;
using AwesomeAssertions;
using Raycoon.RayMigrator.Shared.Configuration;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P0: the inverse operations of the merge (#23 Part B). Diff must be the smallest document that merges
/// back to the target, Common the part all documents agree on; both follow the alias rules of the merger.
/// </summary>
public class ConfigurationJsonDiffTests
{
    private static JsonNode P(string json) => ConfigurationJsonMerger.Parse(json)!;

    [Fact]
    public void Diff_EqualDocuments_IsNull()
    {
        ConfigurationJsonDiff.Diff(P("""{"a":1,"b":[{"Alias":"x","v":1}]}"""), P("""{"a":1,"b":[{"Alias":"x","v":1}]}""")).Should().BeNull();
    }

    [Fact]
    public void Diff_ScalarChanged_ContainsOnlyThatKey()
    {
        var diff = ConfigurationJsonDiff.Diff(P("""{"a":1,"b":2}"""), P("""{"a":1,"b":3}"""));

        diff!.ToJsonString().Should().Be("""{"b":3}""");
    }

    [Fact]
    public void Diff_AliasArray_ContainsOnlyChangedElementsWithAliasAndChangedProperties()
    {
        var parent = P("""{"Products":[{"Alias":"Shop","A":1,"B":1},{"Alias":"Crm","A":1}]}""");
        var target = P("""{"Products":[{"Alias":"Shop","A":1,"B":2},{"Alias":"Crm","A":1},{"Alias":"New","A":5}]}""");

        var diff = ConfigurationJsonDiff.Diff(parent, target);

        diff!.ToJsonString().Should().Be("""{"Products":[{"Alias":"Shop","B":2},{"Alias":"New","A":5}]}""");
    }

    [Fact]
    public void Diff_NonAliasArrayChanged_IsWrittenWhole()
    {
        var diff = ConfigurationJsonDiff.Diff(P("""{"WriteTo":[{"Name":"A"},{"Name":"B"}]}"""), P("""{"WriteTo":[{"Name":"A"}]}"""));

        diff!.ToJsonString().Should().Be("""{"WriteTo":[{"Name":"A"}]}""");
    }

    [Fact]
    public void Diff_ExplicitNullInTarget_IsWritten_NullInBothIsNot()
    {
        ConfigurationJsonDiff.Diff(P("""{"a":"x","b":null}"""), P("""{"a":null,"b":null}"""))!.ToJsonString().Should().Be("""{"a":null}""");
        ConfigurationJsonDiff.Diff(P("""{"b":null}"""), P("""{"b":null}""")).Should().BeNull();
    }

    [Fact]
    public void Diff_KeyOnlyInParent_IsIgnored()
    {
        ConfigurationJsonDiff.Diff(P("""{"a":1,"gone":2}"""), P("""{"a":1}""")).Should().BeNull();
    }

    [Fact]
    public void Common_ScalarsAndNonAliasArrays_OnlyWhenEqualEverywhere()
    {
        ConfigurationJsonDiff.TryCommon(new List<JsonNode?> { P("""{"a":1,"b":2,"c":[1,2]}"""), P("""{"a":1,"b":3,"c":[1,2]}""") }, out var common).Should().BeTrue();

        common!.ToJsonString().Should().Be("""{"a":1,"c":[1,2]}""");
    }

    [Fact]
    public void Common_AliasArray_ElementsPresentEverywhere_ReducedToTheirCommonPart()
    {
        var docs = new List<JsonNode?>
        {
            P("""{"P":[{"Alias":"Shop","A":1,"B":1},{"Alias":"Crm","A":2}]}"""),
            P("""{"P":[{"Alias":"shop","A":1,"B":9}]}"""),
        };

        ConfigurationJsonDiff.TryCommon(docs, out var common).Should().BeTrue();

        common!.ToJsonString().Should().Be("""{"P":[{"Alias":"Shop","A":1}]}""");
    }

    [Fact]
    public void Common_AliasOnlyInCommon_KeepsTheElement()
    {
        var docs = new List<JsonNode?> { P("""{"P":[{"Alias":"Shop","A":1}]}"""), P("""{"P":[{"Alias":"Shop","A":2}]}""") };

        ConfigurationJsonDiff.TryCommon(docs, out var common).Should().BeTrue();

        common!.ToJsonString().Should().Be("""{"P":[{"Alias":"Shop"}]}""");
    }

    [Fact]
    public void Common_NothingInCommon_ReturnsFalse()
    {
        ConfigurationJsonDiff.TryCommon(new List<JsonNode?> { P("""{"a":1}"""), P("""{"a":2}""") }, out var common).Should().BeFalse();
        common.Should().BeNull();
        ConfigurationJsonDiff.TryCommon(new List<JsonNode?>(), out _).Should().BeFalse();
    }

    [Fact]
    public void Common_KeyCasingDiffers_MatchesCaseInsensitively_KeepsFirstSpelling()
    {
        ConfigurationJsonDiff.TryCommon(new List<JsonNode?> { P("""{"Repository":{"SchemaName":"s"}}"""), P("""{"repository":{"schemaname":"s"}}""") }, out var common).Should().BeTrue();

        common!.ToJsonString().Should().Be("""{"Repository":{"SchemaName":"s"}}""");
    }

    public static TheoryData<string> GoldenMergeCases()
    {
        var data = new TheoryData<string>();
        foreach (var dir in Directory.GetDirectories(Path.Combine(AppContext.BaseDirectory, "ConfigMergeCases")).OrderBy(d => d))
            data.Add(Path.GetFileName(dir));
        return data;
    }

    [Theory]
    [MemberData(nameof(GoldenMergeCases))]
    public void RoundTrip_MergeOfDiff_ReproducesTarget(string caseName)
    {
        string dir = Path.Combine(AppContext.BaseDirectory, "ConfigMergeCases", caseName);
        var parent = P(File.ReadAllText(Path.Combine(dir, "appsettings.json")));
        var target = P(File.ReadAllText(Path.Combine(dir, "expected.json")));

        var diff = ConfigurationJsonDiff.Diff(parent, target);
        var merged = ConfigurationJsonMerger.Merge(parent, diff)!;

        JsonNode.DeepEquals(merged, target).Should().BeTrue($"case '{caseName}': diff\n{diff?.ToJsonString()}\nmerged\n{merged.ToJsonString()}");
    }
}
