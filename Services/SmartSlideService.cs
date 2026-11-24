using System.Collections.Generic;
using System.Windows;
using SlideCraft.Models;
using SlideCraft.ViewModels;
using SlideCraft.Views;

namespace SlideCraft.Services;

/// <summary>
/// Implémentation réelle de <see cref="ISmartSlideService"/> :
/// 1. on analyse la présentation (calcul local, instantané) ;
/// 2. on construit le ViewModel du rapport ;
/// 3. on affiche la fenêtre en modal.
///
/// Ce fichier utilise WPF : les tests automatiques l'ignorent, mais
/// l'analyse elle-même (<see cref="SmartSlideAnalyzer"/>) est testée.
/// </summary>
public class SmartSlideService : ISmartSlideService
{
    public void Show(Presentation presentation, int startIndex)
    {
        if (presentation is null || presentation.Slides.Count == 0)
        {
            return;
        }

        IReadOnlyList<SlideAnalysis> analyses = SmartSlideAnalyzer.Analyze(presentation);
        var viewModel = new SmartSlideViewModel(analyses, startIndex);
        var window = new SmartSlideWindow(viewModel);

        if (Application.Current?.MainWindow is { } mainWindow
            && !ReferenceEquals(mainWindow, window))
        {
            window.Owner = mainWindow;
        }

        window.ShowDialog();
    }
}
