using System.Windows;
using SlideCraft.Models;
using SlideCraft.ViewModels;
using SlideCraft.Views;

namespace SlideCraft.Services;

/// <summary>
/// Implémentation réelle de <see cref="ISubtitleEditorService"/> : crée le
/// ViewModel, ouvre la fenêtre en modal et renvoie le résultat.
/// </summary>
public class SubtitleEditorService : ISubtitleEditorService
{
    public SubtitleEditorResult? Edit(SlideSubtitles? subtitles, string slideTitle)
    {
        var viewModel = new SubtitleEditorViewModel(subtitles, slideTitle);
        var window = new SubtitleEditorWindow(viewModel);

        if (Application.Current?.MainWindow is { } mainWindow
            && !ReferenceEquals(mainWindow, window))
        {
            window.Owner = mainWindow;
        }

        return window.ShowDialog() == true ? window.Result : null;
    }
}
