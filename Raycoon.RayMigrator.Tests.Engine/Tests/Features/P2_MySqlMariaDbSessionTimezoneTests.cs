// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

using Raycoon.RayMigrator.Database;
using Raycoon.RayMigrator.Database.Common;
using Raycoon.RayMigrator.Tests.Engine.Fixtures;

namespace Raycoon.RayMigrator.Tests.Engine.Tests.Features;

/// <summary>
/// P2: Engine tests for DAL-014's session time-zone enforcement and TIMESTAMP round-trip.
/// Verifies that:
///   - Every DAL execute method (ExecuteNonQueryAsync, ExecuteScalarAsync, ExecuteReaderAsync)
///     issues <c>SET time_zone = '+00:00';</c> immediately after opening a connection,
///     so that any subsequent SQL runs with session tz pinned to UTC.
///   - <c>DateTime.UtcNow</c> written into a <c>TIMESTAMP</c> column round-trips back with
///     millisecond fidelity.
/// </summary>
[Collection("MySql")]
[Trait("Engine", "MySql")]
[Trait("Category", "Features")]
public class MySqlSessionTimezoneTests : MySqlTestBase
{
    private static readonly DalSettings Settings = new()
    {
        UseTransaction = false,
        DbCommandTimeoutInSeconds = 30
    };

    public MySqlSessionTimezoneTests(MySqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task ExecuteScalarAsync_SessionTimeZone_IsUtc()
    {
        if (!Fixture.IsDatabaseAvailable) return;

        DalFactory.TryGetDal("MySql", Fixture.EngineConfig.ConnectionString, out IDal? dal)
            .Should().BeTrue("the MySql DAL must be discoverable by DalFactory");

        var result = await dal!.ExecuteScalarAsync("SELECT @@session.time_zone", Settings);

        result.Should().NotBeNull("SELECT @@session.time_zone must return a non-null value");
        result!.ToString().Should().Be("+00:00",
            "DalMySql.ExecuteScalarAsync must pin session time_zone to '+00:00' after DAL-014");
    }

    [Fact]
    public async Task TimestampColumn_UtcNow_RoundTripsWithinTwoMilliseconds()
    {
        if (!Fixture.IsDatabaseAvailable) return;

        DalFactory.TryGetDal("MySql", Fixture.EngineConfig.ConnectionString, out IDal? dal)
            .Should().BeTrue("the MySql DAL must be discoverable by DalFactory");

        const string tableName = "_dal014_tz_test";

        await dal!.ExecuteNonQueryAsync($"DROP TABLE IF EXISTS `{tableName}`;", Settings);
        await dal.ExecuteNonQueryAsync(
            $"CREATE TABLE `{tableName}` (`Id` INT NOT NULL AUTO_INCREMENT, `Ts` TIMESTAMP(3) NOT NULL, PRIMARY KEY (`Id`)) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;",
            Settings);

        try
        {
            var written = DateTime.UtcNow;
            var writtenText = written.ToString("yyyy-MM-dd HH:mm:ss.fff");

            await dal.ExecuteNonQueryAsync(
                $"INSERT INTO `{tableName}` (`Ts`) VALUES ('{writtenText}');",
                Settings);

            var obj = await dal.ExecuteScalarAsync(
                $"SELECT `Ts` FROM `{tableName}` ORDER BY `Id` DESC LIMIT 1;",
                Settings);

            obj.Should().NotBeNull("the inserted TIMESTAMP row must be readable");
            var read = Convert.ToDateTime(obj!);

            var deltaMs = Math.Abs((read - written).TotalMilliseconds);
            deltaMs.Should().BeLessThan(2.0,
                $"TIMESTAMP round-trip must be within ~1 ms when session time_zone='+00:00'. written={written:O}, read={read:O}, deltaMs={deltaMs}");
        }
        finally
        {
            await dal.ExecuteNonQueryAsync($"DROP TABLE IF EXISTS `{tableName}`;", Settings);
        }
    }
}

[Collection("MariaDb")]
[Trait("Engine", "MariaDb")]
[Trait("Category", "Features")]
public class MariaDbSessionTimezoneTests : MariaDbTestBase
{
    private static readonly DalSettings Settings = new()
    {
        UseTransaction = false,
        DbCommandTimeoutInSeconds = 30
    };

    public MariaDbSessionTimezoneTests(MariaDbFixture fixture) : base(fixture) { }

    [Fact]
    public async Task ExecuteScalarAsync_SessionTimeZone_IsUtc()
    {
        if (!Fixture.IsDatabaseAvailable) return;

        DalFactory.TryGetDal("MariaDb", Fixture.EngineConfig.ConnectionString, out IDal? dal)
            .Should().BeTrue("the MariaDb DAL must be discoverable by DalFactory");

        var result = await dal!.ExecuteScalarAsync("SELECT @@session.time_zone", Settings);

        result.Should().NotBeNull("SELECT @@session.time_zone must return a non-null value");
        result!.ToString().Should().Be("+00:00",
            "DalMariaDb.ExecuteScalarAsync must pin session time_zone to '+00:00' after DAL-014");
    }

    [Fact]
    public async Task TimestampColumn_UtcNow_RoundTripsWithinTwoMilliseconds()
    {
        if (!Fixture.IsDatabaseAvailable) return;

        DalFactory.TryGetDal("MariaDb", Fixture.EngineConfig.ConnectionString, out IDal? dal)
            .Should().BeTrue("the MariaDb DAL must be discoverable by DalFactory");

        const string tableName = "_dal014_tz_test";

        await dal!.ExecuteNonQueryAsync($"DROP TABLE IF EXISTS `{tableName}`;", Settings);
        await dal.ExecuteNonQueryAsync(
            $"CREATE TABLE `{tableName}` (`Id` INT NOT NULL AUTO_INCREMENT, `Ts` TIMESTAMP(3) NOT NULL, PRIMARY KEY (`Id`)) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;",
            Settings);

        try
        {
            var written = DateTime.UtcNow;
            var writtenText = written.ToString("yyyy-MM-dd HH:mm:ss.fff");

            await dal.ExecuteNonQueryAsync(
                $"INSERT INTO `{tableName}` (`Ts`) VALUES ('{writtenText}');",
                Settings);

            var obj = await dal.ExecuteScalarAsync(
                $"SELECT `Ts` FROM `{tableName}` ORDER BY `Id` DESC LIMIT 1;",
                Settings);

            obj.Should().NotBeNull("the inserted TIMESTAMP row must be readable");
            var read = Convert.ToDateTime(obj!);

            var deltaMs = Math.Abs((read - written).TotalMilliseconds);
            deltaMs.Should().BeLessThan(2.0,
                $"TIMESTAMP round-trip must be within ~1 ms when session time_zone='+00:00'. written={written:O}, read={read:O}, deltaMs={deltaMs}");
        }
        finally
        {
            await dal.ExecuteNonQueryAsync($"DROP TABLE IF EXISTS `{tableName}`;", Settings);
        }
    }
}
