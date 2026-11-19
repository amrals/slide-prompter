using SlidePrompter.Models;

namespace SlidePrompter.Services.Interfaces;

/// <summary>
/// Loads and persists application settings in JSON format.
/// </summary>
public interface ISettingsService
{
    string SettingsFilePath { get; }

    AppSettings Load();

    void Save(AppSettings settings);
}

