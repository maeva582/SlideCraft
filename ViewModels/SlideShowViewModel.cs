using System;
using System.Collections.Generic;
using System.Linq;
using SlideCraft.Commands;
using SlideCraft.Models;
using SlideCraft.Services;

namespace SlideCraft.ViewModels;

/// <summary>
/// Le « cerveau » du diaporama plein écran.
///
/// Il ne connaît QUE les diapositives et un numéro de diapositive courante :
/// aucune référence à WPF, aucun bouton, aucune couleur. C'est la fenêtre
/// <see cref="Views.SlideShowWindow"/> qui dessine ce que ce ViewModel décrit.
/// On peut donc le tester sans Windows.
///
/// Le diaporama ne modifie jamais la présentation : il ne fait que lire.
/// </summary>
public class SlideShowViewModel : ObservableObject
{
    /// <summary>Les diapositives projetées, dans l'ordre.</summary>
    private readonly IReadOnlyList<Slide> _slides;

    /// <summary>Indice (0 = première) de la diapositive affichée.</summary>
    private int _currentIndex;

    /// <summary>Vrai quand le panneau des notes est affiché.</summary>
    private bool _isNotesVisible = true;

    private string _statusMessage = string.Empty;

    /// <summary>Le haut-parleur (voix Windows), null si la narration est impossible.</summary>
    private readonly INarrationService? _narration;

    /// <summary>Vrai quand la voix est autorisée à lire la diapositive courante.</summary>
    private bool _isNarrationEnabled;

    private string _narrationStatus = string.Empty;

    /// <summary>
    /// La partie de quiz en cours, null en projection normale. Une seule
    /// session pour tout le diaporama : les scores suivent les diapositives.
    /// </summary>
    private readonly GameSession? _game;

    /// <summary>
    /// Prépare la projection.
    /// <paramref name="startIndex"/> est l'indice de la diapositive de départ
    /// (celle qui est sélectionnée dans l'éditeur) ; il est ramené dans les
    /// limites si nécessaire, pour ne jamais planter.
    ///
    /// <paramref name="narration"/> est facultatif : sans lui, le diaporama
    /// fonctionne exactement comme avant, simplement sans voix.
    /// </summary>
    public SlideShowViewModel(
        IReadOnlyList<Slide> slides,
        int startIndex,
        INarrationService? narration = null,
        GameSession? game = null)
    {
        _slides = slides ?? throw new ArgumentNullException(nameof(slides));

        _narration = narration;
        _game = game;
        _isNarrationEnabled = narration is { IsAvailable: true };

        if (_slides.Count == 0)
        {
            // Un diaporama sans diapositive n'a aucun sens : mieux vaut une
            // erreur claire tout de suite qu'une fenêtre noire et muette.
            throw new ArgumentException(
                "Cannot start a slide show with no slide.",
                nameof(slides));
        }

        _currentIndex = Math.Clamp(startIndex, 0, _slides.Count - 1);

        // Les commandes servent aux boutons « Précédent » / « Suivant » de la
        // barre des notes. Les flèches du clavier, elles, appellent
        // directement Next() / Previous().
        NextCommand = new RelayCommand(_ => Next(), _ => CanGoNext);
        PreviousCommand = new RelayCommand(_ => Previous(), _ => CanGoPrevious);
        ToggleNotesCommand = new RelayCommand(_ => ToggleNotes());
        ToggleNarrationCommand = new RelayCommand(_ => ToggleNarration());
        ReplayNarrationCommand = new RelayCommand(_ => ReplayNarration());

        // ---- Mode jeu (quiz) ----
        AnswerCommand = new RelayCommand(OnAnswerCommand);
        AwardCommand = new RelayCommand(parameter => Award(parameter as GamePlayer));
        ToggleLeaderboardCommand = new RelayCommand(_ => ToggleLeaderboard());

        if (narration is null || !narration.IsAvailable)
        {
            _narrationStatus = "No voice available: the slide show is silent.";
        }

        // La question de la première diapositive (si elle en a une) doit être
        // prête avant l'affichage.
        LoadQuestionForCurrentSlide();

        // La voix démarre dès la première diapositive : c'est tout l'intérêt
        // du mode « le professeur explique à voix haute ».
        SpeakCurrentSlide();
    }

    /// <summary>Diapositive actuellement projetée.</summary>
    public Slide CurrentSlide => _slides[_currentIndex];

    /// <summary>Indice de la diapositive projetée (0 = la première).</summary>
    public int CurrentIndex => _currentIndex;

    /// <summary>Numéro affiché à l'utilisateur (1 = la première).</summary>
    public int CurrentNumber => _currentIndex + 1;

    /// <summary>Nombre total de diapositives.</summary>
    public int TotalSlides => _slides.Count;

    /// <summary>
    /// Transition d'entrée de la diapositive affichée (onglet Transitions).
    /// La fenêtre la lit pour jouer l'effet ; le ViewModel ne dessine rien.
    /// </summary>
    public TransitionKind CurrentTransition => CurrentSlide.Transition;

    /// <summary>Texte « 3 / 12 » affiché discrètement pendant la projection.</summary>
    public string ProgressLabel => $"{CurrentNumber} / {TotalSlides}";

    /// <summary>Vrai s'il reste une diapositive après celle-ci.</summary>
    public bool CanGoNext => _currentIndex < _slides.Count - 1;

    /// <summary>Vrai s'il y a une diapositive avant celle-ci.</summary>
    public bool CanGoPrevious => _currentIndex > 0;

    /// <summary>Vrai quand le panneau des notes est visible (touche N).</summary>
    public bool IsNotesVisible
    {
        get => _isNotesVisible;
        set => SetProperty(ref _isNotesVisible, value);
    }

    /// <summary>
    /// Notes privées de la diapositive courante. Un texte de remplacement est
    /// renvoyé quand l'utilisateur n'a rien écrit : la zone n'est jamais vide
    /// sans explication.
    /// </summary>
    public string NotesText => string.IsNullOrWhiteSpace(CurrentSlide.Notes.PersonalNotes)
        ? "No notes for this slide."
        : CurrentSlide.Notes.PersonalNotes;

    /// <summary>
    /// Texte exactement tel que la voix le lira : le script de narration s'il
    /// existe, sinon le titre et les textes de la diapositive. L'afficher
    /// permet de vérifier ce que le public va entendre.
    /// </summary>
    public string NarrationText => NarrationTextBuilder.Build(CurrentSlide);

    /// <summary>Vrai si une voix est installée et utilisable.</summary>
    public bool IsNarrationAvailable => _narration is { IsAvailable: true };

    /// <summary>La partie de quiz, null hors mode jeu (utilisé par le XAML).</summary>
    public GameSession? Game => _game;

    /// <summary>Vrai si la diapositive courante porte une question jouable.</summary>
    public bool HasQuestion => _game is { HasQuestion: true };

    // ---- Sous-titres bilingues (étape 9) ----

    /// <summary>
    /// Mode d'affichage des sous-titres, changé par la touche T.
    /// Il est conservé d'une diapositive à l'autre : le présentateur choisit
    /// une fois pour toutes la langue que lit son public.
    /// </summary>
    private SubtitleMode _subtitleMode = SubtitleMode.Both;

    /// <summary>Message d'aide affiché quand T ne peut rien changer.</summary>
    private string _subtitleStatus = string.Empty;

    /// <summary>Vrai si la diapositive courante a des sous-titres à montrer.</summary>
    public bool HasSubtitles => CurrentSlide.Subtitles is { HasContent: true };

    /// <summary>Étiquette de la première langue (« English »).</summary>
    public string SubtitlePrimaryLanguage => CurrentSlide.Subtitles?.PrimaryLanguage ?? string.Empty;

    /// <summary>Étiquette de la seconde langue (« French »).</summary>
    public string SubtitleSecondaryLanguage => CurrentSlide.Subtitles?.SecondaryLanguage ?? string.Empty;

    /// <summary>Texte de la première langue.</summary>
    public string SubtitlePrimaryText => CurrentSlide.Subtitles?.PrimaryText ?? string.Empty;

    /// <summary>Texte de la seconde langue.</summary>
    public string SubtitleSecondaryText => CurrentSlide.Subtitles?.SecondaryText ?? string.Empty;

    /// <summary>Vrai si la première ligne doit être dessinée à l'écran.</summary>
    public bool IsSubtitlePrimaryVisible => HasSubtitles
        && (_subtitleMode is SubtitleMode.Both or SubtitleMode.PrimaryOnly)
        && CurrentSlide.Subtitles!.HasPrimary;

    /// <summary>Vrai si la seconde ligne doit être dessinée à l'écran.</summary>
    public bool IsSubtitleSecondaryVisible => HasSubtitles
        && (_subtitleMode is SubtitleMode.Both or SubtitleMode.SecondaryOnly)
        && CurrentSlide.Subtitles!.HasSecondary;

    /// <summary>Vrai si au moins une ligne de sous-titre est visible.</summary>
    public bool IsSubtitleVisible => IsSubtitlePrimaryVisible || IsSubtitleSecondaryVisible;

    /// <summary>Mode courant (les tests s'en servent pour vérifier la touche T).</summary>
    public SubtitleMode SubtitleMode => _subtitleMode;

    /// <summary>Petit rappel affiché dans les notes : « Subtitles: English + French ».</summary>
    public string SubtitleModeLabel
    {
        get
        {
            if (!HasSubtitles)
            {
                return "Subtitles: none on this slide (T)";
            }

            string primary = SubtitlePrimaryLanguage;
            string secondary = SubtitleSecondaryLanguage;

            return _subtitleMode switch
            {
                SubtitleMode.Both => $"Subtitles: {primary} + {secondary} (T)",
                SubtitleMode.PrimaryOnly => $"Subtitles: {primary} only (T)",
                SubtitleMode.SecondaryOnly => $"Subtitles: {secondary} only (T)",
                _ => "Subtitles: off (T)"
            };
        }
    }

    /// <summary>Explication affichée quand T appuie sur une diapositive sans sous-titre.</summary>
    public string SubtitleStatus
    {
        get => _subtitleStatus;
        private set => SetProperty(ref _subtitleStatus, value);
    }

    /// <summary>
    /// Vrai quand la voix lit la diapositive (touche V pour couper / remettre).
    /// </summary>
    public bool IsNarrationEnabled
    {
        get => _isNarrationEnabled;
        set
        {
            if (_isNarrationEnabled == value)
            {
                return;
            }

            _isNarrationEnabled = value;
            OnPropertyChanged();

            if (value)
            {
                SpeakCurrentSlide();
            }
            else
            {
                _narration?.Stop();
                NarrationStatus = IsNarrationAvailable
                    ? "Voice off — press V to read the slide again."
                    : NarrationStatus;
            }
        }
    }

    /// <summary>Nom de la voix, ou explication quand il n'y en a pas.</summary>
    public string NarrationLabel => _narration is { IsAvailable: true } narr
        ? $"Voice: {narr.VoiceName}"
        : "Voice: unavailable";

    /// <summary>Message d'état de la narration, affiché sous les notes.</summary>
    public string NarrationStatus
    {
        get => _narrationStatus;
        private set => SetProperty(ref _narrationStatus, value);
    }

    /// <summary>
    /// Petit message d'aide (fin de présentation, première diapositive...).
    /// Il est affiché en haut de l'écran, donc visible même quand les notes
    /// sont masquées.
    /// </summary>
    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public RelayCommand NextCommand { get; }
    public RelayCommand PreviousCommand { get; }
    public RelayCommand ToggleNotesCommand { get; }
    public RelayCommand ToggleNarrationCommand { get; }
    public RelayCommand ReplayNarrationCommand { get; }
    public RelayCommand AnswerCommand { get; }
    public RelayCommand AwardCommand { get; }
    public RelayCommand ToggleLeaderboardCommand { get; }

    /// <summary>
    /// Passe à la diapositive suivante.
    /// Renvoie false si on était déjà à la fin (la touche ne fait rien,
    /// mais l'utilisateur reçoit une explication).
    /// </summary>
    public bool Next()
    {
        if (!CanGoNext)
        {
            StatusMessage = "End of the presentation — press Esc to return to the editor.";
            return false;
        }

        GoTo(_currentIndex + 1);
        return true;
    }

    /// <summary>Revient à la diapositive précédente (false si on est au début).</summary>
    public bool Previous()
    {
        if (!CanGoPrevious)
        {
            StatusMessage = "You are on the first slide.";
            return false;
        }

        GoTo(_currentIndex - 1);
        return true;
    }

    /// <summary>Va directement à une diapositive (touches Début / Fin).</summary>
    public void GoTo(int index)
    {
        int limited = Math.Clamp(index, 0, _slides.Count - 1);

        if (limited == _currentIndex)
        {
            return;
        }

        _currentIndex = limited;
        StatusMessage = string.Empty;
        NotifyCurrentSlideChanged();
    }

    public void GoToFirst() => GoTo(0);

    public void GoToLast() => GoTo(_slides.Count - 1);

    /// <summary>Affiche ou masque les notes du présentateur (touche N).</summary>
    public void ToggleNotes()
    {
        IsNotesVisible = !IsNotesVisible;
    }

    /// <summary>Coupe ou remet la voix (touche V).</summary>
    public void ToggleNarration()
    {
        IsNarrationEnabled = !IsNarrationEnabled;
    }

    /// <summary>Relit la diapositive courante depuis le début (bouton « Read again »).</summary>
    public void ReplayNarration()
    {
        if (!IsNarrationEnabled)
        {
            IsNarrationEnabled = true;
            return;
        }

        SpeakCurrentSlide();
    }

    /// <summary>
    /// Demande à la voix de lire la diapositive affichée. Ne fait rien quand
    /// la voix est coupée : c'est ce qui garantit qu'un simple appui sur la
    /// flèche droite ne relance pas la lecture après un « V ».
    /// </summary>
    public void SpeakCurrentSlide()
    {
        if (_narration is null || !_narration.IsAvailable || !_isNarrationEnabled)
        {
            return;
        }

        string text = NarrationTextBuilder.Build(CurrentSlide);

        if (string.IsNullOrWhiteSpace(text))
        {
            NarrationStatus = "This slide has nothing to read yet.";
            return;
        }

        NarrationStatus = _narration.Speak(text)
            ? "Reading this slide aloud…"
            : "The voice could not start on this slide.";
    }

    /// <summary>Coupe la voix (appelé quand on quitte le diaporama).</summary>
    public void StopNarration()
    {
        _narration?.Stop();
    }

    // ------------------------------------------------------------------
    // Mode jeu : réponses, points et classement
    // ------------------------------------------------------------------

    /// <summary>
    /// Enregistre la réponse choisie (clic sur un bouton, ou touche 1 à 4).
    /// Renvoie false si la diapositive n'a pas de quiz ou si la réponse est
    /// déjà donnée.
    /// </summary>
    public bool Answer(int index)
    {
        return _game is not null && _game.SelectAnswer(index);
    }

    /// <summary>Donne les points de la question à un joueur.</summary>
    public void Award(GamePlayer? player)
    {
        _game?.Award(player);
    }

    /// <summary>Affiche ou masque le classement (touche L).</summary>
    public void ToggleLeaderboard()
    {
        _game?.ToggleLeaderboard();
    }

    // ------------------------------------------------------------------
    // Sous-titres bilingues (étape 9)
    // ------------------------------------------------------------------

    /// <summary>
    /// Passe au mode d'affichage suivant (touche T).
    ///
    /// L'ordre est : les deux langues, puis la principale seule, puis la
    /// secondaire seule, puis rien — mais un mode qui n'afficherait aucune
    /// ligne (langue vide sur cette diapositive) est sauté, pour ne jamais
    /// laisser l'écran muet sans raison.
    /// </summary>
    public void CycleSubtitleMode()
    {
        if (CurrentSlide.Subtitles is not { } subtitles || !subtitles.HasContent)
        {
            SubtitleStatus = "This slide has no subtitles. Add them with the Subtitles button in the editor.";
            return;
        }

        var modes = new List<SubtitleMode>();

        if (subtitles.HasPrimary && subtitles.HasSecondary)
        {
            modes.Add(SubtitleMode.Both);
        }

        if (subtitles.HasPrimary)
        {
            modes.Add(SubtitleMode.PrimaryOnly);
        }

        if (subtitles.HasSecondary)
        {
            modes.Add(SubtitleMode.SecondaryOnly);
        }

        modes.Add(SubtitleMode.Hidden);

        // IndexOf renvoie -1 si le mode courant n'est pas proposé ici :
        // on repart alors du premier mode disponible.
        int index = modes.IndexOf(_subtitleMode);
        _subtitleMode = modes[(index + 1) % modes.Count];
        SubtitleStatus = string.Empty;

        NotifySubtitleChanged();
    }

    /// <summary>
    /// Si la langue affichée seule n'existe pas sur la nouvelle diapositive,
    /// on bascule sur l'autre langue au lieu de laisser un écran vide.
    /// </summary>
    private void AdjustSubtitleMode()
    {
        if (CurrentSlide.Subtitles is not { } subtitles)
        {
            return;
        }

        if (_subtitleMode == SubtitleMode.PrimaryOnly && !subtitles.HasPrimary && subtitles.HasSecondary)
        {
            _subtitleMode = SubtitleMode.SecondaryOnly;
        }
        else if (_subtitleMode == SubtitleMode.SecondaryOnly && !subtitles.HasSecondary && subtitles.HasPrimary)
        {
            _subtitleMode = SubtitleMode.PrimaryOnly;
        }
    }

    /// <summary>Prévient l'interface que les sous-titres affichés ont changé.</summary>
    private void NotifySubtitleChanged()
    {
        OnPropertyChanged(nameof(HasSubtitles));
        OnPropertyChanged(nameof(SubtitlePrimaryLanguage));
        OnPropertyChanged(nameof(SubtitleSecondaryLanguage));
        OnPropertyChanged(nameof(SubtitlePrimaryText));
        OnPropertyChanged(nameof(SubtitleSecondaryText));
        OnPropertyChanged(nameof(IsSubtitlePrimaryVisible));
        OnPropertyChanged(nameof(IsSubtitleSecondaryVisible));
        OnPropertyChanged(nameof(IsSubtitleVisible));
        OnPropertyChanged(nameof(SubtitleMode));
        OnPropertyChanged(nameof(SubtitleModeLabel));
    }

    /// <summary>
    /// Charge la question de la diapositive affichée. Le numéro de question
    /// est calculé sur toute la présentation : il ne change pas si l'on
    /// revient en arrière, ce qui évite un numéro qui s'incrémente à l'infini.
    /// </summary>
    private void LoadQuestionForCurrentSlide()
    {
        if (_game is null)
        {
            return;
        }

        QuizQuestion? question = CurrentSlide.Quiz?.Questions.FirstOrDefault();

        int number = 0;
        int total = 0;

        for (int i = 0; i < _slides.Count; i++)
        {
            bool hasQuiz = _slides[i].Quiz is { Questions: { Count: > 0 } };

            if (hasQuiz)
            {
                total++;

                if (i <= _currentIndex)
                {
                    number++;
                }
            }
        }

        _game.LoadQuestion(question, number, total);
        OnPropertyChanged(nameof(HasQuestion));
    }

    /// <summary>
    /// Paramètre de commande d'une réponse : le XAML envoie l'index (int),
    /// le clavier envoie la même valeur convertie en texte.
    /// </summary>
    private void OnAnswerCommand(object? parameter)
    {
        int index = parameter switch
        {
            int value => value,
            string text when int.TryParse(text, out int parsed) => parsed,
            _ => -1
        };

        if (index >= 0)
        {
            Answer(index);
        }
    }

    /// <summary>
    /// Prévient l'interface que la diapositive affichée a changé : tout ce qui
    /// en dépend (contenu, numéro, notes, boutons) doit être redessiné.
    /// </summary>
    private void NotifyCurrentSlideChanged()
    {
        OnPropertyChanged(nameof(CurrentSlide));
        OnPropertyChanged(nameof(CurrentIndex));
        OnPropertyChanged(nameof(CurrentNumber));
        OnPropertyChanged(nameof(ProgressLabel));
        OnPropertyChanged(nameof(CanGoNext));
        OnPropertyChanged(nameof(CanGoPrevious));
        OnPropertyChanged(nameof(NotesText));
        OnPropertyChanged(nameof(NarrationText));
        OnPropertyChanged(nameof(CurrentTransition));

        // Les sous-titres de la nouvelle diapositive remplacent les précédents.
        AdjustSubtitleMode();
        NotifySubtitleChanged();

        // La question de la nouvelle diapositive remplace celle d'avant.
        LoadQuestionForCurrentSlide();

        // La nouvelle diapositive est lue à voix haute, si la voix est active.
        SpeakCurrentSlide();

        // Les boutons Précédent / Suivant doivent s'activer ou se désactiver
        // immédiatement, sans attendre un mouvement de souris.
        NextCommand.RaiseCanExecuteChanged();
        PreviousCommand.RaiseCanExecuteChanged();
    }
}
