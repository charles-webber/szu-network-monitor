using SZUNetworkMonitor;
using Xunit;

namespace SZUNetworkMonitor.Tests;

public sealed class ConnectivityCheckGateTests
{
    [Fact]
    public void CompletedCheckBlocksRepeatedChecksForSixtySeconds()
    {
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 9, 17, 1, 0, 0, TimeSpan.Zero));
        var gate = new ConnectivityCheckGate(clock, TimeSpan.FromSeconds(60));

        Assert.Equal(ConnectivityCheckState.Started, gate.TryBegin(out _));
        Assert.Equal(ConnectivityCheckState.AlreadyRunning, gate.TryBegin(out _));

        gate.Complete();

        Assert.Equal(ConnectivityCheckState.WaitingForNextInterval, gate.TryBegin(out var remaining));
        Assert.Equal(TimeSpan.FromSeconds(60), remaining);

        clock.Advance(TimeSpan.FromSeconds(60));
        Assert.Equal(ConnectivityCheckState.Started, gate.TryBegin(out _));
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow;

        public ManualTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan amount)
        {
            _utcNow = _utcNow.Add(amount);
        }
    }
}
