using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using SlideCraft.Models;
using SlideCraft.ViewModels;
using SlideCraft.Views;

namespace SlideCraft.Services;

/// <summary>
/// Implémentation réelle de <see cref="IReviewService"/> : elle construit le
/// résumé, crée la fenêtre et l'affiche en modal.
///
/// Ce fichier utilise WPF (fenêtre et propriétaire) : les tests automatiques
/// l'ignorent, mais les compteurs, eux, sont dans
/// <see cref="ReviewViewModel"/> et sont testés.
/// </summary>
public class ReviewService : IReviewService
{
    public void Show(
        Presentation presentation,
        IReadOnlyList<string> history,
        ICommand? undoCommand,
        ICommand? redoCommand)
    {
        if (presentation is null || presentation.Slides.Count == 0)
        {
            return;
        }

        var viewModel = new ReviewViewModel(presentation, history, undoCommand, redoCommand);
        var window = new ReviewWindow(viewModel);

        if (Application.Current?.MainWindow is { } mainWindow
            && !ReferenceEquals(mainWindow, window))
        {
            window.Owner = mainWindow;
        }

        window.ShowDialog();
    }
}
