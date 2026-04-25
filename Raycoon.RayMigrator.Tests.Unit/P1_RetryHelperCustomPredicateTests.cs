using FluentAssertions;
using Raycoon.RayMigrator.Database.Common;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1: Tests for RetryHelper custom predicate overloads (G4).
/// Validates the Func&lt;Exception, (bool isTransient, string? errorCode)&gt; predicate path.
/// </summary>
public class RetryHelperCustomPredicateTests
{
    private static readonly Func<Exception, (bool isTransient, string? errorCode)> TransientPredicate =
        ex => ex is TimeoutException ? (true, "42") : (false, null);

    #region Succeeds on first attempt

    [Fact]
    public async Task CustomPredicate_AsyncGeneric_SucceedsOnFirstAttempt()
    {
        var callCount = 0;

        var result = await RetryHelper.ExecuteWithRetryAsync(
            () => { callCount++; return Task.FromResult(99); },
            maxRetries: 3, retryDelayMs: 1, isTransientPredicate: TransientPredicate);

        result.Should().Be(99);
        callCount.Should().Be(1);
    }

    [Fact]
    public async Task CustomPredicate_AsyncVoid_SucceedsOnFirstAttempt()
    {
        var callCount = 0;

        await RetryHelper.ExecuteWithRetryAsync(
            () => { callCount++; return Task.CompletedTask; },
            maxRetries: 3, retryDelayMs: 1, isTransientPredicate: TransientPredicate);

        callCount.Should().Be(1);
    }

    [Fact]
    public void CustomPredicate_Sync_SucceedsOnFirstAttempt()
    {
        var callCount = 0;

        var result = RetryHelper.ExecuteWithRetry(
            () => { callCount++; return 99; },
            maxRetries: 3, retryDelayMs: 1, isTransientPredicate: TransientPredicate);

        result.Should().Be(99);
        callCount.Should().Be(1);
    }

    #endregion

    #region Retries on transient then succeeds

    [Fact]
    public async Task CustomPredicate_AsyncGeneric_RetriesOnTransient_ThenSucceeds()
    {
        var callCount = 0;

        var result = await RetryHelper.ExecuteWithRetryAsync(
            () =>
            {
                callCount++;
                if (callCount == 1)
                    throw new TimeoutException("Simulated");
                return Task.FromResult(99);
            },
            maxRetries: 3, retryDelayMs: 1, isTransientPredicate: TransientPredicate);

        result.Should().Be(99);
        callCount.Should().Be(2);
    }

    [Fact]
    public async Task CustomPredicate_AsyncVoid_RetriesOnTransient_ThenSucceeds()
    {
        var callCount = 0;

        await RetryHelper.ExecuteWithRetryAsync(
            () =>
            {
                callCount++;
                if (callCount == 1)
                    throw new TimeoutException("Simulated");
                return Task.CompletedTask;
            },
            maxRetries: 3, retryDelayMs: 1, isTransientPredicate: TransientPredicate);

        callCount.Should().Be(2);
    }

    [Fact]
    public void CustomPredicate_Sync_RetriesOnTransient_ThenSucceeds()
    {
        var callCount = 0;

        var result = RetryHelper.ExecuteWithRetry(
            () =>
            {
                callCount++;
                if (callCount == 1)
                    throw new TimeoutException("Simulated");
                return 99;
            },
            maxRetries: 3, retryDelayMs: 1, isTransientPredicate: TransientPredicate);

        result.Should().Be(99);
        callCount.Should().Be(2);
    }

    #endregion

    #region Error handling

    [Fact]
    public async Task CustomPredicate_ExhaustsRetries_ThrowsRetryExhaustedException()
    {
        var callCount = 0;

        var act = async () => await RetryHelper.ExecuteWithRetryAsync(
            () => { callCount++; return ThrowAndReturn<int>(new TimeoutException("Simulated")); },
            maxRetries: 2, retryDelayMs: 1, isTransientPredicate: TransientPredicate);

        var ex = await act.Should().ThrowAsync<RetryExhaustedException>();
        ex.Which.AttemptsMade.Should().Be(3); // 1 initial + 2 retries
        callCount.Should().Be(3);
    }

    [Fact]
    public async Task CustomPredicate_NonTransient_NotRetried_RethrowsOriginal()
    {
        var callCount = 0;

        var act = async () => await RetryHelper.ExecuteWithRetryAsync(
            () => { callCount++; return ThrowAndReturn<int>(new InvalidOperationException("Non-transient")); },
            maxRetries: 3, retryDelayMs: 1, isTransientPredicate: TransientPredicate);

        await act.Should().ThrowAsync<InvalidOperationException>();
        callCount.Should().Be(1);
    }

    [Fact]
    public async Task CustomPredicate_MaxRetriesZero_NoRetry()
    {
        var callCount = 0;

        var act = async () => await RetryHelper.ExecuteWithRetryAsync(
            () => { callCount++; return ThrowAndReturn<int>(new TimeoutException("Simulated")); },
            maxRetries: 0, retryDelayMs: 1, isTransientPredicate: TransientPredicate);

        await act.Should().ThrowAsync<RetryExhaustedException>();
        callCount.Should().Be(1);
    }

    [Fact]
    public void CustomPredicate_NegativeMaxRetries_TreatedAsZero()
    {
        var callCount = 0;

        var act = () => RetryHelper.ExecuteWithRetry(
            () => { callCount++; return ThrowAndReturn_Sync<int>(new TimeoutException("Simulated")); },
            maxRetries: -1, retryDelayMs: 1, isTransientPredicate: TransientPredicate);

        act.Should().Throw<RetryExhaustedException>();
        callCount.Should().Be(1);
    }

    #endregion

    #region Callback and error code

    [Fact]
    public async Task CustomPredicate_OnRetryCallback_IsInvoked()
    {
        var callCount = 0;
        var retryAttempts = new List<int>();

        await RetryHelper.ExecuteWithRetryAsync(
            () =>
            {
                callCount++;
                if (callCount <= 2)
                    throw new TimeoutException("Simulated");
                return Task.FromResult(42);
            },
            maxRetries: 3, retryDelayMs: 1, isTransientPredicate: TransientPredicate,
            onRetry: (attempt, maxAttempts, errorCode, desc, delay) =>
            {
                retryAttempts.Add(attempt);
            });

        retryAttempts.Should().HaveCount(2);
        retryAttempts.Should().ContainInOrder(1, 2);
    }

    [Fact]
    public async Task CustomPredicate_ErrorCode_PassedToRetryExhaustedException()
    {
        var act = async () => await RetryHelper.ExecuteWithRetryAsync(
            () => ThrowAndReturn<int>(new TimeoutException("Simulated")),
            maxRetries: 1, retryDelayMs: 1,
            isTransientPredicate: ex => ex is TimeoutException ? (true, "9999") : (false, null));

        var ex = await act.Should().ThrowAsync<RetryExhaustedException>();
        ex.Which.LastErrorCode.Should().Be("9999");
    }

    [Fact]
    public async Task CustomPredicate_LinearBackoff_CallbackReceivesIncreasingDelay()
    {
        var callCount = 0;
        var delays = new List<int>();

        var act = async () => await RetryHelper.ExecuteWithRetryAsync(
            () => { callCount++; return ThrowAndReturn<int>(new TimeoutException("Simulated")); },
            maxRetries: 3, retryDelayMs: 1, isTransientPredicate: TransientPredicate,
            onRetry: (attempt, maxAttempts, errorCode, desc, delay) =>
            {
                delays.Add(delay);
            });

        await act.Should().ThrowAsync<RetryExhaustedException>();
        // Linear backoff: delay = retryDelayMs * attempt → 1, 2, 3
        delays.Should().ContainInOrder(1, 2, 3);
    }

    [Fact]
    public async Task CustomPredicate_PredicateReceivesCorrectException()
    {
        Exception? capturedEx = null;

        var act = async () => await RetryHelper.ExecuteWithRetryAsync(
            () => ThrowAndReturn<int>(new TimeoutException("specific message")),
            maxRetries: 0, retryDelayMs: 1,
            isTransientPredicate: ex =>
            {
                capturedEx = ex;
                return (true, "42");
            });

        await act.Should().ThrowAsync<RetryExhaustedException>();
        capturedEx.Should().NotBeNull();
        capturedEx.Should().BeOfType<TimeoutException>();
        capturedEx!.Message.Should().Be("specific message");
    }

    #endregion

    /// <summary>
    /// Helper to throw an exception while satisfying the return type requirement for Func&lt;Task&lt;T&gt;&gt;.
    /// </summary>
    private static Task<T> ThrowAndReturn<T>(Exception ex) => throw ex;

    /// <summary>
    /// Helper for synchronous throw with return type.
    /// </summary>
    private static T ThrowAndReturn_Sync<T>(Exception ex) => throw ex;
}
