// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

using System.Data;
using System.Data.Common;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Raycoon.RayMigrator.Database.Common;
using Raycoon.RayMigrator.Database.SqlServer;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1: Tests for DalBase type mapping (G2), TryGetDbSpecificSqlParameter (G3),
/// and DalParameterList/DalParameter (G8).
/// </summary>
public class DalBaseTypeAndParameterTests
{
    /// <summary>
    /// Minimal concrete DalBase subclass for testing abstract base class behavior.
    /// </summary>
    private class TestDal : DalBase
    {
        public override string DatabaseType => "Test";
        public override DalSpecificProperties DalSpecificProperties => new();

        public override Task ExecuteNonQueryAsync(string sqlCode, IDalSettings dalSettings, DalParameterList? dalParameterList = null) => throw new NotImplementedException();
        public override void ExecuteNonQuery(string sqlCode, IDalSettings dalSettings, DalParameterList? dalParameterList = null) => throw new NotImplementedException();
        public override Task<object?> ExecuteScalarAsync(string sqlCode, IDalSettings dalSettings, DalParameterList? dalParameterList = null) => throw new NotImplementedException();
        public override Task<List<Dictionary<string, object?>>> ExecuteReaderAsync(string sqlCode, IDalSettings dalSettings, DalParameterList? dalParameterList = null) => throw new NotImplementedException();
        public override Task<bool> IsConnectionValid(string connectionString, IDalSettings dalSettings) => throw new NotImplementedException();
        public override void CheckConnectionStringOrValidateConnection(bool validateConnection) => throw new NotImplementedException();
        public override DbConnection CreateConnection() => throw new NotImplementedException();
        public override Task ExecuteNonQueryAsync(string sqlCode, DbConnection connection, DbTransaction transaction, int commandTimeoutInSeconds, DalParameterList? dalParameterList = null) => throw new NotImplementedException();
        public override Task<object?> ExecuteScalarAsync(string sqlCode, DbConnection connection, DbTransaction transaction, int commandTimeoutInSeconds, DalParameterList? dalParameterList = null) => throw new NotImplementedException();
    }

    #region TryGetDbTypeForType — Supported Types

    public static IEnumerable<object[]> SupportedTypeMappings => new List<object[]>
    {
        new object[] { typeof(byte), DbType.Byte },
        new object[] { typeof(sbyte), DbType.SByte },
        new object[] { typeof(short), DbType.Int16 },
        new object[] { typeof(ushort), DbType.UInt16 },
        new object[] { typeof(int), DbType.Int32 },
        new object[] { typeof(uint), DbType.UInt32 },
        new object[] { typeof(long), DbType.Int64 },
        new object[] { typeof(ulong), DbType.UInt64 },
        new object[] { typeof(float), DbType.Single },
        new object[] { typeof(double), DbType.Double },
        new object[] { typeof(decimal), DbType.Decimal },
        new object[] { typeof(bool), DbType.Boolean },
        new object[] { typeof(string), DbType.String },
        new object[] { typeof(char), DbType.StringFixedLength },
        new object[] { typeof(Guid), DbType.Guid },
        new object[] { typeof(DateTime), DbType.DateTime },
        new object[] { typeof(DateTimeOffset), DbType.DateTimeOffset },
        new object[] { typeof(byte[]), DbType.Binary },
        new object[] { typeof(System.Xml.Linq.XElement), DbType.Xml },
    };

    [Theory]
    [MemberData(nameof(SupportedTypeMappings))]
    public void TryGetDbTypeForType_SupportedTypes_ReturnsCorrectDbType(Type inputType, DbType expectedDbType)
    {
        var dal = new TestDal();

        var result = dal.TryGetDbTypeForType(inputType, out DbType dbType);

        result.Should().BeTrue();
        dbType.Should().Be(expectedDbType);
    }

    public static IEnumerable<object[]> NullableTypeMappings => new List<object[]>
    {
        new object[] { typeof(int?), DbType.Int32 },
        new object[] { typeof(DateTime?), DbType.DateTime },
        new object[] { typeof(Guid?), DbType.Guid },
        new object[] { typeof(bool?), DbType.Boolean },
        new object[] { typeof(double?), DbType.Double },
        new object[] { typeof(decimal?), DbType.Decimal },
    };

    [Theory]
    [MemberData(nameof(NullableTypeMappings))]
    public void TryGetDbTypeForType_NullableTypes_ReturnsUnderlyingDbType(Type inputType, DbType expectedDbType)
    {
        var dal = new TestDal();

        var result = dal.TryGetDbTypeForType(inputType, out DbType dbType);

        result.Should().BeTrue();
        dbType.Should().Be(expectedDbType);
    }

    [Fact]
    public void TryGetDbTypeForType_UnknownType_ReturnsFalse()
    {
        var dal = new TestDal();

        var result = dal.TryGetDbTypeForType(typeof(object), out DbType dbType);

        result.Should().BeFalse();
        dbType.Should().Be(default(DbType));
    }

    [Fact]
    public void TryGetDbTypeForType_CustomClass_ReturnsFalse()
    {
        var dal = new TestDal();

        var result = dal.TryGetDbTypeForType(typeof(DalParameter), out DbType dbType);

        result.Should().BeFalse();
        dbType.Should().Be(default(DbType));
    }

    #endregion

    #region TryGetDbSpecificSqlParameter (tested via DalSqlServer)

    [Fact]
    public void TryGetDbSpecificSqlParameter_ValidParameters_ReturnsTrue()
    {
        var dal = new DalSqlServer("Data Source=.;Initial Catalog=Test;TrustServerCertificate=True");
        var paramList = new DalParameterList();
        paramList.AddParameter(new DalParameter("Id", 42, typeof(int)));
        paramList.AddParameter(new DalParameter("Name", "test", typeof(string)));

        var result = dal.TryGetDbSpecificSqlParameter<SqlParameter>(paramList, out var sqlParams);

        result.Should().BeTrue();
        sqlParams.Should().NotBeNull();
        sqlParams.Should().HaveCount(2);
        sqlParams![0].ParameterName.Should().Be("Id");
        sqlParams[0].DbType.Should().Be(DbType.Int32);
        sqlParams[1].ParameterName.Should().Be("Name");
        sqlParams[1].DbType.Should().Be(DbType.String);
    }

    [Fact]
    public void TryGetDbSpecificSqlParameter_MultipleTypes_ConvertsAll()
    {
        var dal = new DalSqlServer("Data Source=.;Initial Catalog=Test;TrustServerCertificate=True");
        var paramList = new DalParameterList();
        paramList.AddParameter(new DalParameter("IntParam", 1, typeof(int)));
        paramList.AddParameter(new DalParameter("StringParam", "hello", typeof(string)));
        paramList.AddParameter(new DalParameter("BoolParam", true, typeof(bool)));
        paramList.AddParameter(new DalParameter("DateParam", new DateTime(2025, 1, 1), typeof(DateTime)));
        paramList.AddParameter(new DalParameter("GuidParam", Guid.Empty, typeof(Guid)));

        var result = dal.TryGetDbSpecificSqlParameter<SqlParameter>(paramList, out var sqlParams);

        result.Should().BeTrue();
        sqlParams.Should().HaveCount(5);
        sqlParams![0].DbType.Should().Be(DbType.Int32);
        sqlParams[1].DbType.Should().Be(DbType.String);
        sqlParams[2].DbType.Should().Be(DbType.Boolean);
        sqlParams[3].DbType.Should().Be(DbType.DateTime);
        sqlParams[4].DbType.Should().Be(DbType.Guid);
    }

    [Fact]
    public void TryGetDbSpecificSqlParameter_UnsupportedType_ThrowsApplicationException()
    {
        var dal = new DalSqlServer("Data Source=.;Initial Catalog=Test;TrustServerCertificate=True");
        var paramList = new DalParameterList();
        paramList.AddParameter(new DalParameter("BadParam", new object(), typeof(object)));

        var act = () => dal.TryGetDbSpecificSqlParameter<SqlParameter>(paramList, out var sqlParams);

        act.Should().Throw<ApplicationException>()
            .WithMessage("*Error converting*");
    }

    [Fact]
    public void TryGetDbSpecificSqlParameter_EmptyList_ReturnsEmptyCollection()
    {
        var dal = new DalSqlServer("Data Source=.;Initial Catalog=Test;TrustServerCertificate=True");
        var paramList = new DalParameterList();

        var result = dal.TryGetDbSpecificSqlParameter<SqlParameter>(paramList, out var sqlParams);

        result.Should().BeTrue();
        sqlParams.Should().NotBeNull();
        sqlParams.Should().BeEmpty();
    }

    #endregion

    #region DalParameterList

    [Fact]
    public void DalParameterList_AddAndRetrieve_ReturnsParameter()
    {
        var paramList = new DalParameterList();
        var param = new DalParameter("Id", 42, typeof(int));

        paramList.AddParameter(param);
        var result = paramList.TryGetValue("Id", out var retrieved);

        result.Should().BeTrue();
        retrieved.Should().BeSameAs(param);
    }

    [Fact]
    public void DalParameterList_AddDuplicate_ThrowsArgumentException()
    {
        var paramList = new DalParameterList();
        paramList.AddParameter(new DalParameter("Id", 1, typeof(int)));

        var act = () => paramList.AddParameter(new DalParameter("Id", 2, typeof(int)));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void DalParameterList_TryGetValue_NonExistent_ReturnsFalse()
    {
        var paramList = new DalParameterList();

        var result = paramList.TryGetValue("NotFound", out var retrieved);

        result.Should().BeFalse();
        retrieved.Should().BeNull();
    }

    [Fact]
    public void DalParameterList_GetAllParameters_ReturnsAll()
    {
        var paramList = new DalParameterList();
        paramList.AddParameter(new DalParameter("A", 1, typeof(int)));
        paramList.AddParameter(new DalParameter("B", "two", typeof(string)));
        paramList.AddParameter(new DalParameter("C", true, typeof(bool)));

        var all = paramList.GetAllParameters().ToList();

        all.Should().HaveCount(3);
    }

    [Fact]
    public void DalParameterList_GetAllParameters_Empty_ReturnsEmpty()
    {
        var paramList = new DalParameterList();

        var all = paramList.GetAllParameters().ToList();

        all.Should().BeEmpty();
    }

    #endregion

    #region DalParameter

    [Fact]
    public void DalParameter_ToString_WithValue_FormatsCorrectly()
    {
        var param = new DalParameter("test", 42, typeof(int));

        var result = param.ToString();

        result.Should().Be("42 (System.Int32)");
    }

    [Fact]
    public void DalParameter_ToString_NullValue_FormatsAsNULL()
    {
        var param = new DalParameter("test", null, typeof(string));

        var result = param.ToString();

        result.Should().Be("NULL (System.String)");
    }

    [Fact]
    public void DalParameter_Constructor_SetsAllProperties()
    {
        var param = new DalParameter("MyParam", "value", typeof(string));

        param.ParameterName.Should().Be("MyParam");
        param.ParameterValue.Should().Be("value");
        param.ParameterType.Should().Be(typeof(string));
    }

    #endregion
}
