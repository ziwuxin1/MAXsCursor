using System.IO;
using System.Text.Json;
using MAXsCursor.Core;

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
        if (!File.Exists(SettingsPath)) return SettingsModel.Defaults();

        try
        {
            var json = File.ReadAllText(SettingsPath);
            var model = JsonSerializer.Deserialize<SettingsModel>(json, JsonOptions);
            if (model is not null)
            {
                if (model.Migrate()) Save(model);
                return model;
            }
            DiagLog.Write("WARN: settings.json deserialized to null, using defaults");
        }
        catch (Exception ex)
        {
            DiagLog.Write($"WARN: settings.json unreadable, using defaults: {ex.Message}");
        }

        // The app must still launch, so fall back to defaults. Keep the bad file aside first,
        // because the next save would otherwise overwrite the user's settings for good.
        BackUpUnreadableFile();
        return SettingsModel.Defaults();
    }

    public static void Save(SettingsModel model)
    {
        var tempPath = SettingsPath + ".tmp";
        try
        {
            var dir = Path.GetDirectoryName(SettingsPath)!;
            Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(model, JsonOptions);

            // Write the full file next to the real one, then swap it in. A crash or power loss
            // mid-write leaves the previous settings.json intact instead of a truncated file.
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, SettingsPath, overwrite: true);
        }
        catch (Exception ex)
        {
            // Persisting settings must never crash the app. The user can re-save from the UI.
            DiagLog.Write($"WARN: saving settings failed: {ex.Message}");
            try { File.Delete(tempPath); } catch { }
        }
    }

    private static void BackUpUnreadableFile()
    {
        try
        {
            var backup = $"{SettingsPath}.bad-{DateTime.Now:yyyyMMdd-HHmmss}";
            File.Copy(SettingsPath, backup, overwrite: true);
            DiagLog.Write($"unreadable settings backed up to {backup}");
        }
        catch (Exception ex)
        {
            DiagLog.Write($"WARN: backing up unreadable settings failed: {ex.Message}");
        }
    }
}
