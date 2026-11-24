using System.Windows;

namespace SlideCraft.Views;

/// <summary>
/// Fenêtre du guide rapide. Son contenu est fixe : aucun code métier ici,
/// seulement le bouton qui referme la fenêtre.
/// </summary>
public partial class HelpWindow : Window
{
    public HelpWindow()
    {
        InitializeComponent();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
