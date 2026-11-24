using System.Windows;
using SlideCraft.ViewModels;

namespace SlideCraft.Views;

/// <summary>
/// La fenêtre du rapport SmartSlide.
///
/// Elle n'a presque pas de code : le ViewModel fournit la liste des analyses
/// et le détail sélectionné, et le bouton « Fermer » utilise IsCancel="True"
/// dans le XAML, ce qui suffit à fermer la fenêtre (y compris avec Échap).
/// </summary>
public partial class SmartSlideWindow : Window
{
    public SmartSlideWindow(SmartSlideViewModel viewModel)
    {
        InitializeComponent();

        DataContext = viewModel ?? throw new System.ArgumentNullException(nameof(viewModel));
    }
}
