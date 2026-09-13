using System;
using System.Linq;
using System.Net;
using MyOnOff.Protocol;

internal static class ProtocolSelfTest
{
    public static int Main()
    {
        var packet = WakeOnLanPacket.Create("AA:BB:CC:DD:EE:FF");
        Require(packet.Length == 102, "WOL packet length");
        Require(packet.Take(6).All(value => value == 0xFF), "WOL synchronization bytes");
        var expectedMac = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF };
        for (var repetition = 0; repetition < 16; repetition++)
        {
            Require(
                packet.Skip(6 + repetition * 6).Take(6).SequenceEqual(expectedMac),
                $"WOL MAC repetition {repetition + 1}");
        }

        Require(
            HostStateClassifier.Classify(new ProbeSnapshot(true, true, true, true)) == HostState.Online,
            "ONLINE classification");
        Require(
            HostStateClassifier.Classify(new ProbeSnapshot(true, true, false, false)) == HostState.Booting,
            "BOOTING classification");
        Require(
            HostStateClassifier.Classify(new ProbeSnapshot(true, false, false, false)) == HostState.Offline,
            "OFFLINE classification");
        Require(
            HostStateClassifier.Classify(new ProbeSnapshot(false, false, false, false)) == HostState.Unknown,
            "UNKNOWN network classification");
        Require(
            HostStateClassifier.Classify(new ProbeSnapshot(true, false, false, false, AccessDenied: true)) == HostState.Unknown,
            "UNKNOWN access classification");

        Require(Ipv4Subnet.Contains(IPAddress.Parse("192.168.219.104"), "192.168.219.0/24"), "CIDR inclusion");
        Require(!Ipv4Subnet.Contains(IPAddress.Parse("192.168.220.1"), "192.168.219.0/24"), "CIDR exclusion");
        Require(Ipv4Subnet.IsPrivateLan(IPAddress.Parse("10.0.0.1")), "private IPv4 inclusion");
        Require(!Ipv4Subnet.IsPrivateLan(IPAddress.Parse("8.8.8.8")), "public IPv4 exclusion");

        Console.WriteLine("Protocol self-test passed: 26 assertions.");
        return 0;
    }

    private static void Require(bool condition, string description)
    {
        if (!condition)
        {
            throw new InvalidOperationException($"Self-test failed: {description}");
        }
    }
}
