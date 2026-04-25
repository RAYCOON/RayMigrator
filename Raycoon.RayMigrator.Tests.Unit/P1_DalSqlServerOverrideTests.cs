using System.Data;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Raycoon.RayMigrator.Database.Common;
using Raycoon.RayMigrator.Database.SqlServer;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1: Tests for SQL Server-specific overrides (G7).
/// Validates ConvertToDbValue DateTime clamping and CreateParameter Size logic.
/// Tests through the public TryGetDbSpecificSqlParameter method which calls
/// CreateParameter → ConvertToDbValue internally.
/// </summary>
public class DalSqlServerOverrideTests
{
    private static DalSqlServer CreateDal() => new("Data Source=.;Initial Catalog=Test;TrustServerCertificate=True");

    #region ConvertToDbValue (tested via SqlParameter.Value)

    [Fact]
    public void ConvertToDbValue_NullValue_ReturnsDBNull()
    {
        var dal = CreateDal();
        var paramList = new DalParameterList();
        paramList.AddParameter(new DalParameter("Val", null, typeof(string)));

        dal.TryGetDbSpecificSqlParameter<SqlParameter>(paramList, out var sqlParams);

        sqlParams![0].Value.Should().Be(DBNull.Value);
    }

    [Fact]
    public void ConvertToDbValue_NormalDateTime_ReturnsUnchanged()
    {
        var dal = CreateDal();
        var date = new DateTime(2025, 1, 1);
        var paramList = new DalParameterList();
        paramList.AddParameter(new DalParameter("Val", date, typeof(DateTime)));

        dal.TryGetDbSpecificSqlParameter<SqlParameter>(paramList, out var sqlParams);

        sqlParams![0].Value.Should().Be(date);
    }

    [Fact]
    public void ConvertToDbValue_DateBefore1753_ClampsTo1753()
    {
        var dal = CreateDal();
        var paramList = new DalParameterList();
        paramList.AddParameter(new DalParameter("Val", new DateTime(1000, 1, 1), typeof(DateTime)));

        dal.TryGetDbSpecificSqlParameter<SqlParameter>(paramList, out var sqlParams);

        sqlParams![0].Value.Should().Be(new DateTime(1753, 1, 1));
    }

    [Fact]
    public void ConvertToDbValue_ExactBoundary1753_ReturnsUnchanged()
    {
        var dal = CreateDal();
        var boundaryDate = new DateTime(1753, 1, 1);
        var paramList = new DalParameterList();
        paramList.AddParameter(new DalParameter("Val", boundaryDate, typeof(DateTime)));

        dal.TryGetDbSpecificSqlParameter<SqlParameter>(paramList, out var sqlParams);

        sqlParams![0].Value.Should().Be(boundaryDate);
    }

    [Fact]
    public void ConvertToDbValue_DateTimeMinValue_ClampsTo1753()
    {
        var dal = CreateDal();
        var paramList = new DalParameterList();
        paramList.AddParameter(new DalParameter("Val", DateTime.MinValue, typeof(DateTime)));

        dal.TryGetDbSpecificSqlParameter<SqlParameter>(paramList, out var sqlParams);

        sqlParams![0].Value.Should().Be(new DateTime(1753, 1, 1));
    }

    [Fact]
    public void ConvertToDbValue_NonDateTimeValue_PassesThrough()
    {
        var dal = CreateDal();
        var paramList = new DalParameterList();
        paramList.AddParameter(new DalParameter("StrVal", "hello", typeof(string)));
        paramList.AddParameter(new DalParameter("IntVal", 42, typeof(int)));

        dal.TryGetDbSpecificSqlParameter<SqlParameter>(paramList, out var sqlParams);

        sqlParams![0].Value.Should().Be("hello");
        sqlParams[1].Value.Should().Be(42);
    }

    #endregion

    #region CreateParameter Size logic

    [Fact]
    public void CreateParameter_StringValue_SetsSizeToStringLength()
    {
        var dal = CreateDal();
        var paramList = new DalParameterList();
        paramList.AddParameter(new DalParameter("Name", "hello", typeof(string)));

        dal.TryGetDbSpecificSqlParameter<SqlParameter>(paramList, out var sqlParams);

        sqlParams![0].Size.Should().Be(5);
    }

    [Fact]
    public void CreateParameter_EmptyString_SetsSizeToOne()
    {
        var dal = CreateDal();
        var paramList = new DalParameterList();
        paramList.AddParameter(new DalParameter("Name", "", typeof(string)));

        dal.TryGetDbSpecificSqlParameter<SqlParameter>(paramList, out var sqlParams);

        sqlParams![0].Size.Should().Be(1); // Math.Max(1, 0)
    }

    [Fact]
    public void CreateParameter_NullStringValue_DoesNotSetSize()
    {
        var dal = CreateDal();
        var paramList = new DalParameterList();
        paramList.AddParameter(new DalParameter("Name", null, typeof(string)));

        dal.TryGetDbSpecificSqlParameter<SqlParameter>(paramList, out var sqlParams);

        // When value is null, the size adjustment branch is skipped
        sqlParams![0].Size.Should().Be(0);
    }

    [Fact]
    public void CreateParameter_NonStringDbType_DoesNotSetSize()
    {
        var dal = CreateDal();
        var paramList = new DalParameterList();
        paramList.AddParameter(new DalParameter("Count", 42, typeof(int)));

        dal.TryGetDbSpecificSqlParameter<SqlParameter>(paramList, out var sqlParams);

        sqlParams![0].Size.Should().Be(0); // Default, not adjusted
    }

    #endregion
}
