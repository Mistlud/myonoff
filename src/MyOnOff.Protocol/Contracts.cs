using System.Text.Json.Serialization;

namespace MyOnOff.Protocol;

[JsonConverter(typeof(JsonStringEnumConverter<HostState>))]
public enum HostState
{
    Offline,
    Booting,
    Online,
    Unknown,
    GoingToSleep,
    ShuttingDown
}

public sealed record HostStatusResponse(
    string Status,
    string Hostname,
    string Ip,
    long UptimeSeconds,
    bool SmbReady,
    string ApiVersion = "1");

public sealed record ControlAcceptedResponse(
    string Action,
    bool Accepted,
    string Message);

public sealed record ProbeSnapshot(
    bool NetworkAvailable,
    bool PingReachable,
    bool AgentReachable,
    bool SmbReachable,
    bool AgentResponseMalformed = false,
    bool UnexpectedHost = false,
    bool AccessDenied = false,
    long? LatencyMilliseconds = null,
    string? Error = null);
