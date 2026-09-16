using SZUNetworkMonitor;
using Xunit;

namespace SZUNetworkMonitor.Tests;

public sealed class SingleInstanceLeaseTests
{
    [Fact]
    public void SecondLeaseForTheSameNamedUserScopeIsRejected()
    {
        var mutexName = $"Local\\SZUNetworkMonitor-Test-{Guid.NewGuid():N}";
        using var firstLease = SingleInstanceLease.TryAcquire(mutexName) ?? throw new InvalidOperationException("The first lease was not acquired.");

        using var secondLease = SingleInstanceLease.TryAcquire(mutexName);

        Assert.Null(secondLease);
    }
}
