// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

﻿using System.Data;
using System.Data.Common;

namespace Raycoon.RayMigrator.Database.Common;

public abstract class DalBase : IDal
{
    public abstract string DatabaseType { get; }
    public abstract DalSpecificProperties DalSpecificProperties { get; }

    public abstract Task ExecuteNonQueryAsync(string sqlCode, IDalSettings dalSettings, DalParameterList? dalParameterList = null);
    public abstract void ExecuteNonQuery(string sqlCode, IDalSettings dalSettings, DalParameterList? dalParameterList = null);

    public abstract Task<object?> ExecuteScalarAsync(string sqlCode, IDalSettings dalSettings, DalParameterList? dalParameterList = null);

    public abstract Task<List<Dictionary<string, object?>>> ExecuteReaderAsync(string sqlCode, IDalSettings dalSettings, DalParameterList? dalParameterList = null);

    public abstract Task<bool> IsConnectionValid(string connectionString, IDalSettings dalSettings);

    public abstract void CheckConnectionStringOrValidateConnection(bool validateConnection);

    /// <inheritdoc />
    public abstract DbConnection CreateConnection();

    /// <inheritdoc />
    public abstract Task ExecuteNonQueryAsync(string sqlCode, DbConnection connection, DbTransaction transaction, int commandTimeoutInSeconds, DalParameterList? dalParameterList = null);

    /// <inheritdoc />
    public abstract Task<object?> ExecuteScalarAsync(string sqlCode, DbConnection connection, DbTransaction transaction, int commandTimeoutInSeconds, DalParameterList? dalParameterList = null);

    /// <summary>
    /// Determines if an exception represents a transient database error that can be retried.
    /// Override in each DAL to check database-specific exception types and error codes.
    /// The base implementation handles TimeoutException as a common transient error
    /// and recursively checks InnerException via virtual dispatch.
    /// </summary>
    public virtual (bool isTransient, string? errorCode) IsTransient(Exception ex)
    {
        if (ex is TimeoutException)
            return (true, null);

        return ex.InnerException != null ? IsTransient(ex.InnerException) : (false, null);
    }

    /// <summary>
    /// Executes an async operation with retry logic for transient errors.
    /// Uses this DAL's IsTransient method for transient error detection.
    /// </summary>
    protected async Task<T> ExecuteWithRetryAsync<T>(
        Func<Task<T>> operation, IDalSettings dalSettings, string? operationDescription = null)
    {
        if (dalSettings.MaxRetries > 0)
            return await RetryHelper.ExecuteWithRetryAsync(
                operation, dalSettings.MaxRetries, dalSettings.RetryDelayMs,
                IsTransient, operationDescription: operationDescription);
        return await operation();
    }

    /// <summary>
    /// Executes an async void operation with retry logic for transient errors.
    /// </summary>
    protected async Task ExecuteWithRetryAsync(
        Func<Task> operation, IDalSettings dalSettings, string? operationDescription = null)
    {
        if (dalSettings.MaxRetries > 0)
            await RetryHelper.ExecuteWithRetryAsync(
                operation, dalSettings.MaxRetries, dalSettings.RetryDelayMs,
                IsTransient, operationDescription: operationDescription);
        else
            await operation();
    }

    /// <summary>
    /// Executes a synchronous operation with retry logic for transient errors.
    /// </summary>
    protected void ExecuteWithRetry(
        Action operation, IDalSettings dalSettings, string? operationDescription = null)
    {
        if (dalSettings.MaxRetries > 0)
            RetryHelper.ExecuteWithRetry(
                () => { operation(); return true; },
                dalSettings.MaxRetries, dalSettings.RetryDelayMs,
                IsTransient, operationDescription: operationDescription);
        else
            operation();
    }

    public virtual bool TryGetDbSpecificSqlParameter<T>(DalParameterList dalParameterList, out List<T>? sqlParameterList) where T : class, IDbDataParameter, new()
    {
        try
        {
            List<T> sqlParameters = new List<T>();

            foreach (var dalParameter in dalParameterList.GetAllParameters())
            {
                Type dalParameterType = dalParameter.Value.ParameterType;

                if (!TryGetDbTypeForType(dalParameterType, out DbType dbType))
                {
                    throw new ApplicationException($"Error converting application parameter of type [{dalParameter.Value.ParameterType}] into a DAL-specific parameter for Type [{typeof(T).Name}].");
                }

                T parameter = CreateParameter<T>(dbType, dalParameter.Value.ParameterName, dalParameter.Value.ParameterValue);
                sqlParameters.Add(parameter);
            }

            sqlParameterList = sqlParameters;
            return true;
        }
        catch (Exception ex)
        {
            throw new ApplicationException($"Error converting application ParameterList into a DAL-specific parameters for Type [{typeof(T).Name}].", ex);
        }
    }

    protected virtual T CreateParameter<T>(DbType dbType, string parameterName, object? parameterValue) where T : class, IDbDataParameter, new()
    {
        return new T
        {
            DbType = dbType,
            ParameterName = parameterName,
            Value = ConvertToDbValue(parameterValue)
        };
    }

    protected virtual object ConvertToDbValue(object? value)
    {
        return value ?? DBNull.Value;
    }

    public bool TryGetDbTypeForType(Type type, out DbType dbType)
    {
        Type? underlyingType = Nullable.GetUnderlyingType(type);
        Type effectiveType = underlyingType ?? type;

        switch (effectiveType)
        {
            case Type t when t == typeof(byte) || t == typeof(byte?):
                dbType = DbType.Byte;
                return true;
            case Type t when t == typeof(sbyte) || t == typeof(sbyte?):
                dbType = DbType.SByte;
                return true;
            case Type t when t == typeof(short) || t == typeof(short?):
                dbType = DbType.Int16;
                return true;
            case Type t when t == typeof(ushort) || t == typeof(ushort?):
                dbType = DbType.UInt16;
                return true;
            case Type t when t == typeof(int) || t == typeof(int?):
                dbType = DbType.Int32;
                return true;
            case Type t when t == typeof(uint) || t == typeof(uint?):
                dbType = DbType.UInt32;
                return true;
            case Type t when t == typeof(long) || t == typeof(long?):
                dbType = DbType.Int64;
                return true;
            case Type t when t == typeof(ulong) || t == typeof(ulong?):
                dbType = DbType.UInt64;
                return true;
            case Type t when t == typeof(float) || t == typeof(float?):
                dbType = DbType.Single;
                return true;
            case Type t when t == typeof(double) || t == typeof(double?):
                dbType = DbType.Double;
                return true;
            case Type t when t == typeof(decimal) || t == typeof(decimal?):
                dbType = DbType.Decimal;
                return true;
            case Type t when t == typeof(bool) || t == typeof(bool?):
                dbType = DbType.Boolean;
                return true;
            case Type t when t == typeof(string):
                dbType = DbType.String;
                return true;
            case Type t when t == typeof(char) || t == typeof(char?):
                dbType = DbType.StringFixedLength;
                return true;
            case Type t when t == typeof(Guid) || t == typeof(Guid?):
                dbType = DbType.Guid;
                return true;
            case Type t when t == typeof(DateTime) || t == typeof(DateTime?):
                dbType = DbType.DateTime;
                return true;
            case Type t when t == typeof(DateTimeOffset) || t == typeof(DateTimeOffset?):
                dbType = DbType.DateTimeOffset;
                return true;
            case Type t when t == typeof(byte[]):
                dbType = DbType.Binary;
                return true;
            case Type t when t == typeof(System.Xml.Linq.XElement) || t == typeof(System.Xml.XmlDocument):
                dbType = DbType.Xml;
                return true;
            default:
                dbType = default;
                return false;
        }
    }
}