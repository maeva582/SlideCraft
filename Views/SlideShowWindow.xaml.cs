using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using SlideCraft.Models;
using SlideCraft.ViewModels;

namespace SlideCraft.Views;

/// <summary>
/// La fenêtre du diaporama plein écran.
///
/// Le code-behind ne contient que ce que le XAML ne sait pas faire :
/// transformer les touches du clavier en appels au ViewModel, et donner
/// le focus à la fenêtre à l'ouverture (sinon les flèches ne feraient rien).
/// Toute la logique de navigation est dans <see cref="SlideShowViewModel"/>.
/// </summary>
public partial class SlideShowWindow : Window
{
    /// <summary>Durée d'une transition, assez courte pour ne pas ralentir l'exposé.</summary>
    private static readonly Duration TransitionDuration = new(TimeSpan.FromMilliseconds(380));

    private readonly SlideShowViewModel _viewModel;

    /// <summary>
    /// Indice de la diapositive affichée avant le changement. Il donne le
    /// SENS du glissement : une diapositive suivante arrive par la droite,
    /// une diapositive précédente par la gauche.
    /// </summary>
    private int _lastIndex;

    public SlideShowWindow(SlideShowViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = viewModel;

        _lastIndex = viewModel.CurrentIndex;

        // La transition est un effet visuel : elle est jouée ici, dans la
        // fenêtre, pendant que le ViewModel se contente de dire QUELLE
        // diapositive est affichée et QUELLE transition elle porte.
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;

        // Le curseur devient une flèche simple : rien à cliquer ici.
        Cursor = Cursors.Arrow;
    }

    // ------------------------------------------------------------------
    // Transitions (onglet Transitions)
    // ------------------------------------------------------------------

    /// <summary>
    /// Joue la transition à chaque changement de diapositive.
    /// On attend le tour de rendu de WPF avant de lancer l'animation, sinon
    /// elle démarrerait parfois sur l'ancienne image.
    /// </summary>
    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(SlideShowViewModel.CurrentSlide))
        {
            return;
        }

        int index = _viewModel.CurrentIndex;
        bool forward = index >= _lastIndex;
        _lastIndex = index;

        Dispatcher.BeginInvoke(
            new Action(() => PlayTransition(forward)),
            DispatcherPriority.Render);
    }

    /// <summary>
    /// Anime la nouvelle diapositive selon sa transition.
    ///
    ///   • Fade : opacité de 0 à 1 ;
    ///   • Push : la diapositive glisse depuis le côté, avec un léger fondu ;
    ///   • Wipe : un rectangle de découpe s'ouvre de gauche à droite.
    ///
    /// Aucune de ces animations ne modifie la présentation : elles ne durent
    /// que quelques dixièmes de seconde, à l'écran seulement.
    /// </summary>
    private void PlayTransition(bool forward)
    {
        // Remise à zéro : une animation précédente ne doit pas laisser la
        // diapositive décalée ou invisible.
        SlideHost.BeginAnimation(OpacityProperty, null);
        SlideTranslate.BeginAnimation(TranslateTransform.XProperty, null);
        SlideHost.Clip = null;
        SlideHost.Opacity = 1;
        SlideTranslate.X = 0;

        TransitionKind kind = _viewModel.CurrentTransition;

        if (kind == TransitionKind.None)
        {
            return;
        }

        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

        switch (kind)
        {
            case TransitionKind.Fade:
                SlideHost.Opacity = 0;
                SlideHost.BeginAnimation(
                    OpacityProperty,
                    new DoubleAnimation(0, 1, TransitionDuration));
                break;

            case TransitionKind.Push:
            {
                double distance = Math.Max(240, ActualWidth * 0.4);
                double from = forward ? distance : -distance;

                SlideTranslate.X = from;
                SlideTranslate.BeginAnimation(
                    TranslateTransform.XProperty,
                    new DoubleAnimation(from, 0, TransitionDuration) { EasingFunction = ease });

                SlideHost.BeginAnimation(
                    OpacityProperty,
                    new DoubleAnimation(0.35, 1, TransitionDuration));
                break;
            }

            case TransitionKind.Wipe:
            {
                double width = Math.Max(1, SlideHost.ActualWidth);
                double height = Math.Max(1, SlideHost.ActualHeight);

                var clip = new RectangleGeometry(new Rect(0, 0, 0, height));
                SlideHost.Clip = clip;

                var reveal = new RectAnimation(
                    new Rect(0, 0, 0, height),
                    new Rect(0, 0, width, height),
                    TransitionDuration)
                {
                    EasingFunction = ease
                };

                // Le rectangle doit disparaître à la fin : laissé en place,
                // il couperait aussi la diapositive suivante.
                reveal.Completed += (_, _) => SlideHost.Clip = null;
                clip.BeginAnimation(RectangleGeometry.RectProperty, reveal);
                break;
            }
        }
    }

    /// <summary>
    /// À l'ouverture, on s'assure que la fenêtre reçoit bien le clavier.
    /// Sans cela, la première flèche peut être ignorée.
    /// </summary>
    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        Activate();
        Focus();
    }

    /// <summary>
    /// Raccourcis du diaporama :
    ///   → ↓ Espace Page suivante Entrée : diapositive suivante
    ///   ← ↑ Page précédente Retour arrière : diapositive précédente
    ///   Début / Fin : première / dernière diapositive
    ///   N : afficher ou masquer les notes
    ///   V : couper ou remettre la voix qui lit la diapositive
    ///   T : choisir la langue des sous-titres (deux, une seule, aucune)
    ///   1 à 4 : enregistrer la réponse de la salle (mode jeu)
    ///   L : afficher ou masquer le classement
    ///   Échap : quitter le diaporama
    /// </summary>
    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Right:
            case Key.Down:
            case Key.Space:
            case Key.PageDown:
            case Key.Enter:
                _viewModel.Next();
                e.Handled = true;
                break;

            case Key.Left:
            case Key.Up:
            case Key.PageUp:
            case Key.Back:
                _viewModel.Previous();
                e.Handled = true;
                break;

            case Key.Home:
                _viewModel.GoToFirst();
                e.Handled = true;
                break;

            case Key.End:
                _viewModel.GoToLast();
                e.Handled = true;
                break;

            case Key.N:
                _viewModel.ToggleNotes();
                e.Handled = true;
                break;

            case Key.V:
                _viewModel.ToggleNarration();
                e.Handled = true;
                break;

            case Key.T:
                _viewModel.CycleSubtitleMode();
                e.Handled = true;
                break;

            case Key.D1:
            case Key.NumPad1:
                _viewModel.Answer(0);
                e.Handled = true;
                break;

            case Key.D2:
            case Key.NumPad2:
                _viewModel.Answer(1);
                e.Handled = true;
                break;

            case Key.D3:
            case Key.NumPad3:
                _viewModel.Answer(2);
                e.Handled = true;
                break;

            case Key.D4:
            case Key.NumPad4:
                _viewModel.Answer(3);
                e.Handled = true;
                break;

            case Key.L:
                _viewModel.ToggleLeaderboard();
                e.Handled = true;
                break;

            case Key.Escape:
                Close();
                e.Handled = true;
                break;
        }
    }

    /// <summary>
    /// À la fermeture (Échap, Alt+F4...), la voix doit se taire tout de suite :
    /// sinon elle continuerait à parler derrière l'éditeur.
    /// </summary>
    private void Window_Closed(object? sender, EventArgs e)
    {
        // La fenêtre se ferme : on ne laisse pas le ViewModel garder une
        // référence vers elle.
        _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        _viewModel.StopNarration();
    }

    // ------------------------------------------------------------------
    // Vidéo : lecture avec le son pendant la projection
    // ------------------------------------------------------------------

    /// <summary>
    /// Lance la vidéo quand sa diapositive arrive à l'écran. Le son est
    /// autorisé ici : c'est la projection, pas l'éditeur.
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
            // Format de vidéo non pris en charge : la diapositive reste
            // affichée, simplement sans image animée.
        }
    }

    /// <summary>Coupe le son dès qu'on quitte la diapositive.</summary>
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
            // Fermer un lecteur déjà libéré n'est pas une erreur.
        }
    }

    /// <summary>Relance la vidéo à la fin, pour qu'elle tourne en boucle.</summary>
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
}
