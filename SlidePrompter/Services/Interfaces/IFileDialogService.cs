namespace SlidePrompter.Services.Interfaces;

/// <summary>
/// Abstracts open/save script file dialogs for clean MVVM separation and testability.
/// </summary>
public interface IFileDialogService
{
    string? ShowOpenScriptDialog();

    string? ShowSaveScriptDialog(string? suggestedFilePath = null);
}

