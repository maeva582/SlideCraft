using System;
using System.Collections.Generic;
using System.Linq;

namespace SlideCraft.Models;

/// <summary>
/// Le résultat SmartSlide pour UNE diapositive : une note de 0 à 100,
/// une appréciation et la liste des points relevés.
///
/// C'est un objet figé (aucune propriété modifiable) : il décrit une photo
/// de la diapositive au moment de l'analyse. Relancer l'analyse en crée un
/// nouveau.
/// </summary>
public class SlideAnalysis
{
    public SlideAnalysis(
        int slideNumber,
        string slideTitle,
        int score,
        IReadOnlyList<SlideIssue> issues)
    {
        SlideNumber = slideNumber;
        SlideTitle = string.IsNullOrWhiteSpace(slideTitle) ? "Untitled" : slideTitle;
        Score = Math.Clamp(score, 0, 100);

        // Les points les plus lourds d'abord : c'est ce qu'il faut corriger
        // en priorité. ThenBy rend l'ordre stable et donc testable.
        Issues = issues
            .OrderByDescending(issue => issue.Penalty)
            .ThenBy(issue => issue.Title, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>Numéro de la diapositive (1 = la première).</summary>
    public int SlideNumber { get; }

    /// <summary>Titre de la diapositive (jamais vide).</summary>
    public string SlideTitle { get; }

    /// <summary>Note de 0 à 100 (100 = aucun défaut détecté).</summary>
    public int Score { get; }

    public IReadOnlyList<SlideIssue> Issues { get; }

    /// <summary>Texte « 82/100 ».</summary>
    public string ScoreLabel => $"{Score}/100";

    /// <summary>Appréciation en un mot.</summary>
    public string Grade => GradeFor(Score);

    /// <summary>Nombre de points qui coûtent réellement des points.</summary>
    public int ProblemCount => Issues.Count(issue => issue.Penalty > 0);

    /// <summary>Vrai si la diapositive a au moins un défaut à corriger.</summary>
    public bool HasProblems => ProblemCount > 0;

    /// <summary>Phrase affichée en haut du détail.</summary>
    public string Summary => HasProblems
        ? $"{ProblemCount} issue(s) to fix on this slide."
        : "Nothing to report: this slide is clear.";

    /// <summary>Appréciation correspondant à une note (utilisée aussi pour la moyenne).</summary>
    public static string GradeFor(int score) => score switch
    {
        >= 90 => "Excellent",
        >= 75 => "Good",
        >= 60 => "Needs work",
        _ => "Needs rework"
    };
}
