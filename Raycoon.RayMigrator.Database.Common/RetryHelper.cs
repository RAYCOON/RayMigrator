// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

namespace Raycoon.RayMigrator.Database.Common;

/// <summary>
/// Provides retry logic for database operations that may fail due to transient errors.
/// Transient error detection is delegated to each DAL via the isTransientPredicate parameter.
/// </summary>
public static class RetryHelper
{
    /// <summary>
    /// Callback delegate for logging retry attempts.
    /// </summary>
    /// <param name="attempt">Current attempt number.</param>
    /// <param name="maxAttempts">Maximum attempts allowed.</param>
    /// <param name="errorCode">The error code that triggered the retry.</param>
    /// <param name="operationDescription">Description of the operation.</param>
    /// <param name="delayMs">Delay before next retry in milliseconds.</param>
    public delegate void RetryLogCallback(int attempt, int maxAttempts, string? errorCode, string operationDescription, int delayMs);

    /// <summary>
    /// Executes an async operation with retry logic using a transient error predicate.
    /// </summary>
    /// <typeparam name="T">The return type of the operation.</typeparam>
    /// <param name="operation">The async operation to execute.</param>
    /// <param name="maxRetries">Maximum number of retry attempts. Set to 0 to disable retries.</param>
    /// <param name="retryDelayMs">Base delay in milliseconds between retries (uses linear backoff).</param>
    /// <param name="isTransientPredicate">Function that evaluates whether an exception is transient. Returns (isTransient, errorCode). The errorCode is a string to support both numeric codes (e.g. SQL Server "233") and SQLSTATE codes (e.g. PostgreSQL "08000").</param>
    /// <param name="onRetry">Optional callback for logging retry attempts.</param>
    /// <param name="operationDescription">Optional description of the operation for logging.</param>
    /// <returns>The result of the operation.</returns>
    /// <exception cref="RetryExhaustedException">Thrown when all retry attempts are exhausted.</exception>
    public static async Task<T> ExecuteWithRetryAsync<T>(
        Func<Task<T>> operation,
        int maxRetries,
        int retryDelayMs,
        Func<Exception, (bool isTransient, string? errorCode)> isTransientPredicate,
        RetryLogCallback? onRetry = null,
        string? operationDescription = null)
    {
        if (maxRetries < 0)
        {
            maxRetries = 0;
        }

        int attempt = 0;
        string? lastErrorCode = null;

        while (true)
        {
            try
            {
                return await operation();
            }
            catch (Exception ex)
            {
                var (isTransient, errorCode) = isTransientPredicate(ex);
                lastErrorCode = errorCode;

                if (isTransient && attempt < maxRetries)
                {
                    attempt++;
                    var delay = retryDelayMs * attempt; // Linear backoff

                    onRetry?.Invoke(attempt, maxRetries, lastErrorCode, operationDescription ?? "database operation", delay);

                    await Task.Delay(delay);
                }
                else if (isTransient && attempt >= maxRetries)
                {
                    throw new RetryExhaustedException(
                        $"Operation '{operationDescription ?? "database operation"}' failed after {attempt + 1} attempts. Last error code: {lastErrorCode}.",
                        attempt + 1,
                        lastErrorCode,
                        ex);
                }
                else
                {
                    throw; // Non-transient exception
                }
            }
        }
    }

    /// <summary>
    /// Executes an async operation (returning void) with retry logic using a transient error predicate.
    /// </summary>
    public static async Task ExecuteWithRetryAsync(
        Func<Task> operation,
        int maxRetries,
        int retryDelayMs,
        Func<Exception, (bool isTransient, string? errorCode)> isTransientPredicate,
        RetryLogCallback? onRetry = null,
        string? operationDescription = null)
    {
        await ExecuteWithRetryAsync(async () =>
        {
            await operation();
            return true; // Dummy return value
        }, maxRetries, retryDelayMs, isTransientPredicate, onRetry, operationDescription);
    }

    /// <summary>
    /// Executes a synchronous operation with retry logic using a transient error predicate.
    /// </summary>
    public static T ExecuteWithRetry<T>(
        Func<T> operation,
        int maxRetries,
        int retryDelayMs,
        Func<Exception, (bool isTransient, string? errorCode)> isTransientPredicate,
        RetryLogCallback? onRetry = null,
        string? operationDescription = null)
    {
        if (maxRetries < 0)
        {
            maxRetries = 0;
        }

        int attempt = 0;
        string? lastErrorCode = null;

        while (true)
        {
            try
            {
                return operation();
            }
            catch (Exception ex)
            {
                var (isTransient, errorCode) = isTransientPredicate(ex);
                lastErrorCode = errorCode;

                if (isTransient && attempt < maxRetries)
                {
                    attempt++;
                    var delay = retryDelayMs * attempt; // Linear backoff

                    onRetry?.Invoke(attempt, maxRetries, lastErrorCode, operationDescription ?? "database operation", delay);

                    Thread.Sleep(delay);
                }
                else if (isTransient && attempt >= maxRetries)
                {
                    throw new RetryExhaustedException(
                        $"Operation '{operationDescription ?? "database operation"}' failed after {attempt + 1} attempts. Last error code: {lastErrorCode}.",
                        attempt + 1,
                        lastErrorCode,
                        ex);
                }
                else
                {
                    throw; // Non-transient exception
                }
            }
        }
    }
}

/// <summary>
/// Exception thrown when all retry attempts for a transient database error have been exhausted.
/// </summary>
public class RetryExhaustedException : Exception
{
    /// <summary>
    /// The number of retry attempts made before giving up.
    /// </summary>
    public int AttemptsMade { get; }

    /// <summary>
    /// The database-specific error code of the last failure.
    /// </summary>
    public string? LastErrorCode { get; }

    public RetryExhaustedException(string message) : base(message) { }

    public RetryExhaustedException(string message, int attemptsMade) : base(message)
    {
        AttemptsMade = attemptsMade;
    }

    public RetryExhaustedException(string message, int attemptsMade, string? lastErrorCode) : base(message)
    {
        AttemptsMade = attemptsMade;
        LastErrorCode = lastErrorCode;
    }

    public RetryExhaustedException(string message, int attemptsMade, Exception innerException) : base(message, innerException)
    {
        AttemptsMade = attemptsMade;
    }

    public RetryExhaustedException(string message, int attemptsMade, string? lastErrorCode, Exception innerException) : base(message, innerException)
    {
        AttemptsMade = attemptsMade;
        LastErrorCode = lastErrorCode;
    }
}
