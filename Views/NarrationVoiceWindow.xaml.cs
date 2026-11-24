using System;
using System.Windows;
using SlideCraft.Services;
using SlideCraft.ViewModels;

namespace SlideCraft.Views;

/// <summary>
/// Code-behind de la fenêtre « Narration voice ».
///
/// Il ne fait que brancher la fenêtre sur son ViewModel : la liste des voix,
/// l'essai et l'enregistrement du choix sont dans
/// <see cref="NarrationVoiceViewModel"/> et <see cref="NarrationService"/>.
/// </summary>
public partial class NarrationVoiceWindow : Window
{
    private readonly NarrationService _narration;
    private readonly NarrationVoiceViewModel _viewModel;

    public NarrationVoiceWindow()
    {
        InitializeComponent();

        // Un moteur vocal réservé à cette fenêtre : il sert à lister les voix
        // et à jouer la phrase d'essai, puis il est libéré à la fermeture.
        _narration = new NarrationService();
        _viewModel = new NarrationVoiceViewModel(_narration);
        DataContext = _viewModel;
    }

    private void UseButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_viewModel.Apply())
        {
            MessageBox.Show(
                this,
                "Choose an installed voice first.",
                "SlideCraft",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    protected override void OnClosed(EventArgs e)
    {
        // Coupe l'essai en cours et rend le moteur vocal à Windows.
        _narration.Dispose();
        base.OnClosed(e);
    }
}
