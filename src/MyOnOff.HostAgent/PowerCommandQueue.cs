using System.Threading.Channels;

namespace MyOnOff.HostAgent;

public enum PowerCommand
{
    Sleep,
    Shutdown
}

public interface IPowerCommandQueue
{
    bool TryEnqueue(PowerCommand command);
}

public sealed class PowerCommandQueue(
    ISystemPowerService powerService,
    ILogger<PowerCommandQueue> logger) : BackgroundService, IPowerCommandQueue
{
    private int _pending;
    private readonly Channel<PowerCommand> _commands = Channel.CreateBounded<PowerCommand>(
        new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true,
            SingleWriter = false
        });

    public bool TryEnqueue(PowerCommand command)
    {
        if (Interlocked.CompareExchange(ref _pending, 1, 0) != 0)
        {
            return false;
        }

        if (_commands.Writer.TryWrite(command))
        {
            return true;
        }

        Interlocked.Exchange(ref _pending, 0);
        return false;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var command in _commands.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                // Let ASP.NET flush the 202 response before the network disappears.
                await Task.Delay(TimeSpan.FromMilliseconds(750), stoppingToken);
                logger.LogInformation("Executing authenticated {PowerCommand} request", command);

                if (command == PowerCommand.Sleep)
                {
                    await powerService.SleepAsync(stoppingToken);
                }
                else
                {
                    await powerService.ShutdownAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Power command {PowerCommand} failed", command);
            }
            finally
            {
                Interlocked.Exchange(ref _pending, 0);
            }
        }
    }
}
