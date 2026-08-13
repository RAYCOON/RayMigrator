using AwesomeAssertions;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Core.Models;
using Raycoon.RayMigrator.Services;
using Raycoon.RayMigrator.Shared.Exceptions;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1: TargetGroupMigrationOrder feature tests.
/// Covers parsing, validation/reordering, TOML integration, GetFullExecutionOrder integration,
/// and CLI comma-separated parsing. Incorrect order causes migrations to execute target groups
/// in the wrong sequence, potentially breaking cross-database schema dependencies.
/// </summary>
public class TargetGroupMigrationOrderTests
{
    private static MigrationFileInfo CreateFile(
        int fileOrderId, string release = "Release 1.0", string targetGroup = "Backend")
    {
        return new MigrationFileInfo
        {
            Filename = $"{fileOrderId:D2}_Migration.sql",
            ReleaseVersion = release,
            TargetGroupAlias = targetGroup,
            FileOrderId = fileOrderId,
            FileUpHash = $"hash_{fileOrderId}"
        };
    }

    private static TargetGroupOptions CreateTargetGroup(string alias, string targetMigrationOrder = "Simultaneously")
    {
        return new TargetGroupOptions
        {
            Alias = alias,
            DatabaseType = "SqlServer",
            TargetMigrationOrder = targetMigrationOrder,
            Targets = new List<TargetOptions> { new() { Alias = "T1", ConnectionString = $"Server={alias}" } }
        };
    }

    #region Parsing

    [Fact]
    public void ParseTargetGroupMigrationOrder_Null_ReturnsNull()
    {
        var result = MigrationService.ParseTargetGroupMigrationOrder(null);

        result.Should().BeNull();
    }

    [Fact]
    public void ParseTargetGroupMigrationOrder_Empty_ReturnsNull()
    {
        var result = MigrationService.ParseTargetGroupMigrationOrder(string.Empty);

        result.Should().BeNull();
    }

    [Fact]
    public void ParseTargetGroupMigrationOrder_WhitespaceOnly_ReturnsNull()
    {
        var result = MigrationService.ParseTargetGroupMigrationOrder("   ");

        result.Should().BeNull();
    }

    [Fact]
    public void ParseTargetGroupMigrationOrder_SingleAlias_ReturnsSingleElement()
    {
        var result = MigrationService.ParseTargetGroupMigrationOrder("Frontend");

        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result![0].Should().Be("Frontend");
    }

    [Fact]
    public void ParseTargetGroupMigrationOrder_MultipleAliases_ParsesCorrectly()
    {
        var result = MigrationService.ParseTargetGroupMigrationOrder("Frontend,Backend");

        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result![0].Should().Be("Frontend");
        result![1].Should().Be("Backend");
    }

    [Fact]
    public void ParseTargetGroupMigrationOrder_TrimsWhitespace()
    {
        var result = MigrationService.ParseTargetGroupMigrationOrder(" Frontend , Backend ");

        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result![0].Should().Be("Frontend");
        result![1].Should().Be("Backend");
    }

    [Fact]
    public void ParseTargetGroupMigrationOrder_SkipsEmptySegments()
    {
        var result = MigrationService.ParseTargetGroupMigrationOrder("Frontend,,Backend");

        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result![0].Should().Be("Frontend");
        result![1].Should().Be("Backend");
    }

    #endregion Parsing

    #region Validation

    [Fact]
    public void ValidateAndReorder_ValidOrder_ReturnsReorderedList()
    {
        var targetGroups = new List<TargetGroupOptions>
        {
            CreateTargetGroup("Backend"),
            CreateTargetGroup("Frontend"),
        };
        var order = new[] { "Frontend", "Backend" };

        var result = MigrationService.ValidateAndReorderTargetGroups(order, targetGroups, "test");

        result.Should().HaveCount(2);
        result[0].Alias.Should().Be("Frontend");
        result[1].Alias.Should().Be("Backend");
    }

    [Fact]
    public void ValidateAndReorder_ReverseOrder_ReturnsReversed()
    {
        var targetGroups = new List<TargetGroupOptions>
        {
            CreateTargetGroup("Alpha"),
            CreateTargetGroup("Beta"),
        };
        var order = new[] { "Beta", "Alpha" };

        var result = MigrationService.ValidateAndReorderTargetGroups(order, targetGroups, "test");

        result.Should().HaveCount(2);
        result[0].Alias.Should().Be("Beta");
        result[1].Alias.Should().Be("Alpha");
    }

    [Fact]
    public void ValidateAndReorder_ThreeTargetGroups_ReordersCorrectly()
    {
        var targetGroups = new List<TargetGroupOptions>
        {
            CreateTargetGroup("Alpha"),
            CreateTargetGroup("Beta"),
            CreateTargetGroup("Gamma"),
        };
        var order = new[] { "Gamma", "Alpha", "Beta" };

        var result = MigrationService.ValidateAndReorderTargetGroups(order, targetGroups, "test");

        result.Should().HaveCount(3);
        result[0].Alias.Should().Be("Gamma");
        result[1].Alias.Should().Be("Alpha");
        result[2].Alias.Should().Be("Beta");
    }

    [Fact]
    public void ValidateAndReorder_SingleTargetGroup_ThrowsConfigurationValidationException()
    {
        var targetGroups = new List<TargetGroupOptions>
        {
            CreateTargetGroup("Backend"),
        };
        var order = new[] { "Backend" };

        var act = () => MigrationService.ValidateAndReorderTargetGroups(order, targetGroups, "test");

        act.Should().Throw<ConfigurationValidationException>()
            .WithMessage("*only 1 TargetGroup*");
    }

    [Fact]
    public void ValidateAndReorder_PartialList_ThrowsConfigurationValidationException()
    {
        var targetGroups = new List<TargetGroupOptions>
        {
            CreateTargetGroup("Backend"),
            CreateTargetGroup("Frontend"),
        };
        var order = new[] { "Frontend" }; // only 1 of 2 specified

        var act = () => MigrationService.ValidateAndReorderTargetGroups(order, targetGroups, "test");

        act.Should().Throw<ConfigurationValidationException>()
            .WithMessage("*1 aliases*")
            .WithMessage("*2 TargetGroups*");
    }

    [Fact]
    public void ValidateAndReorder_TooManyAliases_ThrowsConfigurationValidationException()
    {
        var targetGroups = new List<TargetGroupOptions>
        {
            CreateTargetGroup("Backend"),
            CreateTargetGroup("Frontend"),
        };
        var order = new[] { "Backend", "Frontend", "Extra" }; // 3 for 2 TGs

        var act = () => MigrationService.ValidateAndReorderTargetGroups(order, targetGroups, "test");

        act.Should().Throw<ConfigurationValidationException>()
            .WithMessage("*3 aliases*")
            .WithMessage("*2 TargetGroups*");
    }

    [Fact]
    public void ValidateAndReorder_UnknownAlias_ThrowsWithAvailableList()
    {
        var targetGroups = new List<TargetGroupOptions>
        {
            CreateTargetGroup("Backend"),
            CreateTargetGroup("Frontend"),
        };
        var order = new[] { "Backend", "Unknown" };

        var act = () => MigrationService.ValidateAndReorderTargetGroups(order, targetGroups, "test");

        var ex = act.Should().Throw<ConfigurationValidationException>().Which;
        ex.Message.Should().Contain("Unknown");
        ex.Message.Should().Contain("Backend");
        ex.Message.Should().Contain("Frontend");
    }

    [Fact]
    public void ValidateAndReorder_CaseInsensitiveMismatch_ThrowsWithHint()
    {
        var targetGroups = new List<TargetGroupOptions>
        {
            CreateTargetGroup("Backend"),
            CreateTargetGroup("Frontend"),
        };
        var order = new[] { "backend", "Frontend" }; // lowercase "backend" vs "Backend"

        var act = () => MigrationService.ValidateAndReorderTargetGroups(order, targetGroups, "test");

        var ex = act.Should().Throw<ConfigurationValidationException>().Which;
        ex.Message.Should().Contain("backend");
        ex.Message.Should().Contain("Backend");
        ex.Message.Should().Contain("case-insensitively");
    }

    [Fact]
    public void ValidateAndReorder_DuplicateAlias_ThrowsConfigurationValidationException()
    {
        var targetGroups = new List<TargetGroupOptions>
        {
            CreateTargetGroup("Backend"),
            CreateTargetGroup("Frontend"),
        };
        var order = new[] { "Frontend", "Frontend" };

        var act = () => MigrationService.ValidateAndReorderTargetGroups(order, targetGroups, "test");

        act.Should().Throw<ConfigurationValidationException>()
            .WithMessage("*duplicate alias 'Frontend'*");
    }

    [Fact]
    public void ValidateAndReorder_ExactCaseMatch_Succeeds()
    {
        var targetGroups = new List<TargetGroupOptions>
        {
            CreateTargetGroup("BackEnd"),
            CreateTargetGroup("FrontEnd"),
        };
        var order = new[] { "FrontEnd", "BackEnd" };

        var result = MigrationService.ValidateAndReorderTargetGroups(order, targetGroups, "test");

        result.Should().HaveCount(2);
        result[0].Alias.Should().Be("FrontEnd");
        result[1].Alias.Should().Be("BackEnd");
    }

    [Fact]
    public void ValidateAndReorder_EmptyArray_ThrowsConfigurationValidationException()
    {
        var targetGroups = new List<TargetGroupOptions>
        {
            CreateTargetGroup("Backend"),
            CreateTargetGroup("Frontend"),
        };
        var order = Array.Empty<string>();

        var act = () => MigrationService.ValidateAndReorderTargetGroups(order, targetGroups, "test");

        act.Should().Throw<ConfigurationValidationException>()
            .WithMessage("*0 aliases*")
            .WithMessage("*2 TargetGroups*");
    }

    #endregion Validation

    #region TOML Parsing

    [Fact]
    public void ParseTomlConfig_TargetGroupMigrationOrder_ParsesArray()
    {
        MigrationService.ParseTomlConfig(
            "TargetGroupMigrationOrder = [\"Frontend\",\"Backend\"]",
            out _, out _, out _, out _, out _, out _, out _, out _, out _,
            out List<string>? tgeo, out _);

        tgeo.Should().NotBeNull();
        tgeo.Should().HaveCount(2);
        tgeo![0].Should().Be("Frontend");
        tgeo![1].Should().Be("Backend");
    }

    [Fact]
    public void ParseTomlConfig_TargetGroupMigrationOrder_NotPresent_ReturnsNull()
    {
        MigrationService.ParseTomlConfig(
            "UseTransaction = true",
            out _, out _, out _, out _, out _, out _, out _, out _, out _,
            out List<string>? tgeo, out _);

        tgeo.Should().BeNull();
    }

    [Fact]
    public void ParseTomlConfig_TargetGroupMigrationOrder_EmptyArray_ReturnsEmptyList()
    {
        MigrationService.ParseTomlConfig(
            "TargetGroupMigrationOrder = []",
            out _, out _, out _, out _, out _, out _, out _, out _, out _,
            out List<string>? tgeo, out _);

        tgeo.Should().NotBeNull();
        tgeo.Should().BeEmpty();
    }

    #endregion TOML Parsing

    #region GetFullExecutionOrder Integration

    [Fact]
    public void GetFullExecutionOrder_WithCustomOrder_RespectsOrder()
    {
        // Arrange: config order is Backend first, but we specify Frontend first
        var files = new List<MigrationFileInfo>
        {
            CreateFile(1, "Release 1.0", "Backend"),
            CreateFile(2, "Release 1.0", "Frontend"),
        };
        var targetGroups = new List<TargetGroupOptions>
        {
            CreateTargetGroup("Backend"),
            CreateTargetGroup("Frontend"),
        };
        var customOrder = new[] { "Frontend", "Backend" };

        var order = MigrationService.GetFullExecutionOrder(files, targetGroups, customOrder);

        order.Should().HaveCount(2);
        order[0].TargetGroupAlias.Should().Be("Frontend");
        order[1].TargetGroupAlias.Should().Be("Backend");
    }

    [Fact]
    public void GetFullExecutionOrder_WithReversedOrder_ReversesTargetGroupProcessing()
    {
        // Arrange: config order Frontend→Backend, reversed to Backend→Frontend
        var files = new List<MigrationFileInfo>
        {
            CreateFile(1, "Release 1.0", "Frontend"),
            CreateFile(2, "Release 1.0", "Backend"),
        };
        var targetGroups = new List<TargetGroupOptions>
        {
            CreateTargetGroup("Frontend"),
            CreateTargetGroup("Backend"),
        };
        var reversedOrder = new[] { "Backend", "Frontend" };

        var order = MigrationService.GetFullExecutionOrder(files, targetGroups, reversedOrder);

        order.Should().HaveCount(2);
        order[0].TargetGroupAlias.Should().Be("Backend");
        order[1].TargetGroupAlias.Should().Be("Frontend");
    }

    [Fact]
    public void GetFullExecutionOrder_WithNullOrder_UsesConfigOrder()
    {
        // Arrange: config order is Frontend first
        var files = new List<MigrationFileInfo>
        {
            CreateFile(1, "Release 1.0", "Frontend"),
            CreateFile(2, "Release 1.0", "Backend"),
        };
        var targetGroups = new List<TargetGroupOptions>
        {
            CreateTargetGroup("Frontend"),
            CreateTargetGroup("Backend"),
        };

        var order = MigrationService.GetFullExecutionOrder(files, targetGroups, null);

        order.Should().HaveCount(2);
        order[0].TargetGroupAlias.Should().Be("Frontend");
        order[1].TargetGroupAlias.Should().Be("Backend");
    }

    [Fact]
    public void GetFullExecutionOrder_TwoReleases_SameOrderApplied()
    {
        // Arrange: 2 releases each with 2 target groups, custom order overrides both
        var files = new List<MigrationFileInfo>
        {
            CreateFile(1, "Release 1.0", "Backend"),
            CreateFile(2, "Release 1.0", "Frontend"),
            CreateFile(3, "Release 1.1", "Backend"),
            CreateFile(4, "Release 1.1", "Frontend"),
        };
        var targetGroups = new List<TargetGroupOptions>
        {
            CreateTargetGroup("Backend"),
            CreateTargetGroup("Frontend"),
        };
        var customOrder = new[] { "Frontend", "Backend" };

        var order = MigrationService.GetFullExecutionOrder(files, targetGroups, customOrder);

        order.Should().HaveCount(4);
        // Release 1.0: Frontend before Backend
        order[0].Should().Be((2, "Frontend", "T1"));
        order[1].Should().Be((1, "Backend", "T1"));
        // Release 1.1: Frontend before Backend
        order[2].Should().Be((4, "Frontend", "T1"));
        order[3].Should().Be((3, "Backend", "T1"));
    }

    #endregion GetFullExecutionOrder Integration

    #region CLI Parsing

    [Fact]
    public void ParseCommaSeparatedToArray_ValidInput_ReturnsArray()
    {
        var result = CommandLineConfiguration.ParseCommaSeparatedToArray("Frontend,Backend");

        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result![0].Should().Be("Frontend");
        result![1].Should().Be("Backend");
    }

    [Fact]
    public void ParseCommaSeparatedToArray_Null_ReturnsNull()
    {
        var result = CommandLineConfiguration.ParseCommaSeparatedToArray(null);

        result.Should().BeNull();
    }

    #endregion CLI Parsing
}
