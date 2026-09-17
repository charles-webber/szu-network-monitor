using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace SZUNetworkMonitor;

// Identifies the physical campus adapter and supplies only its source address
// and DNS servers to the helper through its private process environment. This
// avoids proxy/TUN DNS answers such as the 198.18.0.0/15 Fake-IP range.
internal sealed record CampusNetworkRoute(string SourceAddress, IReadOnlyList<string> DnsServers)
{
    public static CampusNetworkRoute? TryDiscover()
    {
        var candidates = NetworkInterface.GetAllNetworkInterfaces()
            .Where(networkInterface =>
                networkInterface.OperationalStatus == OperationalStatus.Up &&
                networkInterface.NetworkInterfaceType is NetworkInterfaceType.Wireless80211 or NetworkInterfaceType.Ethernet)
            .OrderBy(networkInterface => networkInterface.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ? 0 : 1);

        foreach (var networkInterface in candidates)
        {
            var properties = networkInterface.GetIPProperties();
            var sourceAddress = properties.UnicastAddresses
                .Select(address => address.Address)
                .FirstOrDefault(IsUsableIPv4);
            if (sourceAddress is null)
            {
                continue;
            }

            var dnsServers = properties.DnsAddresses
                .Where(IsUsableIPv4)
                .Select(address => address.ToString())
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (dnsServers.Length == 0)
            {
                continue;
            }

            return new CampusNetworkRoute(sourceAddress.ToString(), dnsServers);
        }

        return null;
    }

    internal static bool IsUsableIPv4(IPAddress address)
    {
        if (address.AddressFamily != AddressFamily.InterNetwork ||
            IPAddress.IsLoopback(address) ||
            address.Equals(IPAddress.Any))
        {
            return false;
        }

        var bytes = address.GetAddressBytes();
        if (bytes[0] == 169 && bytes[1] == 254)
        {
            return false;
        }

        // RFC 2544 benchmarking addresses are commonly used as TUN/Fake-IP
        // destinations and are never valid physical-adapter or DNS addresses.
        return bytes[0] != 198 || (bytes[1] != 18 && bytes[1] != 19);
    }
}
