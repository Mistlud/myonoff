namespace MyOnOff.DesktopController;

public static class AppLog
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MyOnOff",
        "controller.log");

    public static async Task WriteAsync(string message)
    {
        try
        {
            await Gate.WaitAsync();
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            await File.AppendAllTextAsync(
                LogPath,
                $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}");
        }
        catch
        {
            // Logging must never crash the controller.
        }
        finally
        {
            if (Gate.CurrentCount == 0)
            {
                Gate.Release();
            }
        }
    }
}
