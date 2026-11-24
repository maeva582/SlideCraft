using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using SlideCraft.Models;

namespace SlideCraft.ViewModels;

/// <summary>
/// Le ViewModel de la fenêtre « Review summary » (onglet Review).
///
/// Il ne modifie rien : il compte et il affiche. Les deux seules actions
/// sont « Undo » et « Redo », qui sont les mêmes commandes que Ctrl+Z et
/// Ctrl+Y dans l'éditeur.
///
/// Tout est calculé ici, sans WPF : la fenêtre ne fait que dessiner, et le
/// calcul peut être vérifié sans Windows.
/// </summary>
public class ReviewViewModel : ObservableObject
{
    /// <summary>
    /// Vitesse de lecture utilisée pour l'estimation : 130 mots par minute.
    /// C'est la moyenne prudente d'une présentation orale (on parle plus
    /// lentement qu'on ne lit un livre).
    /// </summary>
    public const int WordsPerMinute = 130;

    public ReviewViewModel(
        Presentation presentation,
        IReadOnlyList<string> history,
        ICommand? undoCommand,
        ICommand? redoCommand)
    {
        if (presentation is null)
        {
            throw new ArgumentNullException(nameof(presentation));
        }

        SlideCount = presentation.Slides.Count;

        // Les mots comptés sont ceux que le public voit : le texte des blocs
        // de texte. Les notes et le script de narration sont comptés à part,
        // car eux ne sont pas projetés.
        SlideWordCount = presentation.Slides.Sum(CountSlideWords);
        NotesWordCount = presentation.Slides.Sum(CountNotesWords);

        SlidesWithoutNotes = presentation.Slides.Count(slide =>
            string.IsNullOrWhiteSpace(slide.Notes.PersonalNotes)
            && string.IsNullOrWhiteSpace(slide.Notes.NarrationText));

        SlidesWithTransition = presentation.Slides.Count(slide =>
            slide.Transition != TransitionKind.None);

        UndoCommand = undoCommand;
        RedoCommand = redoCommand;

        // L'historique arrive déjà de la plus récente à la plus ancienne.
        // La numérotation suit cet ordre : 1 = ce que Ctrl+Z déferait d'abord.
        HistoryItems = history?
            .Select((name, index) => $"{index + 1}. {name}")
            .ToList()
            ?? new List<string>();
    }

    /// <summary>Nombre de diapositives de la présentation.</summary>
    public int SlideCount { get; }

    /// <summary>Nombre de mots réellement affichés sur les diapositives.</summary>
    public int SlideWordCount { get; }

    /// <summary>Nombre de mots des notes et des scripts de narration.</summary>
    public int NotesWordCount { get; }

    /// <summary>Diapositives sans aucune note (ni note privée ni script).</summary>
    public int SlidesWithoutNotes { get; }

    /// <summary>Diapositives qui ont une transition (Fade, Push ou Wipe).</summary>
    public int SlidesWithTransition { get; }

    /// <summary>Durée estimée de la lecture des textes projetés.</summary>
    public TimeSpan ReadingTime => TimeSpan.FromMinutes(
        SlideWordCount / (double)WordsPerMinute);

    public string SlideCountLabel => $"{SlideCount} slide(s)";

    public string SlideWordCountLabel => $"{SlideWordCount} word(s) on the slides";

    public string NotesWordCountLabel => $"{NotesWordCount} word(s) in notes and scripts";

    /// <summary>
    /// « about 4 min » : la durée est arrondie à la minute supérieure, car
    /// annoncer « 0 min » pour une présentation courte n'aiderait personne.
    /// </summary>
    public string ReadingTimeLabel => SlideWordCount == 0
        ? "No text to read yet"
        : $"about {Math.Max(1, (int)Math.Ceiling(ReadingTime.TotalMinutes))} min to read the slides aloud";

    public string NotesLabel => SlidesWithoutNotes == 0
        ? "Every slide has notes or a narration script."
        : $"{SlidesWithoutNotes} slide(s) have no notes and no narration script.";

    public string TransitionLabel => SlidesWithTransition == 0
        ? "No transition: slides appear instantly during the show."
        : $"{SlidesWithTransition} of {SlideCount} slide(s) use a transition.";

    /// <summary>Lignes de l'historique, la plus récente en premier.</summary>
    public IReadOnlyList<string> HistoryItems { get; }

    /// <summary>Vrai quand l'historique est vide (rien à annuler).</summary>
    public bool HasHistory => HistoryItems.Count > 0;

    /// <summary>Vrai quand l'historique est vide, pour afficher une explication.</summary>
    public bool IsHistoryEmpty => !HasHistory;

    /// <summary>Phrase affichée au-dessus de la liste.</summary>
    /// <remarks>
    /// La liste est une photo prise à l'ouverture de la fenêtre : après un
    /// Undo, elle n'est pas recalculée. Le texte le dit clairement, plutôt
    /// que de laisser croire à une liste fausse.
    /// </remarks>
    public string HistoryExplanation => HasHistory
        ? "Newest first. \"Undo\" removes the first line, exactly like Ctrl+Z in the editor. "
          + "The list is a snapshot: close and reopen this window to see it updated."
        : "No change yet: the list fills up as you edit the presentation.";

    /// <summary>Commande d'annulation de l'éditeur (Ctrl+Z), null en test.</summary>
    public ICommand? UndoCommand { get; }

    /// <summary>Commande de rétablissement de l'éditeur (Ctrl+Y), null en test.</summary>
    public ICommand? RedoCommand { get; }

    /// <summary>Compte les mots d'une diapositive (uniquement le texte projeté).</summary>
    private static int CountSlideWords(Slide slide)
    {
        return slide.Elements
            .OfType<TextElement>()
            .Sum(element => CountWords(element.Text));
    }

    /// <summary>Compte les mots des notes privées et du script de narration.</summary>
    private static int CountNotesWords(Slide slide)
    {
        return CountWords(slide.Notes.PersonalNotes) + CountWords(slide.Notes.NarrationText);
    }

    /// <summary>
    /// Compte les mots d'un texte : les espaces, retours à la ligne et
    /// tabulations séparent les mots, tout le reste compte pour un mot.
    /// </summary>
    public static int CountWords(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        return text.Split(
                new[] { ' ', '\t', '\r', '\n' },
                StringSplitOptions.RemoveEmptyEntries)
            .Length;
    }
}
