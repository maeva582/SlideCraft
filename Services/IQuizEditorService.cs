using SlideCraft.Models;

namespace SlideCraft.Services;

/// <summary>
/// Ouvre la fenêtre d'édition du quiz d'une diapositive.
///
/// Même principe que <see cref="ISmartSlideService"/> : le ViewModel
/// principal ne crée pas de fenêtre WPF lui-même, il appelle cette interface
/// (remplaçable par une doublure dans les tests).
/// </summary>
public interface IQuizEditorService
{
    /// <summary>
    /// Affiche l'éditeur. <paramref name="quiz"/> est une copie du quiz actuel
    /// (null si la diapositive n'en a pas).
    /// Renvoie null si l'utilisateur annule.
    /// </summary>
    QuizEditorResult? Edit(Quiz? quiz, string slideTitle);
}
