namespace MyOnOff.HostAgent;

public sealed class AgentOptions
{
    public const string SectionName = "Agent";

    public string ListenUrl { get; set; } = "http://0.0.0.0:5055";
    public string HostIp { get; set; } = "192.168.219.104";
    public string AllowedSubnet { get; set; } = "192.168.219.0/24";
    public int SmbPort { get; set; } = 445;
    public int SmbProbeTimeoutMilliseconds { get; set; } = 1000;
    public string AuthToken { get; set; } = string.Empty;
    public string LogPath { get; set; } = string.Empty;
}
