using System.Windows;
using SlideCraft.Services;
using SlideCraft.ViewModels;

namespace SlideCraft.Views;

/// <summary>
/// La fenêtre d'écriture des sous-titres bilingues.
///
/// Le code-behind ne fait que trois choses que le XAML ne sait pas faire :
/// valider puis renvoyer les sous-titres, renvoyer « supprimer », et vider
/// les champs. La construction et la validation sont dans
/// <see cref="SubtitleEditorViewModel"/> (testable sans Windows).
/// </summary>
public partial class SubtitleEditorWindow : Window
{
    private readonly SubtitleEditorViewModel _viewModel;

    public SubtitleEditorWindow(SubtitleEditorViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = viewModel;
    }

    /// <summary>Résultat renvoyé au ViewModel principal (null si annulé).</summary>
    public SubtitleEditorResult? Result { get; private set; }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!_viewModel.Validate())
        {
            // Le message est déjà affiché en rouge sous les champs.
            return;
        }

        Result = new SubtitleEditorResult(true, _viewModel.Build());
        DialogResult = true;
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        MessageBoxResult answer = MessageBox.Show(
            this,
            "Remove the subtitles from this slide?",
            "SlideCraft",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (answer != MessageBoxResult.Yes)
        {
            return;
        }

        Result = new SubtitleEditorResult(true, null);
        DialogResult = true;
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.Clear();
    }
}
