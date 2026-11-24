using System.Collections.Generic;
using System.Windows;
using SlideCraft.Models;
using SlideCraft.ViewModels;
using SlideCraft.Views;

namespace SlideCraft.Services;

/// <summary>
/// Implémentation réelle de <see cref="ISlideShowService"/> : elle crée la
/// fenêtre <see cref="SlideShowWindow"/>, lui donne son ViewModel, puis
/// l'affiche en modal (ShowDialog).
///
/// « Modale » = on ne peut rien faire dans la fenêtre principale tant que le
/// diaporama est ouvert, exactement comme dans PowerPoint.
///
/// Ce fichier fait partie des rares fichiers qui utilisent WPF : c'est pour
/// cela que les tests automatiques l'ignorent.
/// </summary>
public class SlideShowService : ISlideShowService
{
    public void Show(Presentation presentation, int startIndex, IReadOnlyList<string>? players = null)
    {
        // Double sécurité : le bouton est déjà désactivé sans diapositive,
        // mais un appel direct ne doit jamais provoquer de plantage.
        if (presentation is null || presentation.Slides.Count == 0)
        {
            return;
        }

        // La voix est créée pour la durée du diaporama seulement : elle est
        // libérée en quittant, pour ne pas garder le moteur vocal occupé.
        using var narration = new NarrationService();

        // Le mode jeu est actif dès qu'un diaporama est lancé : la session
        // existe même sans joueur (les points vont alors à la salle).
        var game = new GameSession(players);

        var viewModel = new SlideShowViewModel(presentation.Slides, startIndex, narration, game);
        var window = new SlideShowWindow(viewModel);

        Window? mainWindow = Application.Current?.MainWindow;
        if (mainWindow is not null && !ReferenceEquals(mainWindow, window))
        {
            window.Owner = mainWindow;
        }

        window.ShowDialog();

        // À la sortie du diaporama, on rend le clavier à l'éditeur : sans cela,
        // Ctrl+Z ou F5 peuvent rester sans effet tant qu'on n'a pas cliqué.
        if (mainWindow is { IsVisible: true })
        {
            mainWindow.Activate();
        }
    }
}
