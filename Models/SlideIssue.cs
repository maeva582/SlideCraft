namespace SlideCraft.Models;

/// <summary>
/// Gravité d'un conseil donné par SmartSlide.
/// </summary>
public enum IssueSeverity
{
    /// <summary>Simple conseil : la diapositive reste correcte.</summary>
    Info,

    /// <summary>À améliorer : cela gêne la lecture en projection.</summary>
    Warning,

    /// <summary>À corriger : le public ne comprendra pas la diapositive.</summary>
    Critical
}

/// <summary>
/// Un point relevé par SmartSlide sur UNE diapositive : un titre court,
/// une explication et le nombre de points retirés à la note.
///
/// L'objet ne contient aucune couleur ni aucun contrôle WPF : c'est
/// l'interface qui décide comment l'afficher (rouge, orange, gris...).
/// </summary>
public class SlideIssue
{
    private SlideIssue(IssueSeverity severity, string title, string advice, int penalty)
    {
        Severity = severity;
        Title = title;
        Advice = advice;
        Penalty = penalty;
    }

    /// <summary>Crée un point « à corriger » (il coûte des points).</summary>
    public static SlideIssue Critical(string title, string advice, int penalty = 20)
        => new(IssueSeverity.Critical, title, advice, penalty);

    /// <summary>Crée un point « à améliorer » (il coûte moins de points).</summary>
    public static SlideIssue Warning(string title, string advice, int penalty = 8)
        => new(IssueSeverity.Warning, title, advice, penalty);

    /// <summary>Crée un simple conseil, qui ne retire aucun point.</summary>
    public static SlideIssue Info(string title, string advice)
        => new(IssueSeverity.Info, title, advice, 0);

    public IssueSeverity Severity { get; }

    /// <summary>Titre très court, affiché en gras (« Trop de texte »).</summary>
    public string Title { get; }

    /// <summary>Explication + ce qu'il faut faire, en une ou deux phrases.</summary>
    public string Advice { get; }

    /// <summary>Points retirés à la note de la diapositive (0 pour un conseil).</summary>
    public int Penalty { get; }

    /// <summary>Étiquette lisible de la gravité, affichée au-dessus du titre.</summary>
    public string SeverityLabel => Severity switch
    {
        IssueSeverity.Critical => "TO FIX",
        IssueSeverity.Warning => "TO IMPROVE",
        _ => "TIP"
    };
}
