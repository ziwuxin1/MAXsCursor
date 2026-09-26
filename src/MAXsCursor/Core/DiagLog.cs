using System.IO;

namespace MAXsCursor.Core;

// Append-only diagnostic log at %TEMP%\MAXsCursor.log. Never throws. Not for hot paths:
// it allocates and touches the disk, so hook callbacks must not call it.
internal static class DiagLog
{
    private static readonly string LogPath = Path.Combine(Path.GetTempPath(), "MAXsCursor.log");
    private static readonly object s_lock = new();

    public static void Write(string message)
    {
        try
        {
            lock (s_lock)
            {
                File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss.fff}] {message}{Environment.NewLine}");
            }
        }
        catch { }
    }
}
