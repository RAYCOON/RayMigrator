using AwesomeAssertions;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Services.Abstractions;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1: <see cref="RunCancellation"/>, the handle a host cancels one run with: the first <see cref="RunCancellation.Cancel"/>
/// decides the action and cancels the token, later calls change nothing, an invalid action is refused, and a disposed
/// handle neither throws nor cancels. <see cref="MigrationCancelledAction"/> mirrors <see cref="MigrationErrorAction"/>.
/// </summary>
public class RunCancellationTests
{
    [Fact]
    public void NewHandle_NotCancelled_HasNoRequest()
    {
        // Arrange
        using var cancellation = new RunCancellation();

        // Act
        bool requested = cancellation.IsCancellationRequested;

        // Assert
        requested.Should().BeFalse();
        cancellation.Token.IsCancellationRequested.Should().BeFalse();
        cancellation.Token.CanBeCanceled.Should().BeTrue(because: "the engine links the token with the token of the call");
        cancellation.Action.Should().BeNull();
        cancellation.Reason.Should().BeNull();
        cancellation.RequestedAtUtc.Should().BeNull();
    }

    [Fact]
    public void Cancel_FirstCall_CancelsTheTokenAndRecordsTheRequest()
    {
        // Arrange
        using var cancellation = new RunCancellation();
        var before = DateTime.UtcNow;

        // Act
        bool cancelled = cancellation.Cancel(MigrationCancelledAction.RollbackFile, "user request");

        // Assert
        cancelled.Should().BeTrue(because: "the first call cancels the run");
        cancellation.Token.IsCancellationRequested.Should().BeTrue();
        cancellation.IsCancellationRequested.Should().BeTrue();
        cancellation.Action.Should().Be(MigrationCancelledAction.RollbackFile);
        cancellation.Reason.Should().Be("user request");
        cancellation.RequestedAtUtc.Should().NotBeNull();
        cancellation.RequestedAtUtc!.Value.Should().BeOnOrAfter(before).And.BeOnOrBefore(DateTime.UtcNow);
        cancellation.RequestedAtUtc.Value.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void Cancel_CallbackThrows_ReturnsTrueAndTheRunStillStops()
    {
        // Arrange: a provider callback that fails, as MySqlConnector's when its connection for KILL QUERY fails
        using var cancellation = new RunCancellation();
        bool otherCallbackRan = false;
        using var failing = cancellation.Token.Register(() => throw new InvalidOperationException("KILL QUERY failed"));
        using var other = cancellation.Token.Register(() => otherCallbackRan = true);

        // Act
        bool cancelled = cancellation.Cancel(MigrationCancelledAction.RollbackRun, "user request");

        // Assert
        cancelled.Should().BeTrue(because: "the action and the token are set, so the run stops at its next checkpoint");
        cancellation.Token.IsCancellationRequested.Should().BeTrue();
        cancellation.Action.Should().Be(MigrationCancelledAction.RollbackRun);
        otherCallbackRan.Should().BeTrue(because: "every callback runs although one of them failed");
        cancellation.Cancel(MigrationCancelledAction.Terminate).Should().BeFalse(because: "the run is cancelled once");
    }

    [Fact]
    public void Cancel_SecondCall_KeepsTheFirstAction()
    {
        // Arrange
        using var cancellation = new RunCancellation();
        cancellation.Cancel(MigrationCancelledAction.Terminate, "first");

        // Act
        bool second = cancellation.Cancel(MigrationCancelledAction.RollbackRun, "second");

        // Assert
        second.Should().BeFalse(because: "a run is cancelled once");
        cancellation.Action.Should().Be(MigrationCancelledAction.Terminate, because: "the first action wins");
        cancellation.Reason.Should().Be("first");
    }

    [Fact]
    public async Task Cancel_FromManyThreadsAtOnce_ExactlyOneCallWins()
    {
        // Arrange
        using var cancellation = new RunCancellation();
        MigrationCancelledAction[] actions =
        [
            MigrationCancelledAction.Terminate, MigrationCancelledAction.RollbackRun,
            MigrationCancelledAction.RollbackFile, MigrationCancelledAction.RollbackRelease
        ];

        // Act
        bool[] results = await Task.WhenAll(Enumerable.Range(0, 40)
            .Select(i => Task.Run(() => cancellation.Cancel(actions[i % actions.Length]), TestContext.Current.CancellationToken)));

        // Assert
        results.Count(r => r).Should().Be(1, because: "the first action is recorded once, whatever thread calls first");
        cancellation.Action.Should().NotBeNull();
        cancellation.Token.IsCancellationRequested.Should().BeTrue();
    }

    [Theory]
    [InlineData(MigrationCancelledAction.Undefined)]
    [InlineData((MigrationCancelledAction)99)]
    public void Cancel_InvalidAction_ThrowsAndDoesNotCancel(MigrationCancelledAction action)
    {
        // Arrange
        using var cancellation = new RunCancellation();

        // Act
        var act = () => cancellation.Cancel(action);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("action");
        cancellation.Token.IsCancellationRequested.Should().BeFalse(because: "a refused call leaves the run alone");
        cancellation.Action.Should().BeNull();
    }

    [Fact]
    public void Cancel_AfterDispose_ReturnsFalseAndKeepsTheTokenReadable()
    {
        // Arrange
        var cancellation = new RunCancellation();
        var token = cancellation.Token;
        cancellation.Dispose();

        // Act
        bool cancelled = cancellation.Cancel(MigrationCancelledAction.Terminate);

        // Assert
        cancelled.Should().BeFalse(because: "the run of a disposed handle has returned");
        cancellation.Token.Should().Be(token);
        cancellation.Token.IsCancellationRequested.Should().BeFalse();
        cancellation.Action.Should().BeNull();
    }

    [Fact]
    public void Dispose_Twice_DoesNotThrow()
    {
        // Arrange
        var cancellation = new RunCancellation();
        cancellation.Dispose();

        // Act
        var act = () => cancellation.Dispose();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void MigrationCancelledAction_Values_MirrorTheErrorActionsWithTheirNumbers()
    {
        // Act
        var values = Enum.GetValues<MigrationCancelledAction>();

        // Assert
        values.Should().Equal(
            [MigrationCancelledAction.Undefined, MigrationCancelledAction.Terminate, MigrationCancelledAction.RollbackRun,
             MigrationCancelledAction.RollbackFile, MigrationCancelledAction.RollbackRelease],
            because: "the cancel actions are the error actions without Ignore");
        ((byte)MigrationCancelledAction.Terminate).Should().Be((byte)MigrationErrorAction.Terminate);
        ((byte)MigrationCancelledAction.RollbackRun).Should().Be((byte)MigrationErrorAction.Rollback);
        ((byte)MigrationCancelledAction.RollbackFile).Should().Be((byte)MigrationErrorAction.RollbackErrorOnly);
        ((byte)MigrationCancelledAction.RollbackRelease).Should().Be((byte)MigrationErrorAction.RollbackRelease);
    }
}
