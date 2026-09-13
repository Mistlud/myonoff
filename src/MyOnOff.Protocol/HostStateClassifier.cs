namespace MyOnOff.Protocol;

public static class HostStateClassifier
{
    public static HostState Classify(ProbeSnapshot snapshot)
    {
        if (!snapshot.NetworkAvailable ||
            snapshot.AgentResponseMalformed ||
            snapshot.UnexpectedHost ||
            snapshot.AccessDenied)
        {
            return HostState.Unknown;
        }

        if (snapshot.AgentReachable && snapshot.SmbReachable)
        {
            return HostState.Online;
        }

        if (snapshot.PingReachable || snapshot.AgentReachable || snapshot.SmbReachable)
        {
            return HostState.Booting;
        }

        return HostState.Offline;
    }
}
