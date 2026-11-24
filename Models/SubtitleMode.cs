namespace SlideCraft.Models;

/// <summary>
/// Ce que le diaporama montre à l'écran pour les sous-titres.
///
/// Le présentateur écrit lui-même les deux langues (aucune traduction
/// automatique) : il choisit donc simplement laquelle afficher.
/// La touche T fait défiler ces quatre modes.
/// </summary>
public enum SubtitleMode
{
    /// <summary>Les deux langues, l'une sous l'autre.</summary>
    Both,

    /// <summary>Seule la langue principale.</summary>
    PrimaryOnly,

    /// <summary>Seule la langue secondaire.</summary>
    SecondaryOnly,

    /// <summary>Aucun sous-titre à l'écran.</summary>
    Hidden
}
