using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.Json;
using MyOnOff.Protocol;

namespace MyOnOff.DesktopController;

public sealed record ProbeResult(
    HostState State,
    ProbeSnapshot Snapshot,
    HostStatusResponse? AgentStatus);

public sealed class HostControllerClient : IDisposable
{
    private readonly HttpClient _httpClient = new();

    public async Task<ProbeResult> ProbeAsync(
        ControllerSettings settings,
        CancellationToken cancellationToken)
    {
        if (!IPAddress.TryParse(settings.HostIp, out var hostAddress) || !Ipv4Subnet.IsPrivateLan(hostAddress))
        {
            var unsafeTarget = new ProbeSnapshot(
                true,
                false,
                false,
                false,
                Error: "Host IP must be a private IPv4 LAN address.");
            return new ProbeResult(HostState.Unknown, unsafeTarget, null);
        }

        if (!NetworkInterface.GetIsNetworkAvailable())
        {
            var unavailable = new ProbeSnapshot(false, false, false, false, Error: "Local network is unavailable.");
            return new ProbeResult(HostState.Unknown, unavailable, null);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMilliseconds(settings.RequestTimeoutMilliseconds));

        var pingTask = ProbePingAsync(settings.HostIp, settings.RequestTimeoutMilliseconds);
        var agentTask = ProbeAgentAsync(settings, timeout.Token);
        var smbTask = ProbePortAsync(settings.HostIp, settings.SmbPort, timeout.Token);

        await Task.WhenAll(pingTask, agentTask, smbTask);
        var ping = await pingTask;
        var agent = await agentTask;
        var directSmb = await smbTask;
        var smbReady = directSmb && (agent.Status?.SmbReady ?? true);
        var unexpectedHost = agent.Status is not null &&
                             !string.IsNullOrWhiteSpace(settings.ExpectedHostname) &&
                             !string.Equals(
                                 agent.Status.Hostname,
                                 settings.ExpectedHostname,
                                 StringComparison.OrdinalIgnoreCase);

        var snapshot = new ProbeSnapshot(
            NetworkAvailable: true,
            PingReachable: ping.Reachable,
            AgentReachable: agent.Reachable,
            SmbReachable: smbReady,
            AgentResponseMalformed: agent.Malformed,
            UnexpectedHost: unexpectedHost,
            AccessDenied: agent.AccessDenied,
            LatencyMilliseconds: agent.LatencyMilliseconds ?? ping.LatencyMilliseconds,
            Error: agent.Error);

        return new ProbeResult(HostStateClassifier.Classify(snapshot), snapshot, agent.Status);
    }

    public async Task SendWakeAsync(ControllerSettings settings, CancellationToken cancellationToken)
    {
        if (!IPAddress.TryParse(settings.BroadcastIp, out var broadcastAddress) ||
            !Ipv4Subnet.IsPrivateLan(broadcastAddress))
        {
            throw new InvalidOperationException("Broadcast IP must be a private IPv4 LAN address.");
        }

        var packet = WakeOnLanPacket.Create(settings.HostMac);
        using var udp = new UdpClient { EnableBroadcast = true };
        var endpoint = new IPEndPoint(broadcastAddress, settings.WolPort);

        for (var attempt = 0; attempt < 3; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await udp.SendAsync(packet, endpoint, cancellationToken);
            if (attempt < 2)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(150), cancellationToken);
            }
        }
    }

    public Task SendSleepAsync(ControllerSettings settings, CancellationToken cancellationToken) =>
        SendControlAsync(settings, ApiRoutes.Sleep, cancellationToken);

    public Task SendShutdownAsync(ControllerSettings settings, CancellationToken cancellationToken) =>
        SendControlAsync(settings, ApiRoutes.Shutdown, cancellationToken);

    public void Dispose() => _httpClient.Dispose();

    private async Task SendControlAsync(
        ControllerSettings settings,
        string route,
        CancellationToken cancellationToken)
    {
        if (!IPAddress.TryParse(settings.HostIp, out var hostAddress) || !Ipv4Subnet.IsPrivateLan(hostAddress))
        {
            throw new InvalidOperationException("Host IP must be a private IPv4 LAN address.");
        }

        if (string.IsNullOrWhiteSpace(settings.AuthToken))
        {
            throw new InvalidOperationException("Configure the authentication token in Settings first.");
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMilliseconds(settings.RequestTimeoutMilliseconds));
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(settings.AgentBaseUri, route));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.AuthToken);
        using var response = await _httpClient.SendAsync(request, timeout.Token);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new UnauthorizedAccessException("The Host Agent rejected the authentication token.");
        }

        response.EnsureSuccessStatusCode();
    }

    private async Task<(bool Reachable, long? LatencyMilliseconds)> ProbePingAsync(
        string host,
        int timeoutMilliseconds)
    {
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(host, timeoutMilliseconds);
            return (reply.Status == IPStatus.Success, reply.Status == IPStatus.Success ? reply.RoundtripTime : null);
        }
        catch (Exception exception) when (exception is PingException or InvalidOperationException)
        {
            return (false, null);
        }
    }

    private async Task<(bool Reachable, bool Malformed, bool AccessDenied, HostStatusResponse? Status, long? LatencyMilliseconds, string? Error)>
        ProbeAgentAsync(ControllerSettings settings, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var response = await _httpClient.GetAsync(new Uri(settings.AgentBaseUri, ApiRoutes.Status), cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var denied = response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized;
                return (false, false, denied, null, null, $"Agent returned HTTP {(int)response.StatusCode}.");
            }

            var status = await response.Content.ReadFromJsonAsync<HostStatusResponse>(cancellationToken: cancellationToken);
            if (status is null || string.IsNullOrWhiteSpace(status.Hostname) || status.ApiVersion != "1")
            {
                return (true, true, false, null, stopwatch.ElapsedMilliseconds, "Agent returned malformed or incompatible status data.");
            }

            return (true, false, false, status, stopwatch.ElapsedMilliseconds, null);
        }
        catch (JsonException)
        {
            return (true, true, false, null, stopwatch.ElapsedMilliseconds, "Agent returned malformed JSON.");
        }
        catch (Exception exception) when (
            exception is HttpRequestException or OperationCanceledException)
        {
            return (false, false, false, null, null, exception.Message);
        }
    }

    private static async Task<bool> ProbePortAsync(
        string host,
        int port,
        CancellationToken cancellationToken)
    {
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(host, port, cancellationToken);
            return client.Connected;
        }
        catch (Exception exception) when (
            exception is SocketException or OperationCanceledException)
        {
            return false;
        }
    }
}
