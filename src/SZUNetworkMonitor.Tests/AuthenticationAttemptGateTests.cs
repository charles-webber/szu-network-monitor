using SZUNetworkMonitor;
using Xunit;

namespace SZUNetworkMonitor.Tests;

public sealed class AuthenticationAttemptGateTests
{
    [Fact]
    public void FailedLoginBlocksNewAttemptsForSixtySeconds()
    {
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 9, 16, 12, 0, 0, TimeSpan.Zero));
        var gate = new AuthenticationAttemptGate(clock, TimeSpan.FromSeconds(60));

        Assert.Equal(AuthenticationAttemptState.Started, gate.TryBegin(out _));
        gate.Complete(succeeded: false);

        Assert.Equal(AuthenticationAttemptState.CoolingDown, gate.TryBegin(out var remaining));
        Assert.Equal(TimeSpan.FromSeconds(60), remaining);

        clock.Advance(TimeSpan.FromSeconds(59));
        Assert.Equal(AuthenticationAttemptState.CoolingDown, gate.TryBegin(out remaining));
        Assert.Equal(TimeSpan.FromSeconds(1), remaining);

        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(AuthenticationAttemptState.Started, gate.TryBegin(out _));
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
