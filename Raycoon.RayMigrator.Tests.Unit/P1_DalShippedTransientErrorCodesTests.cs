using AwesomeAssertions;
using Raycoon.RayMigrator.Database.Common;
using Raycoon.RayMigrator.Database.MariaDb;
using Raycoon.RayMigrator.Database.MySql;
using Raycoon.RayMigrator.Database.PostgreSQL;
using Raycoon.RayMigrator.Database.Sqlite;
using Raycoon.RayMigrator.Database.SqlServer;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1: Drift guard between the TransientErrorCodes.txt each DAL project ships (linked into
/// DataAccessLayers/{Type}/ of every consumer's output) and the built-in list in its DalBase subclass (ADR-022).
/// </summary>
public class DalShippedTransientErrorCodesTests
{
    [Theory]
    [InlineData("SqlServer")]
    [InlineData("PostgreSQL")]
    [InlineData("MariaDb")]
    [InlineData("MySql")]
    [InlineData("Sqlite")]
    public void ShippedFile_MatchesTheBuiltInListOfTheDal(string databaseType)
    {
        // Arrange: a DAL constructed directly carries the built-in list; only DalFactory loads the file.
        string path = TransientErrorCodesFile.GetPath(AppContext.BaseDirectory, databaseType);
        File.Exists(path).Should().BeTrue($"the {databaseType} DAL project ships {TransientErrorCodesFile.FileName} as linked Content");
        var builtIn = CreateDal(databaseType).TransientErrorCodes;

        // Act
        var shipped = TransientErrorCodesFile.Parse(File.ReadLines(path));

        // Assert
        shipped.Should().OnlyHaveUniqueItems();
        shipped.Should().BeEquivalentTo(builtIn, because: "the shipped file must not drift from the built-in list of {0}", databaseType);
    }

    private static DalBase CreateDal(string databaseType) => databaseType switch
    {
        "SqlServer" => new DalSqlServer("Server=test;"),
        "PostgreSQL" => new DalPostgreSql("Host=test"),
        "MariaDb" => new DalMariaDb("Server=test"),
        "MySql" => new DalMySql("Server=test"),
        "Sqlite" => new DalSqlite("Data Source=:memory:"),
        _ => throw new ArgumentOutOfRangeException(nameof(databaseType), databaseType, "Unknown DatabaseType")
    };
}
