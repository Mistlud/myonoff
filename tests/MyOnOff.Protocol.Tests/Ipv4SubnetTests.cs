using System.Net;
using MyOnOff.Protocol;

namespace MyOnOff.Protocol.Tests;

public sealed class Ipv4SubnetTests
{
    [Theory]
    [InlineData("192.168.219.1", true)]
    [InlineData("192.168.219.104", true)]
    [InlineData("192.168.220.1", false)]
    [InlineData("10.0.0.1", false)]
    public void Contains_UsesConfiguredPrefix(string address, bool expected)
    {
        Assert.Equal(
            expected,
            Ipv4Subnet.Contains(IPAddress.Parse(address), "192.168.219.0/24"));
    }

    [Fact]
    public void Contains_RejectsInvalidCidr()
    {
        Assert.Throws<FormatException>(() =>
            Ipv4Subnet.Contains(IPAddress.Loopback, "192.168.219.0/99"));
    }

    [Theory]
    [InlineData("10.1.2.3", true)]
    [InlineData("172.31.255.1", true)]
    [InlineData("192.168.1.1", true)]
    [InlineData("172.32.0.1", false)]
    [InlineData("8.8.8.8", false)]
    public void IsPrivateLan_RejectsPublicAddresses(string address, bool expected)
    {
        Assert.Equal(expected, Ipv4Subnet.IsPrivateLan(IPAddress.Parse(address)));
    }
}
