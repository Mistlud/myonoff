using System.IO;
using System.Text.Json;

namespace MyOnOff.DesktopController;

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public string SettingsPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MyOnOff",
        "controller-settings.json");

    public async Task<ControllerSettings> LoadAsync()
    {
        if (!File.Exists(SettingsPath))
        {
            var defaults = new ControllerSettings();
            await SaveAsync(defaults);
            return defaults;
        }

        await using var stream = File.OpenRead(SettingsPath);
        return await JsonSerializer.DeserializeAsync<ControllerSettings>(stream, JsonOptions)
               ?? new ControllerSettings();
    }

    public async Task SaveAsync(ControllerSettings settings)
    {
        var directory = Path.GetDirectoryName(SettingsPath)
                        ?? throw new InvalidOperationException("Settings directory is unavailable.");
        Directory.CreateDirectory(directory);
        await using var stream = File.Create(SettingsPath);
        await JsonSerializer.SerializeAsync(stream, settings, JsonOptions);
    }
}
