using SlideCraft.Models;

namespace SlideCraft.Services;

/// <summary>
/// Ouvre la fenêtre d'écriture des sous-titres bilingues.
///
/// Même principe que <see cref="IQuizEditorService"/> : le ViewModel
/// principal ne crée pas de fenêtre WPF lui-même, il appelle cette
/// interface (remplaçable par une doublure dans les tests).
/// </summary>
public interface ISubtitleEditorService
{
    /// <summary>
    /// Affiche l'éditeur. <paramref name="subtitles"/> est une copie des
    /// sous-titres actuels (null si la diapositive n'en a pas).
    /// Renvoie null si l'utilisateur annule.
    /// </summary>
    SubtitleEditorResult? Edit(SlideSubtitles? subtitles, string slideTitle);
}
