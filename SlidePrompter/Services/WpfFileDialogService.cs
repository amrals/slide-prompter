using System.IO;
using SlidePrompter.Services.Interfaces;

namespace SlidePrompter.Services;

/// <summary>
/// WPF file dialog implementation for importing and saving Markdown/Text scripts.
/// </summary>
public sealed class WpfFileDialogService : IFileDialogService
{
    private const string ScriptFilter = "Roteiros (*.md;*.txt)|*.md;*.txt|Markdown (*.md)|*.md|Texto (*.txt)|*.txt|Todos os arquivos (*.*)|*.*";

    public string? ShowOpenScriptDialog()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Importar Roteiro de Apresentação",
            Filter = ScriptFilter,
            CheckFileExists = true
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? ShowSaveScriptDialog(string? suggestedFilePath = null)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Salvar Roteiro de Apresentação",
            Filter = ScriptFilter,
            DefaultExt = ".md",
            AddExtension = true
        };

        if (!string.IsNullOrWhiteSpace(suggestedFilePath))
        {
            dialog.FileName = Path.GetFileName(suggestedFilePath);
            string? directory = Path.GetDirectoryName(suggestedFilePath);
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            {
                dialog.InitialDirectory = directory;
            }
        }
        else
        {
            dialog.FileName = "roteiro-apresentacao.md";
        }

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}

