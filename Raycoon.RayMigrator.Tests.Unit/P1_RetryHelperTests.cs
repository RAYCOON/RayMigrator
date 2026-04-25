
using FluentAssertions;
using Raycoon.RayMigrator.Database.Common;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1: Tests for RetryHelper integration (O1).
/// Validates retry logic for transient/non-transient exceptions, async/sync paths, and callback invocation.
/// </summary>
public class RetryHelperTests
{
    private static readonly Func<Exception, (bool isTransient, string? errorCode)> TransientPredicate =
        ex => ex is TimeoutException ? (true, null) : (false, null);

    [Fact]
    public async Task RetryHelper_AsyncWithReturnValue_SucceedsOnFirstAttempt()
    {
        var callCount = 0;

        var result = await RetryHelper.ExecuteWithRetryAsync(
            () => { callCount++; return Task.FromResult(42); },
            maxRetries: 3, retryDelayMs: 10, isTransientPredicate: TransientPredicate);

        result.Should().Be(42);
        callCount.Should().Be(1);
    }

    [Fact]
    public async Task RetryHelper_AsyncVoid_SucceedsOnFirstAttempt()
    {
        var callCount = 0;

        await RetryHelper.ExecuteWithRetryAsync(
            () => { callCount++; return Task.CompletedTask; },
            maxRetries: 3, retryDelayMs: 10, isTransientPredicate: TransientPredicate);

        callCount.Should().Be(1);
    }

    [Fact]
    public void RetryHelper_Sync_SucceedsOnFirstAttempt()
    {
        var callCount = 0;

        var result = RetryHelper.ExecuteWithRetry(
            () => { callCount++; return 42; },
            maxRetries: 3, retryDelayMs: 10, isTransientPredicate: TransientPredicate);

        result.Should().Be(42);
        callCount.Should().Be(1);
    }

    [Fact]
    public async Task RetryHelper_AsyncWithReturnValue_RetriesOnTransientError_ThenSucceeds()
    {
        var callCount = 0;

        var result = await RetryHelper.ExecuteWithRetryAsync(
            () =>
            {
                callCount++;
                if (callCount == 1)
                    throw new TimeoutException("Simulated timeout");
                return Task.FromResult(42);
            },
            maxRetries: 3, retryDelayMs: 10, isTransientPredicate: TransientPredicate);

        result.Should().Be(42);
        callCount.Should().Be(2);
    }

    [Fact]
    public async Task RetryHelper_AsyncVoid_RetriesOnTransientError_ThenSucceeds()
    {
        var callCount = 0;

        await RetryHelper.ExecuteWithRetryAsync(
            () =>
            {
                callCount++;
                if (callCount == 1)
                    throw new TimeoutException("Simulated timeout");
                return Task.CompletedTask;
            },
            maxRetries: 3, retryDelayMs: 10, isTransientPredicate: TransientPredicate);

        callCount.Should().Be(2);
    }

    [Fact]
    public void RetryHelper_Sync_RetriesOnTransientError_ThenSucceeds()
    {
        var callCount = 0;

        var result = RetryHelper.ExecuteWithRetry(
            () =>
            {
                callCount++;
                if (callCount == 1)
                    throw new TimeoutException("Simulated timeout");
                return 42;
            },
            maxRetries: 3, retryDelayMs: 10, isTransientPredicate: TransientPredicate);

        result.Should().Be(42);
        callCount.Should().Be(2);
    }

    [Fact]
    public async Task RetryHelper_ThrowsRetryExhaustedException_AfterAllRetriesExhausted()
    {
        var callCount = 0;

        var act = async () => await RetryHelper.ExecuteWithRetryAsync(
            () => { callCount++; return ThrowAndReturn<int>(new TimeoutException("Simulated timeout")); },
            maxRetries: 2, retryDelayMs: 10, isTransientPredicate: TransientPredicate);

        var ex = await act.Should().ThrowAsync<RetryExhaustedException>();
        ex.Which.AttemptsMade.Should().Be(3); // 1 initial + 2 retries
        callCount.Should().Be(3);
    }

    [Fact]
    public async Task RetryHelper_NonTransientException_IsNotRetried()
    {
        var callCount = 0;

        var act = async () => await RetryHelper.ExecuteWithRetryAsync(
            () => { callCount++; return ThrowAndReturn<int>(new InvalidOperationException("Non-transient error")); },
            maxRetries: 3, retryDelayMs: 10, isTransientPredicate: TransientPredicate);

        await act.Should().ThrowAsync<InvalidOperationException>();
        callCount.Should().Be(1);
    }

    [Fact]
    public async Task RetryHelper_MaxRetriesZero_NoRetryAttempted()
    {
        var callCount = 0;

        var act = async () => await RetryHelper.ExecuteWithRetryAsync(
            () => { callCount++; return ThrowAndReturn<int>(new TimeoutException("Simulated timeout")); },
            maxRetries: 0, retryDelayMs: 10, isTransientPredicate: TransientPredicate);

        // With maxRetries=0, first attempt fails as transient but attempt >= maxRetries,
        // so RetryExhaustedException is thrown
        await act.Should().ThrowAsync<RetryExhaustedException>();
        callCount.Should().Be(1);
    }

    [Fact]
    public async Task RetryHelper_NegativeMaxRetries_TreatedAsZero()
    {
        var callCount = 0;

        var act = async () => await RetryHelper.ExecuteWithRetryAsync(
            () => { callCount++; return ThrowAndReturn<int>(new TimeoutException("Simulated timeout")); },
            maxRetries: -1, retryDelayMs: 10, isTransientPredicate: TransientPredicate);

        await act.Should().ThrowAsync<RetryExhaustedException>();
        callCount.Should().Be(1);
    }

    [Fact]
    public async Task RetryHelper_OnRetryCallback_IsInvoked()
    {
        var callCount = 0;
        var retryAttempts = new List<int>();

        await RetryHelper.ExecuteWithRetryAsync(
            () =>
            {
                callCount++;
                if (callCount <= 2)
                    throw new TimeoutException("Simulated timeout");
                return Task.FromResult(42);
            },
            maxRetries: 3, retryDelayMs: 10,
            isTransientPredicate: TransientPredicate,
            onRetry: (attempt, maxAttempts, errorCode, desc, delay) =>
            {
                retryAttempts.Add(attempt);
            });

        retryAttempts.Should().HaveCount(2);
        retryAttempts.Should().ContainInOrder(1, 2);
    }

    /// <summary>
    /// Helper to throw an exception while satisfying the return type requirement for Func&lt;Task&lt;T&gt;&gt;.
    /// Avoids CS0162 unreachable code warning.
    /// </summary>
    private static Task<T> ThrowAndReturn<T>(Exception ex)
    {
        throw ex;
    }
}
