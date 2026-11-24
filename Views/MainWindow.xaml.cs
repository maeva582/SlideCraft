using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using SlideCraft.Models;
using SlideCraft.Services;
using SlideCraft.ViewModels;

namespace SlideCraft.Views;

/// <summary>
/// Code-behind de la fenêtre principale.
///
/// Il reste volontairement court : il ne contient que ce qui concerne
/// directement la fenêtre (déplacer, réduire, fermer) et les gestes de
/// souris impossibles à exprimer en XAML (glisser un élément, le
/// redimensionner). Toute la logique métier est dans <see cref="MainViewModel"/>.
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    // --- État du glisser en cours (déplacement d'un élément) ---
    private SlideElement? _draggedElement;
    private Point _dragStartPoint;
    private double _dragStartX;
    private double _dragStartY;

    /// <summary>Vrai dès que la souris a réellement bougé pendant un appui.</summary>
    private bool _dragMoved;

    public MainWindow()
    {
        InitializeComponent();

        // La fenêtre fournit au ViewModel les services qui ont besoin de WPF :
        // les boîtes de dialogue Windows, le diaporama, SmartSlide, l'éditeur
        // de quiz, la saisie des joueurs et l'éditeur de sous-titres.
        _viewModel = new MainViewModel(
            PresentationFactory.CreateSamplePresentation(),
            new FileDialogService(),
            new SlideShowService(),
            new SmartSlideService(),
            new QuizEditorService(),
            new GameSetupService(),
            new SubtitleEditorService(),
            new ReviewService());

        DataContext = _viewModel;
    }

    // ------------------------------------------------------------------
    // Barre de titre personnalisée
    // ------------------------------------------------------------------

    /// <summary>
    /// Clic sur la barre de titre : on déplace la fenêtre.
    /// Double-clic : on bascule entre normal et plein écran.
    /// </summary>
    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleMaximize();
            return;
        }

        // Si l'utilisateur clique sur un bouton de la barre (réduire, fermer...),
        // on ne déplace pas la fenêtre.
        if (HasButtonAncestor(e.OriginalSource as DependencyObject))
        {
            return;
        }

        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    /// <summary>Remonte l'arbre visuel pour savoir si on a cliqué dans un bouton.</summary>
    private static bool HasButtonAncestor(DependencyObject? element)
    {
        while (element is not null)
        {
            if (element is Button)
            {
                return true;
            }

            element = VisualTreeHelper.GetParent(element);
        }

        return false;
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        ToggleMaximize();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    /// <summary>
    /// Appelé quand l'utilisateur ferme la fenêtre (bouton Fermer, Alt+F4 ou
    /// via le menu Windows). On demande confirmation si des modifications ne
    /// sont pas enregistrées ; annuler la fermeture = e.Cancel = true.
    /// </summary>
    private void Window_Closing(object sender, CancelEventArgs e)
    {
        if (!_viewModel.ConfirmDiscardChanges())
        {
            e.Cancel = true;
        }
    }

    private void ToggleMaximize()
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    // ------------------------------------------------------------------
    // Sélection et déplacement des éléments sur la diapositive
    // ------------------------------------------------------------------

    /// <summary>
    /// Clic sur le fond de la diapositive : on désélectionne l'élément.
    /// </summary>
    private void SlideCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _viewModel.SelectElement(null);
    }

    /// <summary>
    /// Appui sur un élément : on le sélectionne et on prépare un déplacement
    /// éventuel. La souris est capturée pour continuer à recevoir les
    /// mouvements même si le curseur sort de l'élément.
    /// </summary>
    private void Element_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: SlideElement element } frameworkElement)
        {
            return;
        }

        _viewModel.SelectElement(element);

        // Double-clic sur un texte : on passe en édition directe au lieu de
        // commencer un déplacement.
        if (e.ClickCount == 2 && element is TextElement)
        {
            _draggedElement = null;
            _viewModel.BeginTextEditing(element);
            e.Handled = true;
            return;
        }

        // On retient le point de départ et la position d'origine : le
        // déplacement se calcule toujours par rapport à eux, ce qui évite
        // d'accumuler les petites erreurs d'arrondi. La photo servant à
        // l'annulation n'est prise qu'au premier vrai mouvement.
        _draggedElement = element;
        _dragMoved = false;
        _dragStartPoint = e.GetPosition(this);
        _dragStartX = element.X;
        _dragStartY = element.Y;

        frameworkElement.CaptureMouse();

        e.Handled = true;
    }

    /// <summary>
    /// Souris en mouvement : on déplace l'élément.
    /// Les pixels de l'écran sont divisés par le zoom, car la diapositive est
    /// affichée dans un repère fixe de 960 x 540 mis à l'échelle.
    /// </summary>
    private void Element_MouseMove(object sender, MouseEventArgs e)
    {
        if (_draggedElement is null || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        Point current = e.GetPosition(this);

        if (!_dragMoved)
        {
            // Un simple clic ne doit rien coûter : on attend un mouvement
            // réel (1 pixel à l'écran) avant de mémoriser l'état d'annulation.
            if (Math.Abs(current.X - _dragStartPoint.X) < 1
                && Math.Abs(current.Y - _dragStartPoint.Y) < 1)
            {
                return;
            }

            _dragMoved = true;
            _viewModel.BeginElementDrag();
        }

        double zoom = _viewModel.ZoomFactor;
        double dx = (current.X - _dragStartPoint.X) / zoom;
        double dy = (current.Y - _dragStartPoint.Y) / zoom;

        // On garde l'élément à l'intérieur de la diapositive.
        _draggedElement.X = Math.Clamp(
            _dragStartX + dx, 0, Math.Max(0, MainViewModel.SlideWidth - _draggedElement.Width));
        _draggedElement.Y = Math.Clamp(
            _dragStartY + dy, 0, Math.Max(0, MainViewModel.SlideHeight - _draggedElement.Height));

        // Le panneau de propriétés affiche X et Y : on le prévient.
        _viewModel.NotifySelectedElementChanged();
    }

    /// <summary>Relâchement de la souris : fin du déplacement.</summary>
    private void Element_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_draggedElement is null)
        {
            return;
        }

        if (sender is FrameworkElement frameworkElement)
        {
            // La libération de la capture déclenche Element_LostMouseCapture,
            // qui termine le déplacement.
            frameworkElement.ReleaseMouseCapture();
        }

        FinishDrag();
        e.Handled = true;
    }

    /// <summary>
    /// La capture de la souris est perdue (relâchement, Alt+Tab, autre fenêtre
    /// qui prend le focus...) : on termine le déplacement proprement, sinon
    /// l'action resterait non annulable et le document « non modifié ».
    /// </summary>
    private void Element_LostMouseCapture(object sender, MouseEventArgs e)
    {
        FinishDrag();
    }

    /// <summary>Termine le déplacement en cours, s'il y en a un.</summary>
    private void FinishDrag()
    {
        if (_draggedElement is null)
        {
            return;
        }

        _draggedElement = null;

        if (!_dragMoved)
        {
            return;
        }

        _dragMoved = false;
        _viewModel.EndElementDrag();
    }

    // ------------------------------------------------------------------
    // Édition directe : texte et cellules de tableau
    // ------------------------------------------------------------------

    /// <summary>
    /// Double-clic sur une cellule de tableau : on édite cette cellule.
    /// Un simple clic laisse l'événement remonter pour sélectionner le tableau.
    /// </summary>
    private void TableCell_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: TableCell cell })
        {
            return;
        }

        if (e.ClickCount == 2)
        {
            _viewModel.BeginCellEditing(cell);
            e.Handled = true;
        }
    }

    /// <summary>
    /// La zone de saisie vient d'apparaître : on lui donne le focus pour que
    /// l'utilisateur puisse taper tout de suite. On passe par le Dispatcher
    /// car WPF n'a pas encore terminé la mise en page à cet instant.
    /// </summary>
    private void EditableTextBox_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is not TextBox textBox || !textBox.IsVisible)
        {
            return;
        }

        textBox.Dispatcher.BeginInvoke(
            new Action(() =>
            {
                textBox.Focus();
                textBox.CaretIndex = textBox.Text.Length;
            }),
            DispatcherPriority.Input);
    }

    /// <summary>L'utilisateur clique ailleurs : la saisie est validée.</summary>
    private void EditableTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        _viewModel.EndInlineEditing();
    }

    /// <summary>
    /// Échap annule la saisie. Entrée valide une cellule (le texte, lui,
    /// accepte les retours à la ligne puisque AcceptsReturn vaut true).
    /// </summary>
    private void EditableTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            _viewModel.CancelInlineEditing();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter && sender is TextBox { AcceptsReturn: false })
        {
            _viewModel.EndInlineEditing();
            e.Handled = true;
        }
    }

    // ------------------------------------------------------------------
    // Redimensionnement par la poignée (Thumb) en bas à droite
    // ------------------------------------------------------------------

    /// <summary>Début du redimensionnement : on mémorise l'état pour l'annulation.</summary>
    private void ResizeGrip_DragStarted(object sender, DragStartedEventArgs e)
    {
        _viewModel.BeginElementDrag();
    }

    /// <summary>Pendant le redimensionnement : on agrandit ou rétrécit l'élément.</summary>
    private void ResizeGrip_DragDelta(object sender, DragDeltaEventArgs e)
    {
        // La poignée appartient à un élément précis : on redimensionne celui-là
        // (et non « l'élément sélectionné », qui pourrait avoir changé).
        if (sender is not FrameworkElement { DataContext: SlideElement element })
        {
            return;
        }

        double zoom = _viewModel.ZoomFactor;

        element.Width = Math.Clamp(
            element.Width + (e.HorizontalChange / zoom),
            40,
            Math.Max(40, MainViewModel.SlideWidth - element.X));

        element.Height = Math.Clamp(
            element.Height + (e.VerticalChange / zoom),
            30,
            Math.Max(30, MainViewModel.SlideHeight - element.Y));

        _viewModel.NotifySelectedElementChanged();
    }

    /// <summary>Fin du redimensionnement : l'action devient annulable.</summary>
    private void ResizeGrip_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        _viewModel.EndElementDrag();
    }

    // ------------------------------------------------------------------
    // Vidéo dans l'éditeur
    // ------------------------------------------------------------------

    /// <summary>
    /// Lance la vidéo quand sa diapositive s'affiche. Elle est muette dans
    /// l'éditeur (le son est réservé au diaporama) et tourne en boucle pour
    /// qu'on puisse vérifier son contenu.
    /// </summary>
    private void VideoPlayer_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not MediaElement player)
        {
            return;
        }

        try
        {
            player.Position = TimeSpan.Zero;
            player.Play();
        }
        catch (Exception)
        {
            // Format non pris en charge par Windows : le cadre reste noir
            // et le reste de la diapositive continue de s'afficher.
        }
    }

    /// <summary>Libère le lecteur quand l'élément quitte l'écran.</summary>
    private void VideoPlayer_Unloaded(object sender, RoutedEventArgs e)
    {
        if (sender is not MediaElement player)
        {
            return;
        }

        try
        {
            player.Close();
        }
        catch (Exception)
        {
            // Fermer un lecteur déjà libéré n'est pas une erreur à signaler.
        }
    }

    /// <summary>Relance la vidéo quand elle arrive à la fin (lecture en boucle).</summary>
    private void VideoPlayer_MediaEnded(object sender, RoutedEventArgs e)
    {
        if (sender is not MediaElement player)
        {
            return;
        }

        try
        {
            player.Position = TimeSpan.Zero;
            player.Play();
        }
        catch (Exception)
        {
            // Même raison que dans VideoPlayer_Loaded.
        }
    }

    // ------------------------------------------------------------------
    // Aide et choix de la voix de narration
    // ------------------------------------------------------------------

    private void NarrationVoiceButton_Click(object sender, RoutedEventArgs e)
    {
        var window = new NarrationVoiceWindow { Owner = this };
        window.ShowDialog();
    }

    private void HelpButton_Click(object sender, RoutedEventArgs e)
    {
        var window = new HelpWindow { Owner = this };
        window.ShowDialog();
    }
}
