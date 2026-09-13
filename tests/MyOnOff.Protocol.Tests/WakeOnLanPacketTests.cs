using MyOnOff.Protocol;

namespace MyOnOff.Protocol.Tests;

public sealed class WakeOnLanPacketTests
{
    [Theory]
    [InlineData("AA:BB:CC:DD:EE:FF")]
    [InlineData("aa-bb-cc-dd-ee-ff")]
    [InlineData("aabb.ccdd.eeff")]
    public void Create_ProducesStandardMagicPacket(string macAddress)
    {
        var packet = WakeOnLanPacket.Create(macAddress);

        Assert.Equal(WakeOnLanPacket.PacketLength, packet.Length);
        Assert.All(packet.Take(6), value => Assert.Equal((byte)0xFF, value));

        var expectedMac = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF };
        for (var repetition = 0; repetition < 16; repetition++)
        {
            Assert.Equal(expectedMac, packet.Skip(6 + (repetition * 6)).Take(6).ToArray());
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("AA:BB:CC")]
    [InlineData("GG:BB:CC:DD:EE:FF")]
    public void Create_RejectsInvalidMac(string macAddress)
    {
        var exception = Record.Exception(() => WakeOnLanPacket.Create(macAddress));
        Assert.True(exception is ArgumentException or FormatException);
    }
}
