using System.Collections.Concurrent;
using System.IO;
using Microsoft.Extensions.Logging;

namespace Viora.Infrastructure.Logging;

public sealed class LogFileProvider : Core.Diagnostics.ILogFileProvider
{
    public LogFileProvider(IAppPaths paths)
    {
        Folder = paths.LogsFolder;
        CurrentLogFile = Path.Combine(Folder, $"viora-{DateTime.Now:yyyyMMdd}.log");
    }

    /// <summary>Convenience for the composition root: computes the log path without instantiating DI.</summary>
    public static string ComputeLogFile(string logsFolder) =>
        Path.Combine(logsFolder, $"viora-{DateTime.Now:yyyyMMdd}.log");

    public string CurrentLogFile { get; }

    public string Folder { get; }
}

/// <summary>
/// Minimal async file sink for Microsoft.Extensions.Logging (Info default, level set from settings).
/// One line per event: timestamp, level, category, message, optional exception.
/// </summary>
public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly BlockingCollection<string> _queue = new(boundedCapacity: 4096);
    private readonly StreamWriter _writer;
    private readonly Thread _pump;
    private LogLevel _minimumLevel = LogLevel.Information;
    private bool _redactPaths = true;

    public FileLoggerProvider(string path, LogLevel initialLevel = LogLevel.Information)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        _writer = new StreamWriter(
            new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite)) { AutoFlush = false };
        _minimumLevel = initialLevel;
        _pump = new Thread(Pump) { IsBackground = true, Name = "Viora.FileLogger" };
        _pump.Start();
    }

    public void SetLevel(LogLevel level) => _minimumLevel = level;

    public void SetRedactPaths(bool redact) => _redactPaths = redact;

    public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName);

    private void Enqueue(string line)
    {
        if (!_queue.IsAddingCompleted)
            _queue.TryAdd(line);
    }

    private void Pump()
    {
        try
        {
            foreach (var line in _queue.GetConsumingEnumerable())
            {
                _writer.WriteLine(line);
                // Batch flush: every line would be slow; idle flush happens on disposal.
                if (_queue.Count == 0) _writer.Flush();
            }
        }
        catch (ObjectDisposedException) { }
    }

    public void Dispose()
    {
        _queue.CompleteAdding();
        _pump.Join(1500);
        try { _writer.Flush(); _writer.Dispose(); } catch { /* best effort at shutdown */ }
        _queue.Dispose();
    }

    private sealed class FileLogger : ILogger
    {
        private readonly FileLoggerProvider _provider;
        private readonly string _category;

        public FileLogger(FileLoggerProvider provider, string category)
        {
            _provider = provider;
            _category = category.Length > 48 ? category[(category.LastIndexOf('.') + 1)..] : category;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= _provider._minimumLevel && logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;

            string message = formatter(state, exception);
            if (_provider._redactPaths && message.Contains('\\'))
                message = AppPaths.RedactPath(message);

            string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{logLevel.ToString()[..4].ToUpperInvariant()}] {_category}: {message}";
            if (exception is not null)
                line += Environment.NewLine + (_provider._redactPaths
                    ? AppPaths_Redact(exception.ToString())
                    : exception.ToString());

            _provider.Enqueue(line);
        }

        private static string AppPaths_Redact(string text)
        {
            string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return string.IsNullOrEmpty(profile) ? text : text.Replace(profile, "…", StringComparison.OrdinalIgnoreCase);
        }
    }
}

/// <summary>Registers the file provider on the standard builder from the composition root.</summary>
public static class FileLoggerFactoryExtensions
{
    public static ILoggingBuilder AddVioraFile(this ILoggingBuilder builder, FileLoggerProvider provider)
        => builder.AddProvider(provider);
}
