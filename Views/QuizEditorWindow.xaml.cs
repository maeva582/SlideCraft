using System.Windows;
using SlideCraft.Services;
using SlideCraft.ViewModels;

namespace SlideCraft.Views;

/// <summary>
/// La fenêtre d'édition du quiz d'une diapositive.
///
/// Le code-behind ne fait que trois choses que le XAML ne sait pas faire :
/// valider puis renvoyer le quiz, renvoyer « supprimer », et vider les champs.
/// La construction du quiz et la validation sont dans
/// <see cref="QuizEditorViewModel"/> (testable sans Windows).
/// </summary>
public partial class QuizEditorWindow : Window
{
    private readonly QuizEditorViewModel _viewModel;

    public QuizEditorWindow(QuizEditorViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = viewModel;
    }

    /// <summary>Résultat renvoyé au ViewModel principal (null si annulé).</summary>
    public QuizEditorResult? Result { get; private set; }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!_viewModel.Validate())
        {
            // Le message est déjà affiché en rouge sous les champs.
            return;
        }

        Result = new QuizEditorResult(true, _viewModel.BuildQuiz());
        DialogResult = true;
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        MessageBoxResult answer = MessageBox.Show(
            this,
            "Remove the quiz from this slide?",
            "SlideCraft",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (answer != MessageBoxResult.Yes)
        {
            return;
        }

        Result = new QuizEditorResult(true, null);
        DialogResult = true;
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.Clear();
    }
}
