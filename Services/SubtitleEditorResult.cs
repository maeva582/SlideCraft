using SlideCraft.Models;

namespace SlideCraft.Services;

/// <summary>
/// Résultat de la fenêtre « Subtitles ».
///
/// Trois cas, comme pour le quiz : annulé (aucun résultat), sous-titres
/// enregistrés (<see cref="Subtitles"/> rempli) et sous-titres supprimés
/// (<see cref="Subtitles"/> null).
/// </summary>
public sealed class SubtitleEditorResult
{
    public SubtitleEditorResult(bool confirmed, SlideSubtitles? subtitles)
    {
        Confirmed = confirmed;
        Subtitles = subtitles;
    }

    /// <summary>Vrai quand l'utilisateur a validé (enregistrer ou supprimer).</summary>
    public bool Confirmed { get; }

    /// <summary>Nouveaux sous-titres, ou null pour les retirer de la diapositive.</summary>
    public SlideSubtitles? Subtitles { get; }
}
