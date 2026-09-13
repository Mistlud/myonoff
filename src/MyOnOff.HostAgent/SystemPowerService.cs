using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MyOnOff.HostAgent;

public interface ISystemPowerService
{
    Task SleepAsync(CancellationToken cancellationToken);
    Task ShutdownAsync(CancellationToken cancellationToken);
}

public sealed class WindowsSystemPowerService : ISystemPowerService
{
    public Task SleepAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureWindows();

        if (!SetSuspendState(hibernate: false, forceCritical: false, disableWakeEvent: false))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows rejected the sleep request.");
        }

        return Task.CompletedTask;
    }

    public Task ShutdownAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureWindows();

        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "shutdown.exe",
            Arguments = "/s /t 0 /d p:0:0 /c \"Requested by MyOnOff Host Agent\"",
            UseShellExecute = false,
            CreateNoWindow = true
        });

        if (process is null)
        {
            throw new InvalidOperationException("Unable to start shutdown.exe.");
        }

        return Task.CompletedTask;
    }

    private static void EnsureWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Power actions are supported only on Windows.");
        }
    }

    [DllImport("PowrProf.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetSuspendState(
        [MarshalAs(UnmanagedType.Bool)] bool hibernate,
        [MarshalAs(UnmanagedType.Bool)] bool forceCritical,
        [MarshalAs(UnmanagedType.Bool)] bool disableWakeEvent);
}
