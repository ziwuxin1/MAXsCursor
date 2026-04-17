using System.IO;
using System.Text.Json;

namespace MAXsCursor.Settings;

internal static class SettingsStore
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MAXsCursor",
        "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static string FilePath => SettingsPath;

    public static SettingsModel Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                var model = JsonSerializer.Deserialize<SettingsModel>(json, JsonOptions);
                if (model is not null) return model;
            }
        }
        catch
        {
            // Corrupt or unreadable file falls through to defaults. We do not surface an error
            // because the app must still launch and the user can re-save from the UI.
        }
        return SettingsModel.Defaults();
    }

    public static void Save(SettingsModel model)
    {
        try
        {
            var dir = Path.GetDirectoryName(SettingsPath)!;
            Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(model, JsonOptions);
            File.WriteAllText(SettingsPath, json);
        }
        catch
        {
            // Persisting settings must never crash the app. If the write fails the user just
            // loses the change, which is recoverable by re-saving from the UI.
        }
    }
}
