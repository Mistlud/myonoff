using System.Net.Sockets;
using Microsoft.Extensions.Options;

namespace MyOnOff.HostAgent;

public interface ISmbReadinessProbe
{
    Task<bool> IsReadyAsync(CancellationToken cancellationToken);
}

public sealed class SmbReadinessProbe(IOptionsMonitor<AgentOptions> options) : ISmbReadinessProbe
{
    public async Task<bool> IsReadyAsync(CancellationToken cancellationToken)
    {
        var settings = options.CurrentValue;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMilliseconds(settings.SmbProbeTimeoutMilliseconds));

        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(settings.HostIp, settings.SmbPort, timeout.Token);
            return client.Connected;
        }
        catch (Exception exception) when (
            exception is SocketException or OperationCanceledException)
        {
            return false;
        }
    }
}
