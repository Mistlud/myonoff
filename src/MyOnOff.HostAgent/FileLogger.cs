using Microsoft.Extensions.Logging;

namespace MyOnOff.HostAgent;

public sealed class FileLoggerProvider(string path) : ILoggerProvider
{
    private readonly object _gate = new();

    public ILogger CreateLogger(string categoryName) => new FileLogger(path, categoryName, _gate);

    public void Dispose()
    {
    }

    private sealed class FileLogger(string path, string categoryName, object gate) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var line = $"{DateTimeOffset.Now:O} [{logLevel}] {categoryName}: {formatter(state, exception)}";
            if (exception is not null)
            {
                line += $" | {exception.GetType().Name}: {exception.Message}";
            }

            try
            {
                lock (gate)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    File.AppendAllText(path, line + Environment.NewLine);
                }
            }
            catch
            {
                // File logging is best-effort and must not stop power control.
            }
        }
    }
}
