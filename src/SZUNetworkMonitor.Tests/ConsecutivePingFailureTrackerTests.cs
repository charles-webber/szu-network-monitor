using SZUNetworkMonitor;
using Xunit;

namespace SZUNetworkMonitor.Tests;

public sealed class ConsecutivePingFailureTrackerTests
{
    [Fact]
    public void FirstFailureWaitsForConfirmationAndSecondFailureTriggersReconnect()
    {
        var tracker = new ConsecutivePingFailureTracker(2);

        Assert.False(tracker.RecordFailure());
        Assert.Equal(1, tracker.ConsecutiveFailures);
        Assert.True(tracker.RecordFailure());
        Assert.Equal(2, tracker.ConsecutiveFailures);
    }

    [Fact]
    public void SuccessfulPingClearsPendingFailureBeforeNextFailure()
    {
        var tracker = new ConsecutivePingFailureTracker(2);

        Assert.False(tracker.RecordFailure());
        tracker.RecordSuccess();

        Assert.Equal(0, tracker.ConsecutiveFailures);
        Assert.False(tracker.RecordFailure());
    }

    [Fact]
    public void ThresholdMustBePositive()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ConsecutivePingFailureTracker(0));
    }
}
