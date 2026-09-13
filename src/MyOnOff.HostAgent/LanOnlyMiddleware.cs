using System.Net;
using Microsoft.Extensions.Options;
using MyOnOff.Protocol;

namespace MyOnOff.HostAgent;

public sealed class LanOnlyMiddleware(
    RequestDelegate next,
    IOptionsMonitor<AgentOptions> options,
    ILogger<LanOnlyMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var remoteAddress = context.Connection.RemoteIpAddress;
        if (remoteAddress is null || !IsAllowed(remoteAddress, options.CurrentValue.AllowedSubnet))
        {
            logger.LogWarning("Rejected request from non-LAN address {RemoteAddress}", remoteAddress);
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { error = "LAN access only." });
            return;
        }

        await next(context);
    }

    private static bool IsAllowed(IPAddress address, string allowedSubnet) =>
        IPAddress.IsLoopback(address) || Ipv4Subnet.Contains(address, allowedSubnet);
}
