using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using SlidePrompter.Models;
using SlidePrompter.Services.Interfaces;

namespace SlidePrompter.Services;

/// <summary>
/// Persists application settings in JSON under %AppData%/SlidePrompter/settings.json.
/// </summary>
public sealed class JsonSettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly object _syncLock = new();

    public string SettingsFilePath { get; }

    public JsonSettingsService()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SlidePrompter",
            "settings.json"))
    {
    }

    public JsonSettingsService(string settingsFilePath)
    {
        SettingsFilePath = settingsFilePath;
    }

    public AppSettings Load()
    {
        lock (_syncLock)
        {
            try
            {
                if (!File.Exists(SettingsFilePath))
                {
                    var defaults = new AppSettings();
                    defaults.Normalize();
                    return defaults;
                }

                string json = File.ReadAllText(SettingsFilePath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json, SerializerOptions) ?? new AppSettings();
                loaded.Normalize();
                return loaded;
            }
            catch
            {
                var fallback = new AppSettings();
                fallback.Normalize();
                return fallback;
            }
        }
    }

    public void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        lock (_syncLock)
        {
            try
            {
                var snapshot = settings.Clone();
                snapshot.Normalize();

                string? directory = Path.GetDirectoryName(SettingsFilePath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string json = JsonSerializer.Serialize(snapshot, SerializerOptions);
                string tempFile = SettingsFilePath + ".tmp";
                File.WriteAllText(tempFile, json);
                File.Move(tempFile, SettingsFilePath, overwrite: true);
            }
            catch
            {
                // Avoid interrupting user presentation flow if disk persistence temporarily fails.
            }
        }
    }
}

