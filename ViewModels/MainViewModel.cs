using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Input;
using SlideCraft.Commands;
using SlideCraft.Models;
using SlideCraft.Services;

namespace SlideCraft.ViewModels;

/// <summary>
/// ViewModel principal : c'est le "cerveau" de la fenêtre.
/// Il contient les données affichées (présentation, diapositive sélectionnée,
/// zoom, message de statut) et les commandes des boutons.
///
/// La fenêtre (MainWindow) ne fait que l'afficher et transmettre les clics.
/// </summary>
public class MainViewModel : ObservableObject
{
    // Taille du repère d'une diapositive (voir SlideElement).
    // Publiques pour que la fenêtre (glisser, redimensionner) utilise
    // exactement les mêmes valeurs.
    public const double SlideWidth = 960;
    public const double SlideHeight = 540;

    private readonly Presentation _presentation;
    private readonly UndoRedoService _undoRedo = new();
    private readonly IFileDialogService? _fileDialogService;
    private readonly ISlideShowService? _slideShowService;
    private readonly ISmartSlideService? _smartSlideService;
    private readonly IQuizEditorService? _quizEditorService;
    private readonly IGameSetupService? _gameSetupService;
    private readonly ISubtitleEditorService? _subtitleEditorService;
    private readonly IReviewService? _reviewService;

    /// <summary>Joueurs du mode jeu, saisis sur ce PC (aucun réseau).</summary>
    private readonly List<string> _gamePlayers = new();

    private Slide? _selectedSlide;
    private SlideElement? _selectedElement;

    /// <summary>Élément mémorisé par « Copier » (le presse-papiers interne).</summary>
    private SlideElement? _copiedElement;

    /// <summary>
    /// Photo prise au début d'une modification continue : glisser (ou
    /// redimensionner) un élément, ou saisir du texte directement sur la
    /// diapositive. Elle devient l'état « avant » de l'unique action
    /// d'annulation créée à la fin du geste.
    /// </summary>
    private PresentationSnapshot? _pendingSnapshot;

    /// <summary>Bloc de texte en cours d'édition directe sur la diapositive.</summary>
    private TextElement? _editingText;

    /// <summary>Cellule de tableau en cours d'édition directe.</summary>
    private TableCell? _editingCell;

    /// <summary>Texte d'origine pendant l'édition, pour savoir s'il a changé.</summary>
    private string _inlineOriginalText = string.Empty;

    /// <summary>
    /// Action qui était en haut de la pile d'annulation lors du dernier
    /// enregistrement. Elle sert à savoir si le document est modifié :
    /// si l'action courante est la même référence, rien n'a changé depuis.
    /// </summary>
    private EditEntry? _savedEntry;

    private double _dragStartX;
    private double _dragStartY;
    private double _dragStartWidth;
    private double _dragStartHeight;

    private double _zoom = 100;
    private string _statusMessage = "Ready";
    private bool _isLayoutMenuOpen;

    // ---- Étape 12 : ce que l'onglet View affiche ou masque ----
    private bool _showThumbnails = true;
    private bool _showPropertiesPanel = true;
    private bool _showNotesPanel = true;

    /// <summary>True while the File page (backstage) covers the editor.</summary>
    private bool _isBackstageOpen;

    /// <summary>Constructeur utilisé par WPF : charge la présentation de démonstration.</summary>
    public MainViewModel() : this(PresentationFactory.CreateSamplePresentation())
    {
    }

    /// <summary>
    /// Constructeur qui accepte n'importe quelle présentation (utile pour les tests).
    /// Sans sélecteur de fichier, le bouton « Image » ne peut pas ouvrir de dialogue.
    /// </summary>
    public MainViewModel(Presentation presentation) : this(presentation, null)
    {
    }

    /// <summary>
    /// Constructeur complet. <paramref name="fileDialogService"/> est fourni par
    /// la fenêtre (boîte de dialogue Windows) ou par un test (doublure).
    /// </summary>
    public MainViewModel(
        Presentation presentation,
        IFileDialogService? fileDialogService,
        ISlideShowService? slideShowService = null,
        ISmartSlideService? smartSlideService = null,
        IQuizEditorService? quizEditorService = null,
        IGameSetupService? gameSetupService = null,
        ISubtitleEditorService? subtitleEditorService = null,
        IReviewService? reviewService = null)
    {
        _presentation = presentation;
        _fileDialogService = fileDialogService;
        _slideShowService = slideShowService;
        _smartSlideService = smartSlideService;
        _quizEditorService = quizEditorService;
        _gameSetupService = gameSetupService;
        _subtitleEditorService = subtitleEditorService;
        _reviewService = reviewService;

        SetBackgroundCommand = new RelayCommand(
            SetBackground,
            _ => SelectedSlide is not null);

        ZoomInCommand = new RelayCommand(_ => Zoom += 10);
        ZoomOutCommand = new RelayCommand(_ => Zoom -= 10);
        ResetZoomCommand = new RelayCommand(_ => Zoom = 100);

        // ---- Étape 2 : gestion des diapositives ----
        AddSlideCommand = new RelayCommand(_ => AddSlide());
        DeleteSlideCommand = new RelayCommand(_ => DeleteSlide(), CanDeleteSlide);
        DuplicateSlideCommand = new RelayCommand(_ => DuplicateSlide(), CanDuplicateSlide);
        MoveSlideUpCommand = new RelayCommand(_ => MoveSelectedSlide(-1), CanMoveSlideUp);
        MoveSlideDownCommand = new RelayCommand(_ => MoveSelectedSlide(1), CanMoveSlideDown);
        ApplyLayoutCommand = new RelayCommand(ApplyLayout);

        // ---- Étape 3 : contenu des diapositives ----
        InsertTextCommand = new RelayCommand(_ => InsertText(), CanEditSlide);
        InsertImageCommand = new RelayCommand(_ => InsertImage(), CanEditSlide);
        InsertVideoCommand = new RelayCommand(_ => InsertVideo(), CanEditSlide);
        InsertShapeCommand = new RelayCommand(_ => InsertShape(), CanEditSlide);
        InsertTableCommand = new RelayCommand(_ => InsertTable(), CanEditSlide);

        DeleteElementCommand = new RelayCommand(_ => DeleteElement(), _ => SelectedElement is not null);
        CopyElementCommand = new RelayCommand(_ => CopyElement(), _ => SelectedElement is not null);
        PasteElementCommand = new RelayCommand(
            _ => PasteElement(),
            _ => _copiedElement is not null && SelectedSlide is not null);

        SetForegroundCommand = new RelayCommand(SetForeground, _ => SelectedElement is TextElement);
        SetFillCommand = new RelayCommand(SetFill, _ => SelectedElement is ShapeElement);

        UndoCommand = new RelayCommand(_ => Undo(), _ => _undoRedo.CanUndo);
        RedoCommand = new RelayCommand(_ => Redo(), _ => _undoRedo.CanRedo);

        // ---- Étape 4 : fichier ----
        NewPresentationCommand = new RelayCommand(_ => NewPresentation());
        OpenPresentationCommand = new RelayCommand(_ => OpenPresentation());

        // Étape 11 : importer un fichier PowerPoint existant (.pptx).
        ImportPowerPointCommand = new RelayCommand(_ => ImportPowerPoint());
        SavePresentationCommand = new RelayCommand(_ => SavePresentation());
        SaveAsPresentationCommand = new RelayCommand(_ => SavePresentationAs());
        ExportPowerPointCommand = new RelayCommand(_ => ExportPowerPoint());

        // La page « File » du ruban (backstage), comme dans PowerPoint.
        OpenBackstageCommand = new RelayCommand(_ => IsBackstageOpen = true);
        CloseBackstageCommand = new RelayCommand(_ => IsBackstageOpen = false);

        // ---- Étape 5 : diaporama plein écran ----
        StartSlideShowCommand = new RelayCommand(_ => StartSlideShow(), CanStartSlideShow);

        // ---- Étape 6 : SmartSlide (analyse de la qualité des diapositives) ----
        AnalyzeSlidesCommand = new RelayCommand(_ => AnalyzeSlides(), CanAnalyzeSlides);

        // ---- Étape 8 : quiz et mode jeu ----
        EditQuizCommand = new RelayCommand(_ => EditQuiz(), CanEditSlide);
        ConfigureGameCommand = new RelayCommand(_ => ConfigureGame());

        // ---- Étape 9 : sous-titres bilingues ----
        EditSubtitlesCommand = new RelayCommand(_ => EditSubtitles(), CanEditSlide);

        // ---- Étape 12 : transitions, revue et affichage ----
        SetTransitionCommand = new RelayCommand(SetTransition, _ => SelectedSlide is not null);
        ApplyTransitionToAllCommand = new RelayCommand(
            _ => ApplyTransitionToAllSlides(),
            _ => SelectedSlide is not null);
        ReviewCommand = new RelayCommand(_ => ShowReview(), _ => Slides.Count > 0);

        // Quand on ajoute, supprime ou déplace une diapositive, le compteur
        // du bas de fenêtre et les boutons doivent se mettre à jour.
        Slides.CollectionChanged += (sender, args) =>
        {
            OnPropertyChanged(nameof(SlideCount));
            OnPropertyChanged(nameof(CurrentSlideNumber));
            RefreshCommands();
        };

        SelectedSlide = Slides.FirstOrDefault();
    }

    /// <summary>La présentation affichée.</summary>
    public Presentation Presentation => _presentation;

    /// <summary>Raccourci vers les diapositives (utilisé par la liste de miniatures).</summary>
    public ObservableCollection<Slide> Slides => _presentation.Slides;

    /// <summary>Diapositive en cours d'édition.</summary>
    public Slide? SelectedSlide
    {
        get => _selectedSlide;
        set
        {
            if (ReferenceEquals(_selectedSlide, value))
            {
                return;
            }

            // On efface la sélection sur l'ANCIENNE diapositive avant de changer,
            // sinon le cadre bleu resterait dessiné dessus.
            ClearSelectionOn(_selectedSlide);
            _selectedSlide = value;
            SelectedElement = null;

            OnPropertyChanged();
            OnPropertyChanged(nameof(CurrentSlideNumber));
            OnPropertyChanged(nameof(HasSelectedSlide));
            OnPropertyChanged(nameof(CurrentSlideTitle));
            OnPropertyChanged(nameof(HasQuiz));
            OnPropertyChanged(nameof(QuizSummary));
            OnPropertyChanged(nameof(HasSubtitles));
            OnPropertyChanged(nameof(SubtitleSummary));
            OnPropertyChanged(nameof(CurrentTransition));
            OnPropertyChanged(nameof(CurrentTransitionLabel));
            SetStatus($"Slide {CurrentSlideNumber} selected");
            RefreshCommands();
        }
    }

    /// <summary>Vrai si une diapositive est sélectionnée (active les boutons).</summary>
    public bool HasSelectedSlide => SelectedSlide is not null;

    /// <summary>Titre de la diapositive courante (utilisé par le panneau de propriétés).</summary>
    public string CurrentSlideTitle
    {
        get => SelectedSlide?.Title ?? string.Empty;
        set
        {
            if (SelectedSlide is not { } slide || slide.Title == value)
            {
                return;
            }

            PerformEdit("Slide title changed", () => slide.Title = value);
        }
    }

    /// <summary>Élément sélectionné dans la zone de travail (null si aucun).</summary>
    public SlideElement? SelectedElement
    {
        get => _selectedElement;
        private set
        {
            if (SetProperty(ref _selectedElement, value))
            {
                NotifySelectedElementChanged();
            }
        }
    }

    /// <summary>Vrai si un élément est sélectionné (utilisé pour afficher les propriétés).</summary>
    public bool HasSelectedElement => SelectedElement is not null;

    /// <summary>Vrai si l'élément sélectionné est un bloc de texte.</summary>
    public bool IsTextElementSelected => SelectedElement is TextElement;

    /// <summary>Vrai si l'élément sélectionné est une forme.</summary>
    public bool IsShapeElementSelected => SelectedElement is ShapeElement;

    // ------------------------------------------------------------------
    // Propriétés éditables de l'élément sélectionné
    // Chaque modification passe par PerformEdit : elle devient annulable.
    // ------------------------------------------------------------------

    /// <summary>Contenu du bloc de texte sélectionné.</summary>
    public string SelectedText
    {
        get => SelectedElement is TextElement text ? text.Text : string.Empty;
        set
        {
            if (SelectedElement is not TextElement text || text.Text == value)
            {
                return;
            }

            PerformEdit("Text changed", () => text.Text = value);
        }
    }

    /// <summary>Taille de police du texte sélectionné (8 à 200).</summary>
    public double SelectedFontSize
    {
        get => SelectedElement is TextElement text ? text.FontSize : 0;
        set
        {
            if (SelectedElement is not TextElement text)
            {
                return;
            }

            double size = Math.Clamp(value, 8, 200);
            if (Math.Abs(text.FontSize - size) < 0.01)
            {
                return;
            }

            PerformEdit("Font size changed", () => text.FontSize = size);
        }
    }

    /// <summary>Couleur du texte sélectionné (« #RRGGBB »).</summary>
    public string SelectedForeground => SelectedElement is TextElement text ? text.Foreground : "#000000";

    /// <summary>Couleur de remplissage de la forme sélectionnée.</summary>
    public string SelectedFill => SelectedElement is ShapeElement shape ? shape.Fill : "#000000";

    /// <summary>Position horizontale de l'élément sélectionné.</summary>
    public double ElementX
    {
        get => SelectedElement?.X ?? 0;
        set => SetGeometry(SelectedElement, element => element.X = Math.Clamp(value, 0, Math.Max(0, SlideWidth - element.Width)), "X position changed");
    }

    /// <summary>Position verticale de l'élément sélectionné.</summary>
    public double ElementY
    {
        get => SelectedElement?.Y ?? 0;
        set => SetGeometry(SelectedElement, element => element.Y = Math.Clamp(value, 0, Math.Max(0, SlideHeight - element.Height)), "Y position changed");
    }

    /// <summary>Largeur de l'élément sélectionné.</summary>
    public double ElementWidth
    {
        get => SelectedElement?.Width ?? 0;
        set => SetGeometry(SelectedElement, element => element.Width = Math.Clamp(value, 40, Math.Max(40, SlideWidth - element.X)), "Width changed");
    }

    /// <summary>Hauteur de l'élément sélectionné.</summary>
    public double ElementHeight
    {
        get => SelectedElement?.Height ?? 0;
        set => SetGeometry(SelectedElement, element => element.Height = Math.Clamp(value, 30, Math.Max(30, SlideHeight - element.Y)), "Height changed");
    }

    /// <summary>
    /// Vrai quand le menu des dispositions est ouvert.
    /// Le bouton « Disposition » et le Popup du XAML sont liés à cette valeur.
    /// </summary>
    public bool IsLayoutMenuOpen
    {
        get => _isLayoutMenuOpen;
        set => SetProperty(ref _isLayoutMenuOpen, value);
    }

    /// <summary>Les dispositions proposées dans le menu de l'onglet Conception.</summary>
    public IReadOnlyList<SlideLayout> Layouts => SlideLayout.Catalog;

    /// <summary>Niveau de zoom en pourcentage (25 à 200).</summary>
    public double Zoom
    {
        get => _zoom;
        set
        {
            double limited = Math.Clamp(value, 25, 200);
            if (SetProperty(ref _zoom, limited))
            {
                OnPropertyChanged(nameof(ZoomFactor));
                OnPropertyChanged(nameof(ZoomLabel));
            }
        }
    }

    /// <summary>Facteur utilisé par le ScaleTransform du XAML (1.0 = 100 %).</summary>
    public double ZoomFactor => Zoom / 100.0;

    /// <summary>Texte affiché à côté du curseur de zoom.</summary>
    public string ZoomLabel => $"{Zoom:0} %";

    // ---- Étape 12 : onglet Transitions ----

    /// <summary>Transition de la diapositive affichée (None si aucune).</summary>
    public TransitionKind CurrentTransition => SelectedSlide?.Transition ?? TransitionKind.None;

    /// <summary>Texte « Transition: Fade » affiché sous les boutons du ruban.</summary>
    public string CurrentTransitionLabel
        => $"Transition: {TransitionCatalog.Label(CurrentTransition)}";

    // ---- Étape 12 : onglet View ----

    /// <summary>Vrai quand la colonne des miniatures est affichée.</summary>
    public bool ShowThumbnails
    {
        get => _showThumbnails;
        set => SetProperty(ref _showThumbnails, value);
    }

    /// <summary>Vrai quand le panneau des propriétés est affiché.</summary>
    public bool ShowPropertiesPanel
    {
        get => _showPropertiesPanel;
        set => SetProperty(ref _showPropertiesPanel, value);
    }

    /// <summary>Vrai quand la zone des notes du présentateur est affichée.</summary>
    public bool ShowNotesPanel
    {
        get => _showNotesPanel;
        set => SetProperty(ref _showNotesPanel, value);
    }

    /// <summary>Numéro de la diapositive affichée (1 pour la première).</summary>
    public int CurrentSlideNumber
    {
        get
        {
            if (SelectedSlide is null)
            {
                return 0;
            }

            return Slides.IndexOf(SelectedSlide) + 1;
        }
    }

    /// <summary>Nombre total de diapositives.</summary>
    public int SlideCount => Slides.Count;

    /// <summary>Message affiché dans la barre d'état en bas.</summary>
    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    /// <summary>
    /// Nom affiché dans la barre de titre : le nom du fichier s'il y en a un,
    /// sinon le titre de la présentation, suivi d'une étoile quand des
    /// modifications ne sont pas encore enregistrées.
    /// </summary>
    public string DocumentTitle
    {
        get
        {
            string name = string.IsNullOrWhiteSpace(Presentation.FilePath)
                ? Presentation.Title
                : Path.GetFileName(Presentation.FilePath);

            return Presentation.IsModified ? name + " *" : name;
        }
    }

    /// <summary>
    /// Vrai quand la page « File » recouvre l'éditeur. Un simple booléen suffit :
    /// la fenêtre affiche ou masque la page avec un convertisseur.
    /// </summary>
    public bool IsBackstageOpen
    {
        get => _isBackstageOpen;
        set => SetProperty(ref _isBackstageOpen, value);
    }

    /// <summary>Nom affiché sur la page File (nom du fichier ou titre).</summary>
    public string DocumentFileName => string.IsNullOrWhiteSpace(Presentation.FilePath)
        ? Presentation.Title
        : Path.GetFileName(Presentation.FilePath);

    /// <summary>Dossier du fichier enregistré, ou une phrase quand rien ne l'est.</summary>
    public string DocumentLocation => string.IsNullOrWhiteSpace(Presentation.FilePath)
        ? "Not saved on this computer yet"
        : Path.GetDirectoryName(Presentation.FilePath) ?? string.Empty;

    /// <summary>« All changes saved » ou « Unsaved changes », sur la page File.</summary>
    public string DocumentState => Presentation.IsModified
        ? "Unsaved changes"
        : "All changes saved";

    public ICommand SetBackgroundCommand { get; }
    public ICommand ZoomInCommand { get; }
    public ICommand ZoomOutCommand { get; }
    public ICommand ResetZoomCommand { get; }

    // ---- Commandes de l'étape 2 ----
    public ICommand AddSlideCommand { get; }
    public ICommand DeleteSlideCommand { get; }
    public ICommand DuplicateSlideCommand { get; }
    public ICommand MoveSlideUpCommand { get; }
    public ICommand MoveSlideDownCommand { get; }
    public ICommand ApplyLayoutCommand { get; }

    // ---- Commandes de l'étape 3 ----
    public ICommand InsertTextCommand { get; }
    public ICommand InsertImageCommand { get; }
    public ICommand InsertVideoCommand { get; }
    public ICommand ImportPowerPointCommand { get; }
    public ICommand InsertShapeCommand { get; }
    public ICommand InsertTableCommand { get; }
    public ICommand DeleteElementCommand { get; }
    public ICommand CopyElementCommand { get; }
    public ICommand PasteElementCommand { get; }
    public ICommand SetForegroundCommand { get; }
    public ICommand SetFillCommand { get; }
    public ICommand UndoCommand { get; }
    public ICommand RedoCommand { get; }

    // ---- Commandes de l'étape 4 ----
    public ICommand NewPresentationCommand { get; }
    public ICommand OpenPresentationCommand { get; }
    public ICommand SavePresentationCommand { get; }
    public ICommand SaveAsPresentationCommand { get; }
    public ICommand ExportPowerPointCommand { get; }
    public ICommand OpenBackstageCommand { get; }
    public ICommand CloseBackstageCommand { get; }

    // ---- Commande de l'étape 5 ----
    public ICommand StartSlideShowCommand { get; }

    // ---- Commande de l'étape 6 ----
    public ICommand AnalyzeSlidesCommand { get; }

    // ---- Commandes de l'étape 8 (quiz et mode jeu) ----
    public ICommand EditQuizCommand { get; }
    public ICommand ConfigureGameCommand { get; }

    // ---- Commande de l'étape 9 (sous-titres bilingues) ----
    public ICommand EditSubtitlesCommand { get; }

    // ---- Commandes de l'étape 12 ----
    public ICommand SetTransitionCommand { get; }
    public ICommand ApplyTransitionToAllCommand { get; }
    public ICommand ReviewCommand { get; }

    /// <summary>Vrai si la diapositive sélectionnée a un quiz.</summary>
    public bool HasQuiz => SelectedSlide?.Quiz is { Questions: { Count: > 0 } };

    /// <summary>Résumé lisible du quiz de la diapositive (« Quiz · 1 question · 10 points »).</summary>
    public string QuizSummary
    {
        get
        {
            if (SelectedSlide?.Quiz is not { Questions: { Count: > 0 } } quiz)
            {
                return "No quiz on this slide";
            }

            int points = quiz.Questions.Sum(question => question.Points);
            return $"Quiz: {quiz.Questions.Count} question{(quiz.Questions.Count == 1 ? string.Empty : "s")} · {points} points";
        }
    }

    /// <summary>Vrai si la diapositive sélectionnée a des sous-titres.</summary>
    public bool HasSubtitles => SelectedSlide?.Subtitles is { HasContent: true };

    /// <summary>
    /// Résumé lisible des sous-titres de la diapositive
    /// (« Subtitles: English / French »).
    /// </summary>
    public string SubtitleSummary
    {
        get
        {
            if (SelectedSlide?.Subtitles is not { HasContent: true } subtitles)
            {
                return "No subtitles on this slide";
            }

            return $"Subtitles: {subtitles.PrimaryLanguage} / {subtitles.SecondaryLanguage}";
        }
    }

    /// <summary>Légende du groupe « Game » du ruban.</summary>
    public string GamePlayersLabel => _gamePlayers.Count == 0
        ? "Game: score the room"
        : $"Game: {_gamePlayers.Count} player{(_gamePlayers.Count == 1 ? string.Empty : "s")}";

    /// <summary>
    /// Sélectionne un élément (ou aucun si <paramref name="element"/> est null).
    /// On désélectionne d'abord tous les autres éléments de la diapositive.
    /// </summary>
    public void SelectElement(SlideElement? element)
    {
        if (SelectedSlide is null)
        {
            SelectedElement = null;
            return;
        }

        foreach (SlideElement item in SelectedSlide.Elements)
        {
            item.IsSelected = ReferenceEquals(item, element);
        }

        SelectedElement = element;

        if (element is null)
        {
            SetStatus("No element selected");
        }
        else
        {
            SetStatus($"{element.TypeLabel} \"{element.Name}\" selected");
        }

        RefreshCommands();
    }

    // ------------------------------------------------------------------
    // Actions sur les diapositives (étape 2)
    // ------------------------------------------------------------------

    /// <summary>
    /// Ajoute une diapositive juste après celle qui est sélectionnée et la
    /// sélectionne. Avec la disposition « Titre et contenu » par défaut, on
    /// voit tout de suite quelque chose à l'écran.
    /// </summary>
    public void AddSlide()
    {
        PerformEdit("Ajout d'une diapositive", () =>
        {
            var slide = new Slide
            {
                Title = NextSlideTitle(),
                BackgroundColor = "#FFFFFF"
            };

            SlideLayout.Get(SlideLayoutKind.TitleAndContent).Apply(slide);

            // Insertion après la diapositive courante (à la fin s'il n'y en a pas).
            int index = SelectedSlide is null ? Slides.Count : Slides.IndexOf(SelectedSlide) + 1;
            Slides.Insert(index, slide);
            SelectedSlide = slide;
        });

        SetStatus($"Slide {CurrentSlideNumber} added");
    }

    /// <summary>
    /// Supprime la diapositive sélectionnée, puis sélectionne celle qui prend
    /// sa place (ou la dernière si on a supprimé la dernière).
    /// On refuse de supprimer la toute dernière diapositive.
    /// </summary>
    public void DeleteSlide()
    {
        if (!CanDeleteSlide(null) || SelectedSlide is null)
        {
            return;
        }

        int index = Slides.IndexOf(SelectedSlide);
        Slide removed = SelectedSlide;

        PerformEdit("Suppression d'une diapositive", () =>
        {
            // On détache d'abord la sélection pour ne pas garder de référence
            // vers une diapositive qui n'existe plus.
            SelectedSlide = null;
            Slides.Remove(removed);
            SelectedSlide = Slides[Math.Min(index, Slides.Count - 1)];
        });

        SetStatus($"Slide deleted ({Slides.Count} left)");
    }

    /// <summary>
    /// Duplique la diapositive sélectionnée : une copie indépendante est
    /// insérée juste après elle (modifier la copie ne modifie pas l'original).
    /// </summary>
    public void DuplicateSlide()
    {
        if (SelectedSlide is null)
        {
            return;
        }

        PerformEdit("Duplication d'une diapositive", () =>
        {
            Slide copy = SelectedSlide!.Clone();
            copy.Title += " (copie)";
            int index = Slides.IndexOf(SelectedSlide);
            Slides.Insert(index + 1, copy);
            SelectedSlide = copy;
        });

        SetStatus("Slide duplicated");
    }

    /// <summary>
    /// Déplace la diapositive sélectionnée de <paramref name="offset"/> places
    /// (-1 vers le haut, +1 vers le bas).
    /// </summary>
    public void MoveSelectedSlide(int offset)
    {
        if (SelectedSlide is null)
        {
            return;
        }

        int from = Slides.IndexOf(SelectedSlide);
        int to = from + offset;

        if (from < 0 || to < 0 || to >= Slides.Count)
        {
            return;
        }

        PerformEdit("Move a slide", () =>
        {
            Slides.Move(from, to);

            // La diapositive sélectionnée n'a pas changé d'identité, mais son
            // numéro a changé : on prévient l'interface.
            OnPropertyChanged(nameof(CurrentSlideNumber));
        });

        SetStatus(offset < 0 ? "Slide moved up" : "Slide moved down");
    }

    /// <summary>Applique une disposition (menu « Disposition » de Conception).</summary>
    public void ApplyLayout(SlideLayout layout)
    {
        if (layout is null)
        {
            return;
        }

        IsLayoutMenuOpen = false;

        if (SelectedSlide is not { } slide)
        {
            return;
        }

        PerformEdit($"Disposition « {layout.Name} »", () =>
        {
            layout.Apply(slide);

            // Les anciens éléments ont disparu : on vide aussi la sélection.
            SelectElement(null);
        });

        SetStatus($"Layout \"{layout.Name}\" applied");
    }

    // ------------------------------------------------------------------
    // Actions sur le contenu (étape 3)
    // ------------------------------------------------------------------

    /// <summary>Insère une zone de texte au centre-gauche de la diapositive.</summary>
    public void InsertText()
    {
        if (SelectedSlide is not { } slide)
        {
            return;
        }

        PerformEdit("Insertion d'une zone de texte", () =>
        {
            (double x, double y) = NextInsertPosition(slide);

            var element = new TextElement
            {
                Name = "Zone de texte",
                X = x,
                Y = y,
                Width = 460,
                Height = 100,
                Text = "Modifiez ce texte dans le panneau de droite.",
                FontSize = 26,
                Foreground = "#1F2937"
            };

            slide.Elements.Add(element);
            SelectElement(element);
        });

        SetStatus("Text box added");
    }

    /// <summary>
    /// Insère une image choisie par l'utilisateur.
    /// Si aucun sélecteur n'est disponible (tests), on l'indique poliment.
    /// </summary>
    public void InsertImage()
    {
        if (SelectedSlide is not { } slide)
        {
            return;
        }

        if (_fileDialogService is null)
        {
            SetStatus("No image picker available");
            return;
        }

        string? path = _fileDialogService.PickImageFile();
        if (string.IsNullOrWhiteSpace(path))
        {
            // L'utilisateur a annulé : on ne fait rien.
            return;
        }

        PerformEdit("Insertion d'une image", () =>
        {
            (double x, double y) = NextInsertPosition(slide);

            var element = new ImageElement
            {
                Name = Path.GetFileName(path),
                ImagePath = path,
                X = x,
                Y = y,
                Width = 420,
                Height = 280
            };

            slide.Elements.Add(element);
            SelectElement(element);
        });

        SetStatus($"Image \"{Path.GetFileName(path)}\" inserted");
    }

    /// <summary>
    /// Insère une vidéo choisie par l'utilisateur. Elle est muette et joue en
    /// boucle dans l'éditeur (pour vérifier son contenu), et se lit avec le
    /// son pendant le diaporama.
    /// </summary>
    public void InsertVideo()
    {
        if (SelectedSlide is not { } slide)
        {
            return;
        }

        if (_fileDialogService is null)
        {
            SetStatus("No video picker available");
            return;
        }

        string? path = _fileDialogService.PickVideoFile();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;   // annulé
        }

        // Si la présentation a déjà un fichier, on recopie la vidéo tout de
        // suite dans son dossier « _media » : le projet reste lisible même si
        // la vidéo d'origine est déplacée ou supprimée. Pour un document pas
        // encore enregistré, la copie se fera au premier enregistrement.
        string storedPath = Presentation.FilePath is { Length: > 0 } projectPath
            ? MediaLibrary.CopyIntoProject(path, projectPath) ?? path
            : path;

        PerformEdit("Insertion d'une vidéo", () =>
        {
            var element = new VideoElement
            {
                Name = Path.GetFileName(path),
                VideoPath = storedPath,
                X = 60,
                Y = 90
            };

            slide.Elements.Add(element);
            SelectElement(element);
        });

        SetStatus(storedPath != path
            ? $"Video \"{Path.GetFileName(path)}\" inserted and copied next to the project"
            : $"Video \"{Path.GetFileName(path)}\" inserted — it plays during the slide show");
    }

    /// <summary>Insère un rectangle aux coins légèrement arrondis.</summary>
    public void InsertShape()
    {
        if (SelectedSlide is not { } slide)
        {
            return;
        }

        PerformEdit("Insertion d'une forme", () =>
        {
            (double x, double y) = NextInsertPosition(slide);

            var element = new ShapeElement
            {
                Name = "Forme",
                X = x,
                Y = y,
                Width = 240,
                Height = 160,
                Fill = "#6366F1",
                Stroke = "#4338CA",
                CornerRadius = 8
            };

            slide.Elements.Add(element);
            SelectElement(element);
        });

        SetStatus("Shape added");
    }

    /// <summary>Insère un tableau 3 x 3 dont la première ligne sert d'en-tête.</summary>
    public void InsertTable()
    {
        if (SelectedSlide is not { } slide)
        {
            return;
        }

        PerformEdit("Insertion d'un tableau", () =>
        {
            (double x, double y) = NextInsertPosition(slide);

            var element = new TableElement
            {
                Name = "Tableau",
                X = x,
                Y = y,
                Width = 520,
                Height = 200,
                Columns = 3,
                Rows = 3,
                FontSize = 16
            };

            // 3 colonnes x 3 lignes = 9 cellules, remplies de gauche à droite.
            element.AddCell("Colonne 1");
            element.AddCell("Colonne 2");
            element.AddCell("Colonne 3");
            element.AddCell("Data 1");
            element.AddCell("Data 2");
            element.AddCell("Data 3");
            element.AddCell("Data 4");
            element.AddCell("Data 5");
            element.AddCell("Data 6");

            slide.Elements.Add(element);
            SelectElement(element);
        });

        SetStatus("Table added");
    }

    /// <summary>Supprime l'élément sélectionné (bouton ou touche Suppr).</summary>
    public void DeleteElement()
    {
        if (SelectedSlide is not { } slide || SelectedElement is not { } element)
        {
            return;
        }

        string type = element.TypeLabel;

        PerformEdit("Delete an element", () =>
        {
            slide.Elements.Remove(element);
            SelectElement(null);
        });

        SetStatus($"{type} deleted");
    }

    /// <summary>Copie l'élément sélectionné dans le presse-papiers interne.</summary>
    public void CopyElement()
    {
        if (SelectedElement is not { } element)
        {
            return;
        }

        _copiedElement = element.Clone();
        SetStatus($"{element.TypeLabel} copied");
        RefreshCommands();
    }

    /// <summary>Colle une copie de l'élément copié, légèrement décalée.</summary>
    public void PasteElement()
    {
        if (SelectedSlide is not { } slide || _copiedElement is not { } source)
        {
            return;
        }

        PerformEdit("Paste an element", () =>
        {
            SlideElement copy = source.Clone();
            copy.Name = source.Name + " (copie)";

            // Décalage pour que la copie ne se confonde pas avec l'original.
            // Math.Max(0, ...) : si l'élément est aussi large que la
            // diapositive, la borne haute vaut 0 et non un nombre négatif
            // (Math.Clamp refuse une borne haute inférieure à la borne basse).
            copy.X = Math.Clamp(source.X + 28, 0, Math.Max(0, SlideWidth - copy.Width));
            copy.Y = Math.Clamp(source.Y + 28, 0, Math.Max(0, SlideHeight - copy.Height));

            slide.Elements.Add(copy);
            SelectElement(copy);
        });

        SetStatus("Element pasted");
    }

    // ------------------------------------------------------------------
    // Édition directe sur la diapositive (double-clic)
    // ------------------------------------------------------------------

    /// <summary>
    /// Démarre l'édition directe d'un bloc de texte (double-clic sur la diapositive).
    /// On prend une photo AVANT la frappe : toute la saisie ne produira
    /// qu'UNE seule action d'annulation, à la fin.
    /// </summary>
    public void BeginTextEditing(SlideElement? element)
    {
        if (element is not TextElement text)
        {
            return;
        }

        EndInlineEditing();

        _editingText = text;
        _inlineOriginalText = text.Text;
        BeginPendingChange();

        text.IsEditing = true;
        SelectElement(text);
        SetStatus("Editing text: click elsewhere to confirm, Esc to cancel");
    }

    /// <summary>Démarre l'édition directe d'une cellule de tableau (double-clic).</summary>
    public void BeginCellEditing(TableCell? cell)
    {
        if (cell is null)
        {
            return;
        }

        EndInlineEditing();

        _editingCell = cell;
        _inlineOriginalText = cell.Text;
        BeginPendingChange();

        cell.IsEditing = true;
        SetStatus("Editing cell: Enter to confirm, Esc to cancel");
    }

    /// <summary>
    /// Termine l'édition directe en cours.
    ///
    /// Une seule action d'annulation est créée, et seulement si le texte a
    /// réellement changé : taper vingt lettres ne doit pas produire vingt
    /// étapes de Ctrl+Z.
    /// </summary>
    public void EndInlineEditing()
    {
        bool changed;

        if (_editingCell is { } cell)
        {
            // On oublie la cellule AVANT de la refermer : la perte de focus
            // provoquée par la disparition de la zone de saisie rappellerait
            // sinon cette méthode une seconde fois.
            _editingCell = null;
            changed = cell.Text != _inlineOriginalText;
            cell.IsEditing = false;
        }
        else if (_editingText is { } text)
        {
            _editingText = null;
            changed = text.Text != _inlineOriginalText;
            text.IsEditing = false;
        }
        else
        {
            // Rien en cours : on ne touche pas à la photo en attente, elle
            // peut appartenir à un glisser en cours.
            return;
        }

        CommitPendingChange("Modification du texte", changed);

        if (changed)
        {
            SetStatus("Text changed");
        }
    }

    /// <summary>
    /// Abandonne l'édition directe et remet le texte d'origine (touche Échap).
    /// Aucune action n'est enregistrée : rien n'a été modifié.
    /// </summary>
    public void CancelInlineEditing()
    {
        if (_editingCell is { } cell)
        {
            _editingCell = null;
            cell.Text = _inlineOriginalText;
            cell.IsEditing = false;
        }
        else if (_editingText is { } text)
        {
            _editingText = null;
            text.Text = _inlineOriginalText;
            text.IsEditing = false;
        }
        else
        {
            // Rien à annuler : on ne modifie pas le message de la barre d'état.
            return;
        }

        _pendingSnapshot = null;
        SetStatus("Edit cancelled");
    }

    // ------------------------------------------------------------------
    // Déplacement / redimensionnement à la souris
    // ------------------------------------------------------------------

    /// <summary>
    /// Appelé quand l'utilisateur commence à faire glisser un élément.
    /// On mémorise l'état pour ne créer qu'UNE seule action annulable,
    /// même si la souris génère des dizaines de mouvements.
    /// </summary>
    public void BeginElementDrag()
    {
        if (SelectedElement is not { } element)
        {
            AbandonPendingChange();
            return;
        }

        BeginPendingChange();

        _dragStartX = element.X;
        _dragStartY = element.Y;
        _dragStartWidth = element.Width;
        _dragStartHeight = element.Height;
    }

    /// <summary>Appelé quand l'utilisateur relâche la souris.</summary>
    public void EndElementDrag()
    {
        if (SelectedElement is not { } element)
        {
            AbandonPendingChange();
            return;
        }

        bool changed =
            Math.Abs(element.X - _dragStartX) > 0.01 ||
            Math.Abs(element.Y - _dragStartY) > 0.01 ||
            Math.Abs(element.Width - _dragStartWidth) > 0.01 ||
            Math.Abs(element.Height - _dragStartHeight) > 0.01;

        // Simple clic sans mouvement : la photo est simplement jetée.
        CommitPendingChange("Edit an element", changed);

        if (changed)
        {
            SetStatus("Element changed");
        }
    }

    // ------------------------------------------------------------------
    // Annuler / Rétablir
    // ------------------------------------------------------------------

    /// <summary>Annule la dernière action.</summary>
    public void Undo()
    {
        string? name = _undoRedo.Undo();

        if (name is null)
        {
            SetStatus("Nothing to undo");
            return;
        }

        UpdateModifiedState();
        SetStatus($"Undone: {name}");
        RefreshCommands();
    }

    /// <summary>Rétablit la dernière action annulée.</summary>
    public void Redo()
    {
        string? name = _undoRedo.Redo();

        if (name is null)
        {
            SetStatus("Nothing to redo");
            return;
        }

        UpdateModifiedState();
        SetStatus($"Redone: {name}");
        RefreshCommands();
    }

    // ------------------------------------------------------------------
    // Fichier (étape 4) : nouvelle présentation, ouvrir, enregistrer
    // ------------------------------------------------------------------

    /// <summary>
    /// Vrai si l'on peut abandonner le document courant.
    /// Si des modifications ne sont pas enregistrées, on demande confirmation.
    /// </summary>
    public bool ConfirmDiscardChanges()
    {
        // Une saisie en cours sur la diapositive doit être validée avant de
        // poser la question, sinon elle serait perdue sans avertissement.
        EndInlineEditing();

        if (!Presentation.IsModified || _fileDialogService is null)
        {
            return true;
        }

        return _fileDialogService.Confirm(
            "This presentation has unsaved changes.\n\n"
            + "Do you really want to discard them?");
    }

    /// <summary>Remplace le document courant par une présentation vide.</summary>
    public void NewPresentation()
    {
        IsBackstageOpen = false;

        if (!ConfirmDiscardChanges())
        {
            return;
        }

        ApplyOpenedPresentation(PresentationFactory.CreateEmptyPresentation(), null);
        SetStatus("New presentation");
    }

    /// <summary>Ouvre un fichier .scraft choisi par l'utilisateur.</summary>
    public void OpenPresentation()
    {
        IsBackstageOpen = false;

        if (_fileDialogService is null)
        {
            SetStatus("No dialog service available");
            return;
        }

        if (!ConfirmDiscardChanges())
        {
            return;
        }

        string? path = _fileDialogService.PickOpenFile();
        if (string.IsNullOrWhiteSpace(path))
        {
            // L'utilisateur a annulé : la présentation actuelle reste intacte.
            return;
        }

        Presentation loaded;

        try
        {
            loaded = PresentationFileService.Load(path);
        }
        catch (Exception exception) when (
            exception is IOException
                or UnauthorizedAccessException
                or InvalidDataException
                or FormatException
                or System.Xml.XmlException
                or NotSupportedException)
        {
            _fileDialogService.ShowError(
                "Cannot open this file.\n\n" + exception.Message);
            SetStatus("Open cancelled");
            return;
        }

        ApplyOpenedPresentation(loaded, path);
        SetStatus($"\"{Path.GetFileName(path)}\" opened");
    }

    /// <summary>
    /// Ouvre un fichier PowerPoint (.pptx) et le transforme en présentation
    /// SlideCraft : textes, images, tableaux, fonds et notes sont repris.
    ///
    /// Le document n'a pas encore de fichier .scraft : il est marqué comme
    /// modifié, pour que Windows propose de l'enregistrer à la fermeture.
    /// </summary>
    public void ImportPowerPoint()
    {
        IsBackstageOpen = false;

        if (_fileDialogService is null)
        {
            SetStatus("No dialog service available");
            return;
        }

        if (!ConfirmDiscardChanges())
        {
            return;
        }

        string? path = _fileDialogService.PickPowerPointFile();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;   // annulé
        }

        Presentation imported;

        try
        {
            imported = PowerPointImporter.Import(path);
        }
        catch (Exception exception) when (
            exception is IOException
                or UnauthorizedAccessException
                or InvalidDataException
                or NotSupportedException)
        {
            _fileDialogService.ShowError(
                "Cannot import this PowerPoint file.\n\n" + exception.Message);
            SetStatus("PowerPoint import failed");
            return;
        }

        ApplyOpenedPresentation(imported, null);

        // Contenu venu d'ailleurs et pas encore enregistré : le document est
        // modifié, même si la pile d'annulation est vide.
        Presentation.IsModified = true;
        OnPropertyChanged(nameof(DocumentTitle));
        RefreshDocumentInfo();

        SetStatus(
            $"Imported \"{Path.GetFileName(path)}\" ({Slides.Count} slides). "
            + "Save it as .scraft to keep it.");
    }

    /// <summary>Enregistre dans le fichier courant (ou demande où, la première fois).</summary>
    public void SavePresentation()
    {
        IsBackstageOpen = false;

        string? path = Presentation.FilePath;

        if (string.IsNullOrWhiteSpace(path))
        {
            SavePresentationAs();
            return;
        }

        SaveTo(path);
    }

    /// <summary>Enregistre sous un nouveau nom choisi par l'utilisateur.</summary>
    public void SavePresentationAs()
    {
        IsBackstageOpen = false;

        if (_fileDialogService is null)
        {
            SetStatus("No dialog service available");
            return;
        }

        string suggested = string.IsNullOrWhiteSpace(Presentation.FilePath)
            ? Presentation.Title
            : Path.GetFileNameWithoutExtension(Presentation.FilePath);

        string? path = _fileDialogService.PickSaveFile(
            SanitizeFileName(suggested),
            PresentationFileService.FileFilter,
            PresentationFileService.FileExtension);
        if (string.IsNullOrWhiteSpace(path))
        {
            return;   // annulé
        }

        // Si l'utilisateur a tapé un nom sans extension, on l'ajoute.
        if (!Path.HasExtension(path))
        {
            path += PresentationFileService.FileExtension;
        }

        SaveTo(path);
    }

    private void SaveTo(string path)
    {
        // Ce qui est en cours de frappe fait partie du document à enregistrer.
        EndInlineEditing();

        try
        {
            PresentationFileService.Save(Presentation, path);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            _fileDialogService?.ShowError(
                "Saving failed.\n\n" + exception.Message);
            SetStatus("Save failed");
            return;
        }

        Presentation.FilePath = path;

        // Le document est maintenant en phase avec le disque : on mémorise
        // l'action courante pour pouvoir dire « non modifié ».
        _savedEntry = _undoRedo.CurrentEntry;
        UpdateModifiedState();
        SetStatus($"Saved to \"{Path.GetFileName(path)}\"");
    }

    /// <summary>
    /// Exporte une copie de la présentation au format PowerPoint (.pptx).
    /// Le document SlideCraft n'est pas touché : on peut continuer à le
    /// modifier et à l'enregistrer en .scraft après l'export.
    ///
    /// Étape 10 : un fichier texte compagnon (« nom-notes.txt ») est écrit
    /// dans le même dossier. Il contient les notes du présentateur et les
    /// sous-titres bilingues, que PowerPoint ne sait pas reprendre.
    /// </summary>
    public void ExportPowerPoint()
    {
        IsBackstageOpen = false;

        EndInlineEditing();

        if (_fileDialogService is null)
        {
            SetStatus("No dialog service available");
            return;
        }

        string suggested = string.IsNullOrWhiteSpace(Presentation.FilePath)
            ? Presentation.Title
            : Path.GetFileNameWithoutExtension(Presentation.FilePath);

        string? path = _fileDialogService.PickSaveFile(
            SanitizeFileName(suggested),
            PowerPointExporter.FileFilter,
            PowerPointExporter.FileExtension);

        if (string.IsNullOrWhiteSpace(path))
        {
            return;   // annulé
        }

        if (!Path.HasExtension(path))
        {
            path += PowerPointExporter.FileExtension;
        }

        try
        {
            PowerPointExporter.Export(Presentation, path);
        }
        catch (Exception exception) when (
            exception is IOException
                or UnauthorizedAccessException
                or NotSupportedException
                or InvalidOperationException)
        {
            _fileDialogService.ShowError(
                "PowerPoint export failed.\n\n" + exception.Message);
            SetStatus("PowerPoint export failed");
            return;
        }

        // ---- Étape 10 : notes et sous-titres à côté du .pptx ----
        // L'échec du fichier texte ne doit pas faire croire que le .pptx
        // a échoué : il est déjà écrit et reste parfaitement utilisable.
        string notesPath = NotesExporter.BuildCompanionPath(path);

        try
        {
            NotesExporter.Export(Presentation, notesPath);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            _fileDialogService.ShowError(
                "The PowerPoint file was exported, but the notes file could not be written.\n\n"
                + exception.Message);
            SetStatus($"Exported to \"{Path.GetFileName(path)}\" (notes file failed)");
            return;
        }

        SetStatus(
            $"Exported \"{Path.GetFileName(path)}\" and \"{Path.GetFileName(notesPath)}\"");
    }

    /// <summary>
    /// Installe une présentation lue (ou une présentation vide) dans la fenêtre.
    ///
    /// On conserve la même collection <see cref="Slides"/> : l'interface y est
    /// liée, la remplacer obligerait à recréer tous les liens du XAML.
    /// </summary>
    // ------------------------------------------------------------------
    // Étape 5 : diaporama plein écran
    // ------------------------------------------------------------------

    /// <summary>
    /// Lance la projection plein écran à partir de la diapositive
    /// sélectionnée (la première si aucune n'est sélectionnée).
    ///
    /// La méthode ne rend la main qu'à la fin du diaporama : pendant ce
    /// temps, l'utilisateur ne peut pas modifier le document.
    /// </summary>
    public void StartSlideShow()
    {
        if (_slideShowService is null || Slides.Count == 0)
        {
            return;
        }

        // Une saisie directe en cours doit être validée : sans cela, le
        // diaporama afficherait l'ancien texte et la modification serait
        // perdue en revenant dans l'éditeur.
        EndInlineEditing();

        int index = SelectedSlide is null ? 0 : Slides.IndexOf(SelectedSlide);
        if (index < 0)
        {
            index = 0;
        }

        _slideShowService.Show(_presentation, index, _gamePlayers);

        SetStatus("Slide show finished — back to editing");
    }

    /// <summary>Le bouton « Lancer » n'est actif que s'il y a de quoi projeter.</summary>
    private bool CanStartSlideShow(object? parameter)
    {
        return _slideShowService is not null && Slides.Count > 0;
    }

    // ------------------------------------------------------------------
    // Étape 6 : SmartSlide
    // ------------------------------------------------------------------

    /// <summary>
    /// Analyse les diapositives (calcul local et instantané) et ouvre le
    /// rapport. La fenêtre s'ouvre sur la diapositive actuellement éditée.
    ///
    /// Rien n'est modifié dans la présentation : l'analyse est en lecture
    /// seule. Il n'y a donc ni action annulable ni document « modifié ».
    /// </summary>
    public void AnalyzeSlides()
    {
        if (_smartSlideService is null || Slides.Count == 0)
        {
            return;
        }

        // Une saisie directe en cours doit être validée : sinon l'analyse
        // porterait sur l'ancien texte, et la frappe serait perdue.
        EndInlineEditing();

        int index = SelectedSlide is null ? 0 : Slides.IndexOf(SelectedSlide);
        if (index < 0)
        {
            index = 0;
        }

        _smartSlideService.Show(_presentation, index);
        SetStatus("SmartSlide: analysis complete");
    }

    /// <summary>Le bouton SmartSlide n'est actif que s'il y a des diapositives.</summary>
    private bool CanAnalyzeSlides(object? parameter)
    {
        return _smartSlideService is not null && Slides.Count > 0;
    }

    // ------------------------------------------------------------------
    // Étape 8 : quiz et mode jeu
    // ------------------------------------------------------------------

    /// <summary>
    /// Ouvre l'éditeur du quiz de la diapositive sélectionnée.
    /// Le ViewModel travaille sur une COPIE du quiz : tant que l'utilisateur
    /// n'a pas validé, la présentation n'est pas touchée.
    /// </summary>
    public void EditQuiz()
    {
        if (SelectedSlide is not { } slide)
        {
            return;
        }

        if (_quizEditorService is null)
        {
            SetStatus("No quiz editor available");
            return;
        }

        // Une saisie directe en cours doit être validée avant d'ouvrir la
        // fenêtre, sinon le texte tapé serait perdu.
        EndInlineEditing();

        QuizEditorResult? result = _quizEditorService.Edit(slide.Quiz?.Clone(), slide.Title);

        if (result is null || !result.Confirmed)
        {
            return;   // annulé : rien ne change
        }

        // Rien à enregistrer si le contenu est identique (ou si l'on supprime
        // un quiz qui n'existe pas) : le document ne doit pas devenir
        // « modifié » pour rien.
        if (result.Quiz is null && slide.Quiz is null)
        {
            return;
        }

        if (result.Quiz is not null && result.Quiz.SameContentAs(slide.Quiz))
        {
            return;
        }

        PerformEdit("Edit quiz", () => slide.Quiz = result.Quiz);

        OnPropertyChanged(nameof(HasQuiz));
        OnPropertyChanged(nameof(QuizSummary));
        OnPropertyChanged(nameof(CurrentSlideNumber));
        OnPropertyChanged(string.Empty);

        SetStatus(result.Quiz is null ? "Quiz removed from this slide" : "Quiz saved");
    }

    /// <summary>
    /// Demande la liste des joueurs du mode jeu. Les noms restent sur le PC :
    /// la salle vote à main levée, personne n'utilise de téléphone.
    /// </summary>
    public void ConfigureGame()
    {
        if (_gameSetupService is null)
        {
            SetStatus("No game setup available");
            return;
        }

        IReadOnlyList<string>? players = _gameSetupService.ConfigurePlayers(_gamePlayers);

        if (players is null)
        {
            return;   // annulé
        }

        _gamePlayers.Clear();

        // Doublons et lignes vides ignorés (sans tenir compte des majuscules).
        var seen = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);

        foreach (string name in players)
        {
            string trimmed = name?.Trim() ?? string.Empty;

            if (trimmed.Length > 0 && seen.Add(trimmed))
            {
                _gamePlayers.Add(trimmed);
            }
        }

        OnPropertyChanged(nameof(GamePlayersLabel));

        SetStatus(_gamePlayers.Count == 0
            ? "Game mode: no players, the room scores"
            : $"Game mode: {_gamePlayers.Count} player{(_gamePlayers.Count == 1 ? string.Empty : "s")}");
    }

    /// <summary>
    /// Ouvre l'écriture des sous-titres bilingues de la diapositive.
    ///
    /// Les deux langues sont écrites à la main : SlideCraft ne traduit rien
    /// et n'utilise pas Internet. Le travail se fait sur une copie ; la
    /// présentation n'est touchée qu'après validation.
    /// </summary>
    public void EditSubtitles()
    {
        if (SelectedSlide is not { } slide)
        {
            return;
        }

        if (_subtitleEditorService is null)
        {
            SetStatus("No subtitle editor available");
            return;
        }

        // Une saisie directe en cours doit être validée avant d'ouvrir la
        // fenêtre, sinon le texte tapé serait perdu.
        EndInlineEditing();

        SubtitleEditorResult? result = _subtitleEditorService.Edit(slide.Subtitles?.Clone(), slide.Title);

        if (result is null || !result.Confirmed)
        {
            return;   // annulé : rien ne change
        }

        // Rien à enregistrer si le contenu est identique (ou si l'on supprime
        // des sous-titres qui n'existent pas).
        if (result.Subtitles is null && slide.Subtitles is null)
        {
            return;
        }

        if (result.Subtitles is not null && result.Subtitles.SameContentAs(slide.Subtitles))
        {
            return;
        }

        PerformEdit("Edit subtitles", () => slide.Subtitles = result.Subtitles);

        OnPropertyChanged(nameof(HasSubtitles));
        OnPropertyChanged(nameof(SubtitleSummary));

        SetStatus(result.Subtitles is null
            ? "Subtitles removed from this slide"
            : "Subtitles saved");
    }

    private void ApplyOpenedPresentation(Presentation presentation, string? path)
    {
        AbandonPendingChange();
        SelectedSlide = null;
        Slides.Clear();

        foreach (Slide slide in presentation.Slides)
        {
            Slides.Add(slide);
        }

        Presentation.Title = presentation.Title;
        Presentation.FilePath = path;
        SelectedSlide = Slides.FirstOrDefault();

        // Nouveau document : l'historique du précédent n'a plus de sens.
        _undoRedo.Clear();
        _savedEntry = null;
        UpdateModifiedState();
        OnPropertyChanged(nameof(DocumentTitle));
    }

    /// <summary>Retire les caractères interdits dans un nom de fichier Windows.</summary>
    private static string SanitizeFileName(string name)
    {
        foreach (char invalid in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(invalid, ' ');
        }

        name = name.Trim();
        return string.IsNullOrEmpty(name) ? "Presentation" : name;
    }

    // ------------------------------------------------------------------
    // Conditions d'activation des boutons (CanExecute)
    // ------------------------------------------------------------------

    private bool CanDeleteSlide(object? parameter) => SelectedSlide is not null && Slides.Count > 1;

    private bool CanDuplicateSlide(object? parameter) => SelectedSlide is not null;

    private bool CanEditSlide(object? parameter) => SelectedSlide is not null;

    private bool CanMoveSlideUp(object? parameter)
        => SelectedSlide is not null && Slides.IndexOf(SelectedSlide) > 0;

    private bool CanMoveSlideDown(object? parameter)
    {
        if (SelectedSlide is null)
        {
            return false;
        }

        int index = Slides.IndexOf(SelectedSlide);
        return index >= 0 && index < Slides.Count - 1;
    }

    /// <summary>Demande à WPF de réévaluer l'état (activé/désactivé) des boutons.</summary>
    private static void RefreshCommands()
    {
        CommandManager.InvalidateRequerySuggested();
    }

    // ------------------------------------------------------------------
    // Annuler / Rétablir : le mécanisme des instantanés
    // ------------------------------------------------------------------

    /// <summary>
    /// Exécute une modification et l'enregistre dans la pile d'annulation.
    ///
    /// On prend une photo AVANT, on applique le changement, puis une photo
    /// APRÈS. Annuler restaure la première, rétablir restaure la seconde.
    /// </summary>
    private void PerformEdit(string name, Action change)
    {
        PresentationSnapshot before = CaptureSnapshot();
        change();
        RecordSnapshot(name, before);
    }

    /// <summary>
    /// Empile une action annulable à partir d'une photo « avant » et d'une
    /// photo de l'état actuel. Une seule méthode pour toutes les modifications,
    /// y compris le glisser à la souris.
    /// </summary>
    private void RecordSnapshot(string name, PresentationSnapshot before)
    {
        PresentationSnapshot after = CaptureSnapshot();

        _undoRedo.Record(new EditEntry(
            name,
            () => RestoreSnapshot(before),
            () => RestoreSnapshot(after)));

        UpdateModifiedState();
        RefreshCommands();
    }

    /// <summary>
    /// Met à jour l'étoile « modifié » de la barre de titre.
    /// On compare l'action courante à celle du dernier enregistrement :
    /// annuler jusqu'à retrouver cet état rend le document « non modifié ».
    /// </summary>
    private void UpdateModifiedState()
    {
        Presentation.IsModified = !ReferenceEquals(_undoRedo.CurrentEntry, _savedEntry);
        OnPropertyChanged(nameof(DocumentTitle));
        RefreshDocumentInfo();
    }

    /// <summary>
    /// Prévient la page File que le nom, le dossier ou l'état du document
    /// ont changé (enregistrement, ouverture, annulation...).
    /// </summary>
    private void RefreshDocumentInfo()
    {
        OnPropertyChanged(nameof(DocumentFileName));
        OnPropertyChanged(nameof(DocumentLocation));
        OnPropertyChanged(nameof(DocumentState));
    }

    /// <summary>Mémorise l'état AVANT une modification continue.</summary>
    private void BeginPendingChange()
    {
        _pendingSnapshot = CaptureSnapshot();
    }

    /// <summary>
    /// Termine une modification continue : une seule action est empilée, et
    /// seulement si quelque chose a réellement changé.
    /// </summary>
    private void CommitPendingChange(string name, bool changed)
    {
        PresentationSnapshot? before = _pendingSnapshot;
        _pendingSnapshot = null;

        if (!changed || before is null)
        {
            return;
        }

        RecordSnapshot(name, before);
    }

    /// <summary>
    /// Abandonne une modification continue sans rien enregistrer. On oublie
    /// aussi l'édition en cours : les objets de la présentation vont être
    /// remplacés, les références vers eux deviendraient fausses.
    /// </summary>
    private void AbandonPendingChange()
    {
        if (_editingText is { } text)
        {
            text.IsEditing = false;
            _editingText = null;
        }

        if (_editingCell is { } cell)
        {
            cell.IsEditing = false;
            _editingCell = null;
        }

        _pendingSnapshot = null;
    }

    /// <summary>Photo de l'état actuel des diapositives.</summary>
    private PresentationSnapshot CaptureSnapshot()
    {
        int index = SelectedSlide is null ? -1 : Slides.IndexOf(SelectedSlide);
        return new PresentationSnapshot(Slides, index);
    }

    /// <summary>Remet la présentation dans l'état mémorisé par une photo.</summary>
    private void RestoreSnapshot(PresentationSnapshot snapshot)
    {
        // Les objets vont être remplacés : on abandonne le geste ou la saisie
        // en cours, puis l'élément sélectionné.
        AbandonPendingChange();
        SelectedElement = null;

        snapshot.Restore(Slides);

        if (Slides.Count == 0)
        {
            SelectedSlide = null;
            return;
        }

        int index = Math.Clamp(snapshot.SelectedIndex, 0, Slides.Count - 1);
        SelectedSlide = Slides[index];
    }

    /// <summary>
    /// Modifie une valeur géométrique de l'élément sélectionné (annulable).
    ///
    /// On essaie d'abord le calcul sur une copie : quand l'utilisateur quitte
    /// simplement une case du panneau de propriétés sans rien changer, aucune
    /// action n'est créée. Sans cela, la pile d'annulation se remplirait de
    /// modifications vides et Ctrl+Z semblerait ne rien faire.
    /// </summary>
    private void SetGeometry(SlideElement? element, Action<SlideElement> apply, string name)
    {
        if (element is null)
        {
            return;
        }

        SlideElement probe = element.Clone();
        apply(probe);

        if (Math.Abs(element.X - probe.X) < 0.01
            && Math.Abs(element.Y - probe.Y) < 0.01
            && Math.Abs(element.Width - probe.Width) < 0.01
            && Math.Abs(element.Height - probe.Height) < 0.01)
        {
            return;
        }

        PerformEdit(name, () => apply(element));
        NotifySelectedElementChanged();
    }

    /// <summary>
    /// Cherche une position libre pour un nouvel élément : on décale chaque
    /// insertion de 18 pixels pour éviter que tout se superpose.
    /// </summary>
    private static (double X, double Y) NextInsertPosition(Slide slide)
    {
        double offset = (slide.Elements.Count % 8) * 18;
        return (90 + offset, 90 + offset);
    }

    /// <summary>Cherche un titre « Diapositive N » encore libre.</summary>
    private string NextSlideTitle()
    {
        int number = Slides.Count + 1;

        while (Slides.Any(slide => slide.Title == $"Slide {number}"))
        {
            number++;
        }

        return $"Slide {number}";
    }

    /// <summary>Enlève le cadre bleu de tous les éléments d'une diapositive.</summary>
    private static void ClearSelectionOn(Slide? slide)
    {
        if (slide is null)
        {
            return;
        }

        foreach (SlideElement item in slide.Elements)
        {
            item.IsSelected = false;
        }
    }

    // ------------------------------------------------------------------
    // Étape 12 : transitions (onglet Transitions)
    // ------------------------------------------------------------------

    /// <summary>
    /// Applique la transition choisie à la diapositive affichée.
    /// Le paramètre vient du bouton du ruban : "None", "Fade", "Push" ou "Wipe".
    /// C'est une vraie modification : elle est annulable avec Ctrl+Z.
    /// </summary>
    public void SetTransition(object? parameter)
    {
        if (SelectedSlide is not { } slide || !TransitionCatalog.TryParse(parameter, out TransitionKind kind))
        {
            return;
        }

        if (slide.Transition == kind)
        {
            // Même transition : rien à enregistrer, mais l'utilisateur voit
            // tout de même ce qui est déjà appliqué.
            SetStatus($"{TransitionCatalog.Label(kind)} is already the transition of this slide");
            return;
        }

        PerformEdit(
            $"Transition: {TransitionCatalog.Label(kind)}",
            () => slide.Transition = kind);

        NotifyTransitionChanged();
        SetStatus(
            $"Transition: {TransitionCatalog.Label(kind)} — {TransitionCatalog.Description(kind)}");
    }

    /// <summary>
    /// Copie la transition de la diapositive affichée sur toutes les
    /// diapositives (bouton « Apply to All »). C'est une seule action
    /// annulable : un Ctrl+Z remet toutes les anciennes transitions.
    /// </summary>
    public void ApplyTransitionToAllSlides()
    {
        if (SelectedSlide is not { } selected)
        {
            return;
        }

        TransitionKind kind = selected.Transition;

        if (Slides.All(slide => slide.Transition == kind))
        {
            SetStatus($"All slides already use {TransitionCatalog.Label(kind)}");
            return;
        }

        PerformEdit(
            $"Transition {TransitionCatalog.Label(kind)} on all slides",
            () =>
            {
                foreach (Slide slide in Slides)
                {
                    slide.Transition = kind;
                }
            });

        NotifyTransitionChanged();
        SetStatus($"All slides now use {TransitionCatalog.Label(kind)}");
    }

    /// <summary>Prévient le ruban que la transition affichée a changé.</summary>
    private void NotifyTransitionChanged()
    {
        OnPropertyChanged(nameof(CurrentTransition));
        OnPropertyChanged(nameof(CurrentTransitionLabel));
    }

    // ------------------------------------------------------------------
    // Étape 12 : revue (onglet Review)
    // ------------------------------------------------------------------

    /// <summary>
    /// Ouvre le résumé de revue : nombre de diapositives, de mots, temps de
    /// lecture estimé et historique des modifications. Rien n'est modifié
    /// dans la présentation : c'est un rapport en lecture seule.
    /// </summary>
    public void ShowReview()
    {
        if (_reviewService is null || Slides.Count == 0)
        {
            return;
        }

        // Une saisie directe en cours doit être validée : sinon le nombre de
        // mots serait calculé sur l'ancien texte.
        EndInlineEditing();

        _reviewService.Show(_presentation, _undoRedo.RecentActions, UndoCommand, RedoCommand);
        SetStatus("Review summary closed");
    }

    /// <summary>Change la couleur de fond de la diapositive (paramètre = "#RRGGBB").</summary>
    private void SetBackground(object? parameter)
    {
        if (SelectedSlide is not { } slide || parameter is not string color)
        {
            return;
        }

        if (slide.BackgroundColor == color)
        {
            // Même couleur : rien à enregistrer, rien à annuler.
            return;
        }

        PerformEdit("Background changed", () => slide.BackgroundColor = color);
        SetStatus("Background changed");
    }

    /// <summary>Change la couleur d'un texte sélectionné (paramètre = "#RRGGBB").</summary>
    private void SetForeground(object? parameter)
    {
        if (SelectedElement is not TextElement text || parameter is not string color)
        {
            return;
        }

        PerformEdit("Text color changed", () => text.Foreground = color);
        NotifySelectedElementChanged();
        SetStatus("Text color changed");
    }

    /// <summary>Change la couleur de remplissage d'une forme (paramètre = "#RRGGBB").</summary>
    private void SetFill(object? parameter)
    {
        if (SelectedElement is not ShapeElement shape || parameter is not string color)
        {
            return;
        }

        PerformEdit("Shape color changed", () =>
        {
            shape.Fill = color;
            shape.Stroke = color;
        });

        NotifySelectedElementChanged();
        SetStatus("Shape color changed");
    }

    /// <summary>Appelé par le XAML via l'interface ICommand (paramètre typé).</summary>
    private void ApplyLayout(object? parameter)
    {
        if (parameter is SlideLayout layout)
        {
            ApplyLayout(layout);
            return;
        }

        IsLayoutMenuOpen = false;
    }

    /// <summary>
    /// Prévient l'interface que les propriétés de l'élément ont changé
    /// (appelé aussi par la fenêtre pendant un glisser ou un redimensionnement).
    ///
    /// Un nom de propriété vide signifie « tout a pu changer » : WPF rafraîchit
    /// alors le panneau entier. Cela évite une longue liste à compléter à
    /// chaque nouvelle propriété — un oubli y était très facile.
    /// </summary>
    public void NotifySelectedElementChanged()
    {
        OnPropertyChanged(string.Empty);
    }

    private void SetStatus(string message)
    {
        StatusMessage = message;
    }
}
