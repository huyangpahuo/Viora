namespace Viora.Core.Diagnostics;

/// <summary>Exposes the active log file location (linked from Help/Settings).</summary>
public interface ILogFileProvider
{
    string CurrentLogFile { get; }

    string Folder { get; }
}
