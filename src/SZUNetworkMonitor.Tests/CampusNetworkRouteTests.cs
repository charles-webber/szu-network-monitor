using System.Net;
using SZUNetworkMonitor;
using Xunit;

namespace SZUNetworkMonitor.Tests;

public sealed class CampusNetworkRouteTests
{
    [Theory]
    [InlineData("172.28.192.101", true)]
    [InlineData("10.0.0.1", true)]
    [InlineData("198.18.6.157", false)]
    [InlineData("198.19.1.1", false)]
    [InlineData("169.254.1.1", false)]
    [InlineData("127.0.0.1", false)]
    [InlineData("0.0.0.0", false)]
    public void UsableIPv4RejectsFakeAndNonRoutableAddresses(string value, bool expected)
    {
        Assert.Equal(expected, CampusNetworkRoute.IsUsableIPv4(IPAddress.Parse(value)));
    }
}
