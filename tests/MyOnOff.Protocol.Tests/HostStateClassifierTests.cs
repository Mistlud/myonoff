using MyOnOff.Protocol;

namespace MyOnOff.Protocol.Tests;

public sealed class HostStateClassifierTests
{
    [Fact]
    public void AgentAndSmbReady_IsOnline()
    {
        Assert.Equal(
            HostState.Online,
            HostStateClassifier.Classify(new ProbeSnapshot(true, true, true, true)));
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void AnyPartialReachability_IsBooting(bool ping, bool agent, bool smb)
    {
        Assert.Equal(
            HostState.Booting,
            HostStateClassifier.Classify(new ProbeSnapshot(true, ping, agent, smb)));
    }

    [Fact]
    public void NoReachability_IsOffline()
    {
        Assert.Equal(
            HostState.Offline,
            HostStateClassifier.Classify(new ProbeSnapshot(true, false, false, false)));
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    public void UnreliableEvidence_IsUnknown(bool network, bool malformed, bool unexpected)
    {
        Assert.Equal(
            HostState.Unknown,
            HostStateClassifier.Classify(
                new ProbeSnapshot(network, false, false, false, malformed, unexpected)));
    }

    [Fact]
    public void AccessDenied_IsUnknown()
    {
        Assert.Equal(
            HostState.Unknown,
            HostStateClassifier.Classify(
                new ProbeSnapshot(true, false, false, false, AccessDenied: true)));
    }
}
