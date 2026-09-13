using Microsoft.Extensions.Options;
using MyOnOff.HostAgent;
using MyOnOff.Protocol;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

var environmentToken = Environment.GetEnvironmentVariable("MYONOFF_AGENT_TOKEN");
if (!string.IsNullOrWhiteSpace(environmentToken))
{
    builder.Configuration[$"{AgentOptions.SectionName}:AuthToken"] = environmentToken;
}

var configuredLogPath = builder.Configuration[$"{AgentOptions.SectionName}:LogPath"];
var logPath = string.IsNullOrWhiteSpace(configuredLogPath)
    ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "MyOnOff", "host-agent.log")
    : Path.GetFullPath(configuredLogPath, builder.Environment.ContentRootPath);
builder.Logging.AddProvider(new FileLoggerProvider(logPath));

builder.Services
    .AddOptions<AgentOptions>()
    .Bind(builder.Configuration.GetSection(AgentOptions.SectionName))
    .Validate(options => IsSafeListenUrl(options.ListenUrl), "Agent:ListenUrl must be an HTTP loopback, wildcard, or private-LAN URL.")
    .Validate(options => IsPrivateIp(options.HostIp), "Agent:HostIp must be a private IPv4 LAN address.")
    .Validate(options => IsValidSubnet(options.AllowedSubnet), "Agent:AllowedSubnet must be a valid IPv4 CIDR.")
    .Validate(options => options.SmbPort is > 0 and <= 65535, "Agent:SmbPort is invalid.")
    .Validate(options => options.SmbProbeTimeoutMilliseconds is >= 100 and <= 30000, "SMB probe timeout is invalid.")
    .ValidateOnStart();

var listenUrl = builder.Configuration[$"{AgentOptions.SectionName}:ListenUrl"] ?? "http://0.0.0.0:5055";
builder.WebHost.UseUrls(listenUrl);

builder.Services.AddSingleton<ISmbReadinessProbe, SmbReadinessProbe>();
builder.Services.AddSingleton<ISystemPowerService, WindowsSystemPowerService>();
builder.Services.AddSingleton<PowerCommandQueue>();
builder.Services.AddSingleton<IPowerCommandQueue>(services => services.GetRequiredService<PowerCommandQueue>());
builder.Services.AddHostedService<PowerCommandQueue>(services => services.GetRequiredService<PowerCommandQueue>());

var app = builder.Build();
app.UseMiddleware<LanOnlyMiddleware>();

app.MapGet(ApiRoutes.Status, async (
    ISmbReadinessProbe smbProbe,
    IOptions<AgentOptions> options,
    CancellationToken cancellationToken) =>
{
    var smbReady = await smbProbe.IsReadyAsync(cancellationToken);
    return Results.Ok(new HostStatusResponse(
        Status: "online",
        Hostname: Environment.MachineName,
        Ip: options.Value.HostIp,
        UptimeSeconds: Environment.TickCount64 / 1000,
        SmbReady: smbReady));
});

app.MapPost(ApiRoutes.Sleep, (
    HttpContext context,
    IOptions<AgentOptions> options,
    IPowerCommandQueue queue,
    ILogger<Program> logger) =>
    QueuePowerCommand(context, options, queue, logger, PowerCommand.Sleep));

app.MapPost(ApiRoutes.Shutdown, (
    HttpContext context,
    IOptions<AgentOptions> options,
    IPowerCommandQueue queue,
    ILogger<Program> logger) =>
    QueuePowerCommand(context, options, queue, logger, PowerCommand.Shutdown));

app.Logger.LogInformation("MyOnOff Host Agent starting on {ListenUrl}", listenUrl);
app.Run();

static bool IsValidSubnet(string cidr)
{
    try
    {
        _ = Ipv4Subnet.Contains(System.Net.IPAddress.Loopback, cidr);
        var networkText = cidr.Split('/', 2)[0];
        return System.Net.IPAddress.TryParse(networkText, out var network) && Ipv4Subnet.IsPrivateLan(network);
    }
    catch (Exception exception) when (exception is FormatException or ArgumentException)
    {
        return false;
    }
}

static bool IsPrivateIp(string value) =>
    System.Net.IPAddress.TryParse(value, out var address) && Ipv4Subnet.IsPrivateLan(address);

static bool IsSafeListenUrl(string value)
{
    if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttp)
    {
        return false;
    }

    if (uri.Host is "0.0.0.0" or "localhost")
    {
        return true;
    }

    return System.Net.IPAddress.TryParse(uri.Host, out var address) &&
           (System.Net.IPAddress.IsLoopback(address) || Ipv4Subnet.IsPrivateLan(address));
}

static IResult QueuePowerCommand(
    HttpContext context,
    IOptions<AgentOptions> options,
    IPowerCommandQueue queue,
    ILogger logger,
    PowerCommand command)
{
    if (!ControlAuthentication.IsConfigured(options))
    {
        logger.LogError("Rejected {PowerCommand}: no authentication token is configured", command);
        return Results.Problem(
            "The Host Agent authentication token is not configured.",
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    if (!ControlAuthentication.IsAuthorized(context.Request, options))
    {
        logger.LogWarning(
            "Rejected unauthenticated {PowerCommand} request from {RemoteAddress}",
            command,
            context.Connection.RemoteIpAddress);
        return Results.Unauthorized();
    }

    if (!queue.TryEnqueue(command))
    {
        return Results.Conflict(new { error = "Another power action is already pending." });
    }

    logger.LogInformation("Accepted authenticated {PowerCommand} request", command);
    return Results.Accepted(
        value: new ControlAcceptedResponse(
            command.ToString().ToLowerInvariant(),
            Accepted: true,
            Message: "Power action accepted."));
}
