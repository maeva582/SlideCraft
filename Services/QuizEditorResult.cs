using SlideCraft.Models;

namespace SlideCraft.Services;

/// <summary>
/// Résultat de la fenêtre « Quiz ».
///
/// Il faut distinguer trois cas : annulé (aucun résultat), quiz enregistré
/// (<see cref="Quiz"/> rempli) et quiz supprimé (<see cref="Quiz"/> null).
/// Un simple « Quiz? » ne pouvait pas dire la différence entre annuler et
/// supprimer.
/// </summary>
public sealed class QuizEditorResult
{
    public QuizEditorResult(bool confirmed, Quiz? quiz)
    {
        Confirmed = confirmed;
        Quiz = quiz;
    }

    /// <summary>Vrai quand l'utilisateur a validé (enregistrer ou supprimer).</summary>
    public bool Confirmed { get; }

    /// <summary>Nouveau quiz, ou null pour supprimer le quiz de la diapositive.</summary>
    public Quiz? Quiz { get; }
}
