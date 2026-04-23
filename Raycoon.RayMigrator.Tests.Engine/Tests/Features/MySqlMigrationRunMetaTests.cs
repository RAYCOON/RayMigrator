// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

using System.Text.Json;
using Raycoon.RayMigrator.Core.Configuration;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Tests.Engine.Fixtures;
using Raycoon.RayMigrator.Tests.Engine.Infrastructure;

namespace Raycoon.RayMigrator.Tests.Engine.Tests.Features;

[Collection("MySql")]
[Trait("Engine", "MySql")]
[Trait("Category", "Features")]
public class MySqlMigrationRunMetaTests : MySqlTestBase
{
    public MySqlMigrationRunMetaTests(MySqlFixture fixture) : base(fixture) { }

    [Fact] public async Task SettingsJson_ShouldExist() { Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available"); await using var ctx = await CreateScenario().BuildAsync(); await ctx.MigrateUpAsync(); ctx.AssertSuccess(true); var json = ctx.GetMigrationRunSettingsJson(); json.Should().NotBeNullOrEmpty("MigrationRunMeta should contain settings JSON after MigrateUp"); var doc = JsonDocument.Parse(json!); var root = doc.RootElement; root.TryGetProperty("RayMigratorVersion", out _).Should().BeTrue("JSON should contain RayMigratorVersion"); root.TryGetProperty("ConsoleOptions", out _).Should().BeTrue("JSON should contain ConsoleOptions"); root.TryGetProperty("Repository", out _).Should().BeTrue("JSON should contain Repository"); root.TryGetProperty("ProductDefaults", out _).Should().BeTrue("JSON should contain ProductDefaults"); root.TryGetProperty("Product", out _).Should().BeTrue("JSON should contain Product"); }

    [Fact] public async Task ConsoleOptions_InJson() { Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available"); await using var ctx = await CreateScenario().BuildAsync(); await ctx.MigrateUpAsync(); ctx.AssertSuccess(true); var json = ctx.GetMigrationRunSettingsJson(); var doc = JsonDocument.Parse(json!); var consoleOpts = doc.RootElement.GetProperty("ConsoleOptions"); consoleOpts.GetProperty("Command").GetString().Should().Be("MigrateUp"); consoleOpts.GetProperty("RunMode").GetString().Should().Be("Migrate"); consoleOpts.GetProperty("Product").GetString().Should().Be("EngineTest"); }

    [Fact] public async Task Product_WithTargetGroups_InJson() { Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available"); await using var ctx = await CreateScenario().BuildAsync(); await ctx.MigrateUpAsync(); ctx.AssertSuccess(true); var json = ctx.GetMigrationRunSettingsJson(); var doc = JsonDocument.Parse(json!); var product = doc.RootElement.GetProperty("Product"); product.GetProperty("Alias").GetString().Should().Be("EngineTest"); var targetGroups = product.GetProperty("TargetGroups"); targetGroups.GetArrayLength().Should().BeGreaterThan(0, "Product should have at least one TargetGroup"); foreach (var tg in targetGroups.EnumerateArray()) { tg.TryGetProperty("Targets", out var targets).Should().BeTrue(); targets.GetArrayLength().Should().BeGreaterThan(0, "Each TargetGroup should have at least one Target"); } }

    [Fact] public async Task ConnectionStrings_ShouldBeMasked() { Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available"); await using var ctx = await CreateScenario().BuildAsync(); await ctx.MigrateUpAsync(); ctx.AssertSuccess(true); var json = ctx.GetMigrationRunSettingsJson(); json.Should().NotBeNullOrEmpty(); var doc = JsonDocument.Parse(json!); var repoConnStr = doc.RootElement.GetProperty("Repository").GetProperty("ConnectionString").GetString(); repoConnStr.Should().NotBeNullOrEmpty("Repository connection string should be present in settings JSON"); var targetGroups = doc.RootElement.GetProperty("Product").GetProperty("TargetGroups"); foreach (var tg in targetGroups.EnumerateArray()) { foreach (var target in tg.GetProperty("Targets").EnumerateArray()) { var connStr = target.GetProperty("ConnectionString").GetString(); connStr.Should().NotBeNullOrEmpty($"Target [{target.GetProperty("Alias").GetString()}] connection string should be present"); } } }

    [Fact] public async Task MetaCount_MatchesRunCount() { Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available"); await using var ctx = await CreateScenario().BuildAsync(); await ctx.MigrateUpAsync(); ctx.AssertSuccess(true); int metaCount = ctx.CountRepoRows("MigrationRunMeta"); int runCount = ctx.CountMigrationRuns(); metaCount.Should().Be(runCount, "MigrationRunMeta should have a 1:1 relationship with MigrationRun"); }

    [Fact] public async Task MigrateDown_SettingsJson_ShouldContainMaskedConnectionStrings() { Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available"); await using var ctx = await CreateScenario().BuildAsync(); await ctx.MigrateUpAsync(); ctx.AssertSuccess(true); await ctx.RebuildForAsync(MigrationCommand.MigrateDown, MigrationRunMode.Migrate, "Release_2.0"); await ctx.MigrateDownAsync("Release_2.0"); ctx.AssertSuccess(true); var json = ctx.GetMigrationRunSettingsJson(); json.Should().NotBeNullOrEmpty("MigrationRunMeta should contain settings JSON after MigrateDown"); var doc = JsonDocument.Parse(json!); var repoConnStr = doc.RootElement.GetProperty("Repository").GetProperty("ConnectionString").GetString(); repoConnStr.Should().Contain(SensitiveDataMasker.MaskString, "Repository connection string should be masked in MigrateDown settings JSON"); var targetGroups = doc.RootElement.GetProperty("Product").GetProperty("TargetGroups"); foreach (var tg in targetGroups.EnumerateArray()) { foreach (var target in tg.GetProperty("Targets").EnumerateArray()) { var connStr = target.GetProperty("ConnectionString").GetString(); connStr.Should().Contain(SensitiveDataMasker.MaskString, $"Target [{target.GetProperty("Alias").GetString()}] connection string should be masked in MigrateDown settings JSON"); } } }

    [Fact] public async Task Baseline_SettingsJson_ShouldContainMaskedConnectionStrings() { Assert.SkipUnless(Fixture.IsDatabaseAvailable, "Docker not available"); await using var ctx = await CreateScenario().BuildAsync(MigrationCommand.Baseline); await ctx.BaselineAsync(); ctx.AssertSuccess(true); var json = ctx.GetMigrationRunSettingsJson(); json.Should().NotBeNullOrEmpty("MigrationRunMeta should contain settings JSON after Baseline"); var doc = JsonDocument.Parse(json!); var repoConnStr = doc.RootElement.GetProperty("Repository").GetProperty("ConnectionString").GetString(); repoConnStr.Should().Contain(SensitiveDataMasker.MaskString, "Repository connection string should be masked in Baseline settings JSON"); var targetGroups = doc.RootElement.GetProperty("Product").GetProperty("TargetGroups"); foreach (var tg in targetGroups.EnumerateArray()) { foreach (var target in tg.GetProperty("Targets").EnumerateArray()) { var connStr = target.GetProperty("ConnectionString").GetString(); connStr.Should().Contain(SensitiveDataMasker.MaskString, $"Target [{target.GetProperty("Alias").GetString()}] connection string should be masked in Baseline settings JSON"); } } }

}
