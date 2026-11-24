using System.Windows;
using SlideCraft.ViewModels;

namespace SlideCraft.Views;

/// <summary>
/// La fenêtre « Review summary » (onglet Review).
///
/// Elle ne contient aucun calcul : tout est dans
/// <see cref="ReviewViewModel"/>, pour que le résumé puisse être vérifié
/// sans Windows. Le code-behind ne fait qu'afficher et se fermer.
/// </summary>
public partial class ReviewWindow : Window
{
    public ReviewWindow(ReviewViewModel viewModel)
    {
        InitializeComponent();

        DataContext = viewModel;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
