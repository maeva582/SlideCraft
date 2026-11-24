using System.Windows;
using SlideCraft.Models;
using SlideCraft.ViewModels;
using SlideCraft.Views;

namespace SlideCraft.Services;

/// <summary>
/// Implémentation réelle de <see cref="IQuizEditorService"/> : crée le
/// ViewModel de l'éditeur, ouvre la fenêtre en modal et renvoie le résultat.
/// </summary>
public class QuizEditorService : IQuizEditorService
{
    public QuizEditorResult? Edit(Quiz? quiz, string slideTitle)
    {
        var viewModel = new QuizEditorViewModel(quiz, slideTitle);
        var window = new QuizEditorWindow(viewModel);

        if (Application.Current?.MainWindow is { } mainWindow
            && !ReferenceEquals(mainWindow, window))
        {
            window.Owner = mainWindow;
        }

        return window.ShowDialog() == true ? window.Result : null;
    }
}
