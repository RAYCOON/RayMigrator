using AwesomeAssertions;
using Raycoon.RayMigrator.Database.SqlServer;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1: SQL Server DAL classification of a pooled session the server killed because the login's state changed
/// after the session logged in (error 4021 followed by 596). Both numbers are retried and clear the connection pool.
/// </summary>
public class DalSqlServerKilledSessionTests
{
    // Constructed directly, so the instance carries the built-in list, not a loaded TransientErrorCodes.txt.
    private readonly DalSqlServer _dal = new("Server=test;");

    [Theory]
    [InlineData(4021)] // Resetting the connection results in a different state than the initial login
    [InlineData(596)]  // Cannot continue the execution because the session is in the kill state
    [InlineData(-2)]   // Timeout, present before
    [InlineData(233)]  // Connection closed during initialization, present before
    public void IsTransientErrorNumber_KilledSessionAndExistingCodes_ReturnsTrue(int errorNumber)
    {
        _dal.IsTransientErrorNumber(errorNumber).Should().BeTrue();
    }

    [Theory]
    [InlineData(18456)] // Login failed for user: a fresh login with a wrong password is not transient
    [InlineData(18470)] // Login disabled
    [InlineData(4060)]  // Cannot open database requested by the login
    [InlineData(102)]   // Syntax error
    public void IsTransientErrorNumber_PermanentLoginAndSqlErrors_ReturnsFalse(int errorNumber)
    {
        _dal.IsTransientErrorNumber(errorNumber).Should().BeFalse();
    }

    [Fact]
    public void RequiresPoolClear_ChainRaisedForStalePooledSession_ReturnsTrue()
    {
        // The exact chain SQL Server sends when sp_reset_connection finds the login state changed:
        // 4021, 18456 (Login failed for user), 596 (kill state), 0 (SqlClient: severe error, discard results).
        DalSqlServer.RequiresPoolClear([4021, 18456, 596, 0]).Should().BeTrue();
    }

    [Fact]
    public void RequiresPoolClear_SessionKilledMidBatch_ReturnsTrue()
    {
        DalSqlServer.RequiresPoolClear([596]).Should().BeTrue();
    }

    [Theory]
    [InlineData(new int[0])]
    [InlineData(new[] { 18456 })]    // Login failed on a fresh connection: the pool is not the problem
    [InlineData(new[] { 233, 0 })]   // Transient network error: SqlClient already discards that one connection
    [InlineData(new[] { -2 })]
    public void RequiresPoolClear_OtherErrors_ReturnsFalse(int[] errorNumbers)
    {
        DalSqlServer.RequiresPoolClear(errorNumbers).Should().BeFalse();
    }
}
