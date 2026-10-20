using AwesomeAssertions;
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
            maxRetries: 3, retryDelayMs: 10, isTransientPredicate: TransientPredicate, cancellationToken: TestContext.Current.CancellationToken);

        result.Should().Be(42);
        callCount.Should().Be(1);
    }

    [Fact]
    public async Task RetryHelper_AsyncVoid_SucceedsOnFirstAttempt()
    {
        var callCount = 0;

        await RetryHelper.ExecuteWithRetryAsync(
            () => { callCount++; return Task.CompletedTask; },
            maxRetries: 3, retryDelayMs: 10, isTransientPredicate: TransientPredicate, cancellationToken: TestContext.Current.CancellationToken);

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
            maxRetries: 3, retryDelayMs: 10, isTransientPredicate: TransientPredicate, cancellationToken: TestContext.Current.CancellationToken);

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
            maxRetries: 3, retryDelayMs: 10, isTransientPredicate: TransientPredicate, cancellationToken: TestContext.Current.CancellationToken);

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
            }, cancellationToken: TestContext.Current.CancellationToken);

        retryAttempts.Should().HaveCount(2);
        retryAttempts.Should().ContainInOrder(1, 2);
    }

    [Fact(Timeout = 30_000)]
    public async Task RetryHelper_CancelledWhileWaitingToRetry_StopsWaitingAndDoesNotRetry()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var callCount = 0;

        // Act
        var act = () => RetryHelper.ExecuteWithRetryAsync(
            () => { callCount++; return ThrowAndReturn<int>(new TimeoutException("Simulated timeout")); },
            maxRetries: 3, retryDelayMs: 60_000, isTransientPredicate: TransientPredicate,
            onRetry: (_, _, _, _, _) => cts.Cancel(),
            cancellationToken: cts.Token);

        // Assert
        var thrown = await act.Should().ThrowAsync<OperationCanceledException>(
            because: "the cancellation ends the minute-long wait before the second attempt");
        thrown.Which.CancellationToken.Should().Be(cts.Token);
        callCount.Should().Be(1, because: "no attempt starts after the cancellation");
    }

    [Fact(Timeout = 30_000)]
    public async Task RetryHelper_AsyncVoid_CancelledWhileWaitingToRetry_StopsWaitingAndDoesNotRetry()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var callCount = 0;

        // Act
        var act = () => RetryHelper.ExecuteWithRetryAsync(
            () => { callCount++; return Task.FromException(new TimeoutException("Simulated timeout")); },
            maxRetries: 3, retryDelayMs: 60_000, isTransientPredicate: TransientPredicate,
            onRetry: (_, _, _, _, _) => cts.Cancel(),
            cancellationToken: cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>(because: "the void overload passes the token on");
        callCount.Should().Be(1);
    }

    [Fact]
    public async Task RetryHelper_TransientFailureAfterCancellation_IsNotClassifiedOrRetried()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var callCount = 0;
        var classified = 0;

        // Act
        var act = () => RetryHelper.ExecuteWithRetryAsync(
            () =>
            {
                callCount++;
                cts.Cancel();
                return ThrowAndReturn<int>(new TimeoutException("Simulated timeout"));
            },
            maxRetries: 3, retryDelayMs: 1,
            isTransientPredicate: ex => { classified++; return TransientPredicate(ex); },
            cancellationToken: cts.Token);

        // Assert
        await act.Should().ThrowExactlyAsync<TimeoutException>(
            because: "the helper passes the failure on as it is; the DAL makes it a cancellation");
        callCount.Should().Be(1, because: "a failure after the cancellation is not retried");
        classified.Should().Be(0, because: "a failure after the cancellation is not classified as transient at all");
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
