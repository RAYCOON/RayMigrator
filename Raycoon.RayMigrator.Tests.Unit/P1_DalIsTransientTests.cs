using System.Data.Common;
using FluentAssertions;
using Raycoon.RayMigrator.Database.Common;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1: Tests for DalBase.IsTransient base implementation.
/// Validates common transient detection logic (TimeoutException, InnerException recursion).
/// </summary>
public class DalIsTransientTests
{
    /// <summary>
    /// Minimal DalBase subclass for testing the base IsTransient implementation.
    /// </summary>
    private class TestDal : DalBase
    {
        public override string DatabaseType => "Test";
        public override DalSpecificProperties DalSpecificProperties => new();
        public override void CheckConnectionStringOrValidateConnection(bool validateConnection) => throw new NotImplementedException();
        public override Task ExecuteNonQueryAsync(string sqlCode, IDalSettings dalSettings, DalParameterList? dalParameterList = null) => throw new NotImplementedException();
        public override void ExecuteNonQuery(string sqlCode, IDalSettings dalSettings, DalParameterList? dalParameterList = null) => throw new NotImplementedException();
        public override Task<object?> ExecuteScalarAsync(string sqlCode, IDalSettings dalSettings, DalParameterList? dalParameterList = null) => throw new NotImplementedException();
        public override Task<List<Dictionary<string, object?>>> ExecuteReaderAsync(string sqlCode, IDalSettings dalSettings, DalParameterList? dalParameterList = null) => throw new NotImplementedException();
        public override Task<bool> IsConnectionValid(string connectionString, IDalSettings dalSettings) => throw new NotImplementedException();
        public override DbConnection CreateConnection() => throw new NotImplementedException();
        public override Task ExecuteNonQueryAsync(string sqlCode, DbConnection connection, DbTransaction transaction, int commandTimeoutInSeconds, DalParameterList? dalParameterList = null) => throw new NotImplementedException();
        public override Task<object?> ExecuteScalarAsync(string sqlCode, DbConnection connection, DbTransaction transaction, int commandTimeoutInSeconds, DalParameterList? dalParameterList = null) => throw new NotImplementedException();
    }

    private readonly TestDal _dal = new();

    [Fact]
    public void IsTransient_TimeoutException_ReturnsTrue()
    {
        var result = _dal.IsTransient(new TimeoutException());
        result.isTransient.Should().BeTrue();
        result.errorCode.Should().BeNull();
    }

    [Fact]
    public void IsTransient_OperationCanceledException_ReturnsFalse()
    {
        var result = _dal.IsTransient(new OperationCanceledException());
        result.isTransient.Should().BeFalse();
        result.errorCode.Should().BeNull();
    }

    [Fact]
    public void IsTransient_InvalidOperationException_ReturnsFalse()
    {
        var result = _dal.IsTransient(new InvalidOperationException());
        result.isTransient.Should().BeFalse();
        result.errorCode.Should().BeNull();
    }

    [Fact]
    public void IsTransient_InnerTimeoutException_ReturnsTrue()
    {
        var inner = new TimeoutException("timeout");
        var outer = new ApplicationException("wrapper", inner);

        var result = _dal.IsTransient(outer);
        result.isTransient.Should().BeTrue();
        result.errorCode.Should().BeNull();
    }

    [Fact]
    public void IsTransient_NestedNonTransient_ReturnsFalse()
    {
        var inner = new InvalidOperationException("not transient");
        var outer = new ApplicationException("wrapper", inner);

        var result = _dal.IsTransient(outer);
        result.isTransient.Should().BeFalse();
    }

    [Fact]
    public void IsTransient_DeeplyNestedTimeoutException_ReturnsTrue()
    {
        var timeout = new TimeoutException("deep timeout");
        var mid = new InvalidOperationException("mid", timeout);
        var outer = new ApplicationException("outer", mid);

        var result = _dal.IsTransient(outer);
        result.isTransient.Should().BeTrue();
    }
}
