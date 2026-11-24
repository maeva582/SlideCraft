using System;
using System.Collections.Generic;
using System.Linq;
using SlideCraft.Models;

namespace SlideCraft.ViewModels;

/// <summary>
/// Le ViewModel de la fenêtre SmartSlide : il présente le rapport d'analyse
/// (une note par diapositive + la moyenne) et laisse l'utilisateur passer
/// d'une diapositive à l'autre dans la liste.
///
/// Il ne fait aucun calcul de note : c'est <see cref="Services.SmartSlideAnalyzer"/>
/// qui analyse, et il ne connaît pas WPF : on peut donc le tester sans Windows.
/// </summary>
public class SmartSlideViewModel : ObservableObject
{
    private SlideAnalysis _selectedAnalysis;

    public SmartSlideViewModel(IReadOnlyList<SlideAnalysis> analyses, int startIndex)
    {
        if (analyses is null)
        {
            throw new ArgumentNullException(nameof(analyses));
        }

        if (analyses.Count == 0)
        {
            throw new ArgumentException(
                "There is no slide to analyze.",
                nameof(analyses));
        }

        Analyses = analyses;

        // On ouvre le rapport sur la diapositive que l'utilisateur était en
        // train de modifier (indice ramené dans les limites).
        _selectedAnalysis = analyses[Math.Clamp(startIndex, 0, analyses.Count - 1)];
    }

    /// <summary>Toutes les analyses, dans l'ordre des diapositives.</summary>
    public IReadOnlyList<SlideAnalysis> Analyses { get; }

    /// <summary>Analyse affichée en détail à droite.</summary>
    public SlideAnalysis SelectedAnalysis
    {
        get => _selectedAnalysis;
        set
        {
            // La ListBox peut envoyer null pendant un changement de liste :
            // on l'ignore plutôt que de casser l'affichage.
            if (value is null)
            {
                return;
            }

            SetProperty(ref _selectedAnalysis, value);
        }
    }

    /// <summary>Note moyenne de la présentation, arrondie.</summary>
    public int AverageScore => (int)Math.Round(Analyses.Average(analysis => analysis.Score));

    /// <summary>Texte « 78/100 ».</summary>
    public string AverageLabel => $"{AverageScore}/100";

    /// <summary>Appréciation de la moyenne (« Bon », « À revoir »...).</summary>
    public string GlobalGrade => SlideAnalysis.GradeFor(AverageScore);

    /// <summary>Nombre de diapositives qui ont au moins un défaut.</summary>
    public int SlidesToFixCount => Analyses.Count(analysis => analysis.HasProblems);

    /// <summary>Diapositive la plus faible (celle à corriger en premier).</summary>
    public SlideAnalysis WeakestAnalysis => Analyses
        .OrderBy(analysis => analysis.Score)
        .ThenBy(analysis => analysis.SlideNumber)
        .First();

    /// <summary>Résumé affiché en bas de la fenêtre.</summary>
    public string OverviewText =>
        $"{Analyses.Count} slide(s) analyzed · average score {AverageScore}/100 "
        + $"({GlobalGrade}) · {SlidesToFixCount} to rework. "
        + $"Weakest: no. {WeakestAnalysis.SlideNumber} \"{WeakestAnalysis.SlideTitle}\".";
}
