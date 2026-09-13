using System.Net;
using MyOnOff.Protocol;

namespace MyOnOff.DesktopController;

public sealed record ControllerSettings
{
    public string HostIp { get; init; } = "192.168.219.104";
    public string HostMac { get; init; } = string.Empty;
    public string BroadcastIp { get; init; } = "192.168.219.255";
    public int WolPort { get; init; } = 9;
    public int AgentPort { get; init; } = 5055;
    public int SmbPort { get; init; } = 445;
    public string SmbShare { get; init; } = "domination";
    public string AuthToken { get; init; } = string.Empty;
    public string ExpectedHostname { get; init; } = string.Empty;
    public int PollIntervalSeconds { get; init; } = 3;
    public int RequestTimeoutMilliseconds { get; init; } = 2500;

    public Uri AgentBaseUri => new($"http://{HostIp}:{AgentPort}", UriKind.Absolute);

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (!IPAddress.TryParse(HostIp, out var hostAddress) || !Ipv4Subnet.IsPrivateLan(hostAddress))
        {
            errors.Add("Host IP must be a private IPv4 LAN address.");
        }

        if (!IPAddress.TryParse(BroadcastIp, out var broadcastAddress) || !Ipv4Subnet.IsPrivateLan(broadcastAddress))
        {
            errors.Add("Broadcast IP must be a private IPv4 LAN address.");
        }

        try
        {
            _ = WakeOnLanPacket.Create(HostMac);
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException)
        {
            errors.Add("Host MAC must contain exactly 6 hexadecimal bytes.");
        }

        if (!IsPort(WolPort) || !IsPort(AgentPort) || !IsPort(SmbPort))
        {
            errors.Add("All ports must be between 1 and 65535.");
        }

        if (PollIntervalSeconds is < 1 or > 60)
        {
            errors.Add("Poll interval must be between 1 and 60 seconds.");
        }

        if (RequestTimeoutMilliseconds is < 250 or > 30000)
        {
            errors.Add("Request timeout must be between 250 and 30000 milliseconds.");
        }

        return errors;
    }

    private static bool IsPort(int value) => value is > 0 and <= 65535;
}
