using System.Collections.ObjectModel;
using SlideCraft.ViewModels;

namespace SlideCraft.Models;

/// <summary>
/// Une diapositive : un arrière-plan, une liste d'éléments, des notes
/// et éventuellement un quiz.
/// </summary>
public class Slide : ObservableObject
{
    private string _title = "Untitled slide";
    private string _backgroundColor = "#FFFFFF";
    private TransitionKind _transition = TransitionKind.None;

    /// <summary>Titre interne (visible dans les propriétés et la carte StoryMap).</summary>
    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    /// <summary>Couleur de fond au format "#RRGGBB".</summary>
    public string BackgroundColor
    {
        get => _backgroundColor;
        set => SetProperty(ref _backgroundColor, value);
    }

    /// <summary>
    /// Effet d'entrée joué quand cette diapositive apparaît pendant le
    /// diaporama (onglet Transitions). « None » = apparition immédiate.
    /// </summary>
    public TransitionKind Transition
    {
        get => _transition;
        set => SetProperty(ref _transition, value);
    }

    /// <summary>Éléments affichés sur la diapositive (texte, images, formes...).</summary>
    public ObservableCollection<SlideElement> Elements { get; } = new();

    /// <summary>Notes privées du présentateur.</summary>
    public PresenterNotes Notes { get; } = new();

    /// <summary>Quiz de la diapositive, null s'il n'y en a pas.</summary>
    public Quiz? Quiz { get; set; }

    /// <summary>
    /// Sous-titres bilingues écrits à la main, null s'il n'y en a pas.
    /// Ils sont affichés en bas de l'écran pendant la projection.
    /// </summary>
    public SlideSubtitles? Subtitles { get; set; }

    /// <summary>
    /// Crée une copie indépendante et strictement identique de la diapositive.
    /// Les éléments, les notes et l'arrière-plan sont recopiés : modifier la
    /// copie ne modifie jamais l'original.
    /// </summary>
    public Slide Clone()
    {
        var copy = new Slide
        {
            Title = Title,
            BackgroundColor = BackgroundColor,
            Transition = Transition
        };

        copy.Notes.PersonalNotes = Notes.PersonalNotes;
        copy.Notes.CoachTip = Notes.CoachTip;
        copy.Notes.NarrationText = Notes.NarrationText;
        copy.Notes.TimeLimitSeconds = Notes.TimeLimitSeconds;

        foreach (SlideElement element in Elements)
        {
            copy.Elements.Add(element.Clone());
        }

        // Le quiz fait partie du contenu : sans cette copie, un simple
        // Ctrl+Z effacerait les questions de la diapositive.
        if (Quiz is not null)
        {
            copy.Quiz = Quiz.Clone();
        }

        // Même raison pour les sous-titres bilingues.
        if (Subtitles is not null)
        {
            copy.Subtitles = Subtitles.Clone();
        }

        return copy;
    }
}
