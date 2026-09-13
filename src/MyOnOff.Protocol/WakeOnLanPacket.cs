using System.Globalization;

namespace MyOnOff.Protocol;

public static class WakeOnLanPacket
{
    public const int PacketLength = 102;

    public static byte[] Create(string macAddress)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(macAddress);

        var normalized = macAddress
            .Replace(":", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(".", string.Empty, StringComparison.Ordinal);

        if (normalized.Length != 12)
        {
            throw new FormatException("MAC address must contain exactly 6 bytes.");
        }

        var mac = new byte[6];
        for (var i = 0; i < mac.Length; i++)
        {
            if (!byte.TryParse(
                    normalized.AsSpan(i * 2, 2),
                    NumberStyles.AllowHexSpecifier,
                    CultureInfo.InvariantCulture,
                    out mac[i]))
            {
                throw new FormatException("MAC address contains non-hexadecimal characters.");
            }
        }

        var packet = new byte[PacketLength];
        Array.Fill(packet, (byte)0xFF, 0, 6);
        for (var repetition = 0; repetition < 16; repetition++)
        {
            Buffer.BlockCopy(mac, 0, packet, 6 + (repetition * mac.Length), mac.Length);
        }

        return packet;
    }
}
