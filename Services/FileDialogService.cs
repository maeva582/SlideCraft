using System.Windows;
using Microsoft.Win32;

namespace SlideCraft.Services;

/// <summary>
/// Version « réelle » de <see cref="IFileDialogService"/> : elle affiche les
/// boîtes de dialogue standard de Windows.
///
/// Ce fichier est l'un des rares de la couche Services qui dépend de WPF,
/// c'est pourquoi le ViewModel ne le connaît pas directement.
/// </summary>
public class FileDialogService : IFileDialogService
{
    public string? PickImageFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Choose an image",
            Filter = "Images (*.png;*.jpg;*.jpeg;*.bmp;*.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.gif"
                     + "|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        // ShowDialog() renvoie true quand l'utilisateur valide.
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? PickVideoFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Choose a video",
            Filter = "Videos (*.mp4;*.wmv;*.avi;*.mov;*.m4v)|*.mp4;*.wmv;*.avi;*.mov;*.m4v"
                     + "|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? PickOpenFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Open a presentation",
            Filter = PresentationFileService.FileFilter,
            DefaultExt = PresentationFileService.FileExtension,
            CheckFileExists = true,
            Multiselect = false
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? PickPowerPointFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Import a PowerPoint presentation",
            Filter = PowerPointImporter.FileFilter,
            DefaultExt = PowerPointImporter.FileExtension,
            CheckFileExists = true,
            Multiselect = false
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? PickSaveFile(string suggestedName, string filter, string defaultExtension)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Save as",
            Filter = filter,
            DefaultExt = defaultExtension,
            AddExtension = true,
            // Windows demande lui-même confirmation avant d'écraser un fichier.
            OverwritePrompt = true,
            FileName = suggestedName
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public bool Confirm(string message)
    {
        return MessageBox.Show(
            message,
            "SlideCraft",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question) == MessageBoxResult.Yes;
    }

    public void ShowError(string message)
    {
        MessageBox.Show(message, "SlideCraft", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
