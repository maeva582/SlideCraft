using SlideCraft.ViewModels;

namespace SlideCraft.Models;

/// <summary>
/// Les notes du présentateur, attachées à UNE diapositive.
/// Elles ne sont jamais affichées sur la projection du public :
/// elles servent à l'onglet "Présentateur" et au coach.
/// </summary>
public class PresenterNotes : ObservableObject
{
    private string _personalNotes = string.Empty;
    private string _coachTip = string.Empty;
    private string _narrationText = string.Empty;
    private int _timeLimitSeconds = 60;

    /// <summary>Texte libre écrit par l'utilisateur pour préparer son exposé.</summary>
    public string PersonalNotes
    {
        get => _personalNotes;
        set => SetProperty(ref _personalNotes, value);
    }

    /// <summary>Conseil généré automatiquement (fonction PresenterCoach).</summary>
    public string CoachTip
    {
        get => _coachTip;
        set => SetProperty(ref _coachTip, value);
    }

    /// <summary>
    /// Texte lu à voix haute pendant la projection (fonction « narration »).
    /// S'il est vide, la voix lit automatiquement le titre et les textes
    /// affichés sur la diapositive : on peut donc projeter sans rien écrire.
    /// </summary>
    public string NarrationText
    {
        get => _narrationText;
        set => SetProperty(ref _narrationText, value);
    }

    /// <summary>Durée conseillée pour cette diapositive (utilisée par TimeSlide).</summary>
    public int TimeLimitSeconds
    {
        get => _timeLimitSeconds;
        set => SetProperty(ref _timeLimitSeconds, value);
    }
}
