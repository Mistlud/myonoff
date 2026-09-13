using System.Net;
using System.Net.Sockets;

namespace MyOnOff.Protocol;

public static class Ipv4Subnet
{
    public static bool Contains(IPAddress address, string cidr)
    {
        ArgumentNullException.ThrowIfNull(address);
        ArgumentException.ThrowIfNullOrWhiteSpace(cidr);

        var parts = cidr.Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2 ||
            !IPAddress.TryParse(parts[0], out var network) ||
            network.AddressFamily != AddressFamily.InterNetwork ||
            !int.TryParse(parts[1], out var prefixLength) ||
            prefixLength is < 0 or > 32)
        {
            throw new FormatException($"Invalid IPv4 CIDR: {cidr}");
        }

        if (address.AddressFamily != AddressFamily.InterNetwork && !address.IsIPv4MappedToIPv6)
        {
            return false;
        }

        var addressBytes = address.MapToIPv4().GetAddressBytes();
        var networkBytes = network.GetAddressBytes();
        var wholeBytes = prefixLength / 8;
        var remainder = prefixLength % 8;

        for (var i = 0; i < wholeBytes; i++)
        {
            if (addressBytes[i] != networkBytes[i])
            {
                return false;
            }
        }

        if (remainder == 0)
        {
            return true;
        }

        var mask = (byte)(0xFF << (8 - remainder));
        return (addressBytes[wholeBytes] & mask) == (networkBytes[wholeBytes] & mask);
    }

    public static bool IsPrivateLan(IPAddress address)
    {
        if (address.AddressFamily != AddressFamily.InterNetwork && !address.IsIPv4MappedToIPv6)
        {
            return false;
        }

        var bytes = address.MapToIPv4().GetAddressBytes();
        return bytes[0] == 10 ||
               (bytes[0] == 172 && bytes[1] is >= 16 and <= 31) ||
               (bytes[0] == 192 && bytes[1] == 168);
    }
}
