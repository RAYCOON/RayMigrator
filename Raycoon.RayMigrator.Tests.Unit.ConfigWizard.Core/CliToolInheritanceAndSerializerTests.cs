
using System.Text.Json.Nodes;
using Raycoon.RayMigrator.Tests.Unit.ConfigWizard.Core.Helpers;
using Raycoon.RayMigrator.Validation;

namespace Raycoon.RayMigrator.Tests.Unit.ConfigWizard.Core;

/// <summary>
/// Covers the CliTool validation and serialization fixes:
/// - Fix 5: WizardValidationInputAdapter walks CliToolParameters inheritance so Alias-Mismatch is caught pre-save.
/// - Fix 4: Serializer skips CliToolParameters when target has no effective CLI tool alias.
/// </summary>
public class CliToolInheritanceAndSerializerTests
{
    private static ConfigurationModel BuildModelWithCliTool(
        string? productAlias,
        string? tgAlias,
        string? targetAlias,
        Dictionary<string, string>? productParams = null,
        Dictionary<string, string>? tgParams = null,
        Dictionary<string, string>? targetParams = null,
        CliToolModel? tool = null)
    {
        var model = TestModelFactory.CreateValidModel();
        if (tool != null) model.CliTools.Add(tool);

        var product = model.Products[0];
        product.UseCliToolAlias = new OverridableValue<string>
        {
            IsOverridden = !string.IsNullOrWhiteSpace(productAlias),
            Value = productAlias
        };
        product.CliToolParameters = productParams;

        var tg = product.TargetGroups[0];
        tg.UseCliToolAlias = new OverridableValue<string>
        {
            IsOverridden = !string.IsNullOrWhiteSpace(tgAlias),
            Value = tgAlias
        };
        tg.CliToolParameters = tgParams;

        var target = tg.Targets[0];
        target.UseCliToolAlias = new OverridableValue<string>
        {
            IsOverridden = !string.IsNullOrWhiteSpace(targetAlias),
            Value = targetAlias
        };
        target.CliToolParameters = targetParams;

        return model;
    }

    private static CliToolModel Tool(string alias, string template) => new()
    {
        Alias = alias,
        ExecutablePath = alias,
        ArgumentTemplate = template,
        InputMode = "File",
        SuccessExitCodes = new List<string> { "0" },
        CliToolTimeoutInSeconds = 60,
    };

    // ── Fix 5 — Adapter walks CliToolParameters inheritance ─────────────

    [Fact]
    public void Adapter_WalksCliToolParameterInheritance_FromProduct()
    {
        var productParams = new Dictionary<string, string>
        {
            { "Server", "s" }, { "User", "u" }, { "Password", "p" }, { "Database", "d" }
        };
        var model = BuildModelWithCliTool(
            productAlias: "sqlcmd", tgAlias: null, targetAlias: null,
            productParams: productParams,
            tool: Tool("sqlcmd", "-S {Server} -U {User} -P {Password} -d {Database} -i {FilePath}"));

        // Target has no own params; adapter must expose product-level via inheritance walk.
        var result = ConfigurationValidator.ValidateAll(model);

        // No RULE_3_8 (all keys present via inheritance)
        result.Errors.Should().NotContain(e => e.Code == RuleIds.RULE_3_8);
        result.Warnings.Should().NotContain(e => e.Code == RuleIds.RULE_3_10);
    }

    [Fact]
    public void Adapter_FiresRule38_ForAliasMismatch_MissingRequiredKeys()
    {
        // Product defines sqlcmd-style params, Target overrides alias to psql which needs different keys.
        var productParams = new Dictionary<string, string>
        {
            { "Server", "s" }, { "User", "u" }, { "Password", "p" }, { "Database", "d" }
        };
        var model = BuildModelWithCliTool(
            productAlias: "sqlcmd", tgAlias: null, targetAlias: "psql",
            productParams: productParams);
        model.CliTools.Add(Tool("sqlcmd", "-S {Server} -U {User} -P {Password} -d {Database} -i {FilePath}"));
        model.CliTools.Add(Tool("psql", "-h {Host} -p {Port} -U {User} -d {Database} -f {FilePath}"));

        var result = ConfigurationValidator.ValidateAll(model);

        // psql expects {Host, Port, User, Database}; inherited params provide {Server, User, Password, Database}
        result.Errors.Should().Contain(e => e.Code == RuleIds.RULE_3_8);
        // And unused keys (Server, Password) trigger RULE_3_10
        result.Warnings.Should().Contain(e => e.Code == RuleIds.RULE_3_10);
    }

    [Fact]
    public void Adapter_FiresRule38_WhenNoParamsAtAnyLevel()
    {
        // The user's exact Bug 1 scenario: UseCliToolAlias active, no CliToolParameters anywhere.
        var model = BuildModelWithCliTool(
            productAlias: null, tgAlias: null, targetAlias: "sqlcmd",
            tool: Tool("sqlcmd", "-S {Server} -U {User} -P {Password} -d {Database} -i {FilePath}"));

        var result = ConfigurationValidator.ValidateAll(model);

        result.Errors.Should().Contain(e => e.Code == RuleIds.RULE_3_8);
    }

    // ── Fix 4 — Serializer skips CliToolParameters without effective alias ─────

    [Fact]
    public void Serializer_SkipsCliToolParameters_WhenTargetHasNoEffectiveAlias()
    {
        // Product has params, but no alias anywhere → target should NOT receive params in JSON.
        var model = BuildModelWithCliTool(
            productAlias: null, tgAlias: null, targetAlias: null,
            productParams: new Dictionary<string, string> { { "Server", "s" }, { "User", "u" } });

        var json = ConfigurationSerializer.ToJson(model);
        var parsed = JsonNode.Parse(json)!;
        var targetObj = parsed["RayMigrator"]!["Products"]![0]!["TargetGroups"]![0]!["Targets"]![0]!;

        targetObj["CliToolParameters"].Should().BeNull(
            "target has no effective UseCliToolAlias, so inherited params must not be propagated");
    }

    [Fact]
    public void Serializer_WritesInheritedCliToolParameters_WhenTargetHasEffectiveAlias()
    {
        var productParams = new Dictionary<string, string> { { "Server", "s" }, { "User", "u" } };
        var model = BuildModelWithCliTool(
            productAlias: null, tgAlias: null, targetAlias: "sqlcmd",
            productParams: productParams,
            tool: Tool("sqlcmd", "-S {Server} -U {User} -i {FilePath}"));

        var json = ConfigurationSerializer.ToJson(model);
        var parsed = JsonNode.Parse(json)!;
        var targetObj = parsed["RayMigrator"]!["Products"]![0]!["TargetGroups"]![0]!["Targets"]![0]!;

        targetObj["CliToolParameters"].Should().NotBeNull(
            "target has effective alias, so inherited product-level params must be propagated");
        targetObj["CliToolParameters"]!["Server"]!.GetValue<string>().Should().Be("s");
        targetObj["CliToolParameters"]!["User"]!.GetValue<string>().Should().Be("u");
    }

    [Fact]
    public void Serializer_BuildFullTarget_SkipsCliToolParameters_WhenTargetHasNoEffectiveAlias()
    {
        // BuildFullTarget is hit when the diff path encounters a new product/target not in base.
        // It lives in the same if-chain as the standard path and must also obey the guard.
        var baseModel = TestModelFactory.CreateValidModel();

        var overlay = TestModelFactory.CreateValidModel();
        // Add a new product to overlay that does not exist in base — forces BuildFullProduct → BuildFullTargetGroup → BuildFullTarget.
        var newProduct = TestModelFactory.CreateValidProduct("BrandNewProduct");
        newProduct.CliToolParameters = new Dictionary<string, string> { ["Server"] = "s" };
        overlay.Products.Add(newProduct);

        var diffJson = ConfigurationSerializer.ToJson(overlay, baseModel);
        var parsed = JsonNode.Parse(diffJson)!;

        var products = parsed["RayMigrator"]?["Products"] as JsonArray;
        products.Should().NotBeNull();
        var brandNew = products!.FirstOrDefault(p => p?["Alias"]?.GetValue<string>() == "BrandNewProduct");
        brandNew.Should().NotBeNull("the new product must be serialized via BuildFullProduct path");

        var targets = brandNew!["TargetGroups"]?[0]?["Targets"] as JsonArray;
        if (targets is { Count: > 0 })
        {
            targets[0]!["CliToolParameters"].Should().BeNull(
                "BuildFullTarget must not propagate product-level CliToolParameters when the target has no effective alias");
        }
    }

    [Fact]
    public void Serializer_Diff_SkipsCliToolParameters_WhenTargetHasNoEffectiveAlias()
    {
        // Build a base model (saved) + an overlay that removes the alias while keeping orphan params.
        // The diff path must also drop the orphan params.
        var baseModel = BuildModelWithCliTool(
            productAlias: null, tgAlias: null, targetAlias: "sqlcmd",
            targetParams: new Dictionary<string, string> { { "Server", "s" } },
            tool: Tool("sqlcmd", "-S {Server} -i {FilePath}"));

        var overlay = BuildModelWithCliTool(
            productAlias: null, tgAlias: null, targetAlias: null,
            targetParams: new Dictionary<string, string> { { "Server", "s" } });

        var diffJson = ConfigurationSerializer.ToJson(overlay, baseModel);
        var parsed = JsonNode.Parse(diffJson)!;

        // If targets got serialized at all, their CliToolParameters key must be absent.
        var products = parsed["RayMigrator"]?["Products"] as JsonArray;
        if (products is { Count: > 0 })
        {
            var targets = products[0]?["TargetGroups"]?[0]?["Targets"] as JsonArray;
            if (targets is { Count: > 0 })
            {
                targets[0]!["CliToolParameters"].Should().BeNull();
            }
        }
    }
}
