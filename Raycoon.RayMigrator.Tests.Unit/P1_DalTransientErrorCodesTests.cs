using System.Data.Common;
using AwesomeAssertions;
using Raycoon.RayMigrator.Database.Common;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1: Tests for the transient error code list of DalBase (ADR-022): the built-in defaults are effective until
/// SetTransientErrorCodes replaces them completely, codes are compared case-insensitively, and IsTransientCode
/// classifies a provider code against the effective list.
/// </summary>
public class DalTransientErrorCodesTests
{
    /// <summary>Minimal DalBase subclass with two built-in codes and a public entry to the protected classifier.</summary>
    private sealed class TestDal : DalBase
    {
        protected override IReadOnlyCollection<string> DefaultTransientErrorCodes => ["1205", "57P01"];
        public (bool isTransient, string? errorCode) Classify(string? code) => IsTransientCode(code);

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

    [Fact]
    public void TransientErrorCodes_BeforeAnySet_AreTheBuiltInDefaults()
    {
        // Arrange
        var dal = new TestDal();

        // Assert
        dal.TransientErrorCodes.Should().BeEquivalentTo(["1205", "57P01"]);
        dal.TransientErrorCodesSource.Should().BeNull(because: "the built-in list has no file source");
    }

    [Theory]
    [InlineData("1205", true)]
    [InlineData("57P01", true)]
    [InlineData("57p01", true)]
    [InlineData("9999", false)]
    public void IsTransientCode_ComparesAgainstTheEffectiveListCaseInsensitively(string code, bool expected)
    {
        // Arrange
        var dal = new TestDal();

        // Act
        var result = dal.Classify(code);

        // Assert
        result.isTransient.Should().Be(expected);
        result.errorCode.Should().Be(code, because: "the provider code is reported for the retry log");
    }

    [Fact]
    public void IsTransientCode_NullCode_IsNotTransient()
    {
        // Act
        var result = new TestDal().Classify(null);

        // Assert
        result.Should().Be((false, (string?)null));
    }

    [Fact]
    public void SetTransientErrorCodes_ReplacesTheBuiltInList()
    {
        // Arrange
        var dal = new TestDal();

        // Act
        dal.SetTransientErrorCodes(["4021", "596"], "/app/DataAccessLayers/Test/TransientErrorCodes.txt");

        // Assert
        dal.TransientErrorCodes.Should().BeEquivalentTo(["4021", "596"], because: "the file is the complete list and does not merge");
        dal.Classify("1205").isTransient.Should().BeFalse(because: "a built-in code not listed in the file is no longer retried");
        dal.Classify("4021").isTransient.Should().BeTrue();
        dal.TransientErrorCodesSource.Should().Be("/app/DataAccessLayers/Test/TransientErrorCodes.txt");
    }

    [Fact]
    public void SetTransientErrorCodes_EmptyList_RetriesNothing()
    {
        // Arrange
        var dal = new TestDal();

        // Act
        dal.SetTransientErrorCodes([], "/app/DataAccessLayers/Test/TransientErrorCodes.txt");

        // Assert
        dal.TransientErrorCodes.Should().BeEmpty();
        dal.Classify("1205").isTransient.Should().BeFalse();
    }

    [Fact]
    public void SetTransientErrorCodes_NullSource_ClearsTheSource()
    {
        // Arrange
        var dal = new TestDal();
        dal.SetTransientErrorCodes(["1"], "/some/file");

        // Act
        dal.SetTransientErrorCodes(["2"], null);

        // Assert
        dal.TransientErrorCodesSource.Should().BeNull();
        dal.TransientErrorCodes.Should().BeEquivalentTo(["2"]);
    }

    [Fact]
    public void SetTransientErrorCodes_NullCodes_Throws()
    {
        // Act
        Action act = () => new TestDal().SetTransientErrorCodes(null!, null);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }
}
