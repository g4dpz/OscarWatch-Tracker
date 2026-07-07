using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using OscarWatch.Core.Models;

namespace OscarWatch.Core.SatelliteLink;

/// <summary>Builds <see cref="HttpListener"/> prefixes without Windows admin URL ACL for <c>http://+:</c>.</summary>
public static class SatelliteLinkListenPrefixBuilder
{
    public static IReadOnlyList<string> Build(SatelliteLinkSettings settings)
    {
        var port = SatelliteLinkSettings.NormalizePort(settings.Port);
        var prefixes = new List<string> { FormatHttpPrefix(IPAddress.Loopback, port) };

        if (settings.AllowLanClients)
        {
            foreach (var address in GetLocalLanIPv4Addresses())
            {
                var prefix = FormatHttpPrefix(address, port);
                if (!prefixes.Contains(prefix, StringComparer.OrdinalIgnoreCase))
                    prefixes.Add(prefix);
            }
        }

        return prefixes;
    }

    public static IReadOnlyList<string> BuildWebSocketEndpoints(SatelliteLinkSettings settings)
    {
        var port = SatelliteLinkSettings.NormalizePort(settings.Port);
        var endpoints = new List<string> { $"ws://127.0.0.1:{port}/" };

        if (!settings.AllowLanClients)
            return endpoints;

        foreach (var address in GetLocalLanIPv4Addresses())
        {
            var endpoint = $"ws://{address}:{port}/";
            if (!endpoints.Contains(endpoint, StringComparer.OrdinalIgnoreCase))
                endpoints.Add(endpoint);
        }

        return endpoints;
    }

    public static string FormatEndpointPreview(SatelliteLinkSettings settings) =>
        string.Join(Environment.NewLine, BuildWebSocketEndpoints(settings));

    public static string DescribeBindFailure(Exception ex)
    {
        var message = ex.Message;
        if (message.Contains("Access is denied", StringComparison.OrdinalIgnoreCase)
            || message.Contains("conflicts with an existing registration", StringComparison.OrdinalIgnoreCase))
        {
            return "Windows denied binding on the network interface. Ensure the port is free, or run Settings → Test port with Satellite link disabled in the main window first.";
        }

        if (message.Contains("address already in use", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Only one usage of each socket address", StringComparison.OrdinalIgnoreCase))
        {
            return "The port is already in use. Disable Satellite link in the main window (or choose another port) before testing.";
        }

        return message;
    }

    private static string FormatHttpPrefix(IPAddress address, int port) =>
        $"http://{address}:{port}/";

    private static IEnumerable<IPAddress> GetLocalLanIPv4Addresses()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up)
                continue;

            if (nic.NetworkInterfaceType is NetworkInterfaceType.Loopback
                or NetworkInterfaceType.Tunnel)
                continue;

            foreach (var unicast in nic.GetIPProperties().UnicastAddresses)
            {
                if (unicast.Address.AddressFamily != AddressFamily.InterNetwork)
                    continue;

                if (IPAddress.IsLoopback(unicast.Address))
                    continue;

                // Skip APIPA / link-local
                var bytes = unicast.Address.GetAddressBytes();
                if (bytes.Length == 4 && bytes[0] == 169 && bytes[1] == 254)
                    continue;

                if (seen.Add(unicast.Address.ToString()))
                    yield return unicast.Address;
            }
        }
    }
}
