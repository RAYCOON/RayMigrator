using FluentAssertions;
using Microsoft.Data.Sqlite;
using Raycoon.RayMigrator.Database.Sqlite;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1: Tests for SQLite foreign-key enforcement wiring (DAL-001).
/// Verifies EnsureForeignKeysEnabled transforms the connection string so
/// Microsoft.Data.Sqlite issues PRAGMA foreign_keys = ON on open.
/// </summary>
public class DalSqliteForeignKeysTests
{
    [Fact]
    public void EnsureForeignKeysEnabled_NoSetting_AppendsForeignKeysTrue()
    {
        var input = "Data Source=:memory:";

        var result = DalSqlite.EnsureForeignKeysEnabled(input);

        new SqliteConnectionStringBuilder(result).ForeignKeys.Should().Be(true);
    }

    [Fact]
    public void EnsureForeignKeysEnabled_ExplicitFalse_KeepsFalse()
    {
        var input = "Data Source=:memory:;Foreign Keys=False";

        var result = DalSqlite.EnsureForeignKeysEnabled(input);

        new SqliteConnectionStringBuilder(result).ForeignKeys.Should().Be(false);
    }

    [Fact]
    public void EnsureForeignKeysEnabled_ExplicitTrue_KeepsTrue()
    {
        var input = "Data Source=:memory:;Foreign Keys=True";

        var result = DalSqlite.EnsureForeignKeysEnabled(input);

        new SqliteConnectionStringBuilder(result).ForeignKeys.Should().Be(true);
    }
}
