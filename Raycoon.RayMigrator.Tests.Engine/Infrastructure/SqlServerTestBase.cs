// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

using Raycoon.RayMigrator.Tests.Engine.Fixtures;

namespace Raycoon.RayMigrator.Tests.Engine.Infrastructure;

/// <summary>
/// Base class for SQL Server engine tests.
/// Provides access to the fixture and a convenience method to create scenarios.
/// </summary>
public abstract class SqlServerTestBase
{
    protected readonly SqlServerFixture Fixture;

    protected SqlServerTestBase(SqlServerFixture fixture) => Fixture = fixture;

    protected ScenarioBuilder CreateScenario() => new(Fixture.EngineConfig);
}
