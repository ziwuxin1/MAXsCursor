using System.IO;
using System.Threading;

namespace MAXsCursor;

internal static class Program
{
    // Local\ (or no prefix) is per-session. Global\ requires SeCreateGlobalPrivilege and
    // throws UnauthorizedAccessException for non-admin users, causing silent startup failure.
    private const string SingleInstanceMutex = "Local\\MAXsCursor.SingleInstance.v1";

    [STAThread]
    public static int Main()
    {
        Log("=== Main() entered ===");

        Mutex? mutex = null;
        try
        {
            mutex = new Mutex(initiallyOwned: true, name: SingleInstanceMutex, out var createdNew);
            if (!createdNew)
            {
                Log("another instance already running, exiting");
                return 0;
            }

            AppDomain.CurrentDomain.UnhandledException += (_, ex) =>
                Log($"UNHANDLED: {ex.ExceptionObject}");

            Log("starting WPF App");
            var app = new App();
            app.DispatcherUnhandledException += (_, ex) =>
            {
                Log($"DISPATCHER UNHANDLED: {ex.Exception}");
                ex.Handled = false;
            };
            app.InitializeComponent();
            var code = app.Run();
            Log($"App.Run returned {code}");
            return code;
        }
        catch (Exception ex)
        {
            Log($"FATAL in Main: {ex}");
            return 1;
        }
        finally
        {
            mutex?.Dispose();
        }
    }

    private static void Log(string message)
    {
        try
        {
            var path = Path.Combine(Path.GetTempPath(), "MAXsCursor.log");
            File.AppendAllText(path, $"[{DateTime.Now:HH:mm:ss.fff}] {message}{Environment.NewLine}");
        }
        catch { }
    }
}
