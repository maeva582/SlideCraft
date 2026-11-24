using System;

namespace SlideCraft.Models;

/// <summary>
/// Les sous-titres bilingues d'UNE diapositive.
///
/// Le texte est écrit à la main par le présentateur : SlideCraft ne traduit
/// rien et n'appelle aucun service en ligne. La langue n'est qu'une
/// étiquette (« English », « French »...) affichée devant la ligne, pour
/// que le public sache tout de suite quelle langue il lit.
///
/// Deux lignes suffisent : la langue principale (celle de la présentation)
/// et la langue secondaire. Un objet null sur <see cref="Slide.Subtitles"/>
/// signifie « cette diapositive n'a pas de sous-titres ».
/// </summary>
public class SlideSubtitles
{
    /// <summary>Langue proposée par défaut (modifiable).</summary>
    public const string DefaultPrimaryLanguage = "English";

    /// <summary>Seconde langue proposée par défaut (modifiable).</summary>
    public const string DefaultSecondaryLanguage = "French";

    /// <summary>Étiquette de la première ligne.</summary>
    public string PrimaryLanguage { get; set; } = DefaultPrimaryLanguage;

    /// <summary>Étiquette de la seconde ligne.</summary>
    public string SecondaryLanguage { get; set; } = DefaultSecondaryLanguage;

    /// <summary>Texte de la première ligne (plusieurs lignes autorisées).</summary>
    public string PrimaryText { get; set; } = string.Empty;

    /// <summary>Texte de la seconde ligne.</summary>
    public string SecondaryText { get; set; } = string.Empty;

    /// <summary>Vrai si la première ligne contient quelque chose à afficher.</summary>
    public bool HasPrimary => !string.IsNullOrWhiteSpace(PrimaryText);

    /// <summary>Vrai si la seconde ligne contient quelque chose à afficher.</summary>
    public bool HasSecondary => !string.IsNullOrWhiteSpace(SecondaryText);

    /// <summary>Vrai si au moins une des deux langues est remplie.</summary>
    public bool HasContent => HasPrimary || HasSecondary;

    /// <summary>
    /// Vrai si les deux sous-titres ont exactement le même contenu.
    /// Comme pour le quiz, cela évite de marquer le document « modifié »
    /// quand on ouvre la fenêtre puis qu'on la referme sans rien changer.
    /// </summary>
    public bool SameContentAs(SlideSubtitles? other)
    {
        return other is not null
            && string.Equals(Normalize(PrimaryLanguage), Normalize(other.PrimaryLanguage), StringComparison.Ordinal)
            && string.Equals(Normalize(SecondaryLanguage), Normalize(other.SecondaryLanguage), StringComparison.Ordinal)
            && PrimaryText == other.PrimaryText
            && SecondaryText == other.SecondaryText;
    }

    /// <summary>
    /// Copie indépendante (duplication d'une diapositive, instantané
    /// d'annulation). Modifier la copie ne touche jamais l'original.
    /// </summary>
    public SlideSubtitles Clone()
    {
        return new SlideSubtitles
        {
            PrimaryLanguage = PrimaryLanguage,
            SecondaryLanguage = SecondaryLanguage,
            PrimaryText = PrimaryText,
            SecondaryText = SecondaryText
        };
    }

    /// <summary>Efface les deux textes, en gardant les langues choisies.</summary>
    public void Clear()
    {
        PrimaryText = string.Empty;
        SecondaryText = string.Empty;
    }

    /// <summary>Étiquette nettoyée, avec la valeur par défaut si elle est vide.</summary>
    private static string Normalize(string? language)
    {
        string text = (language ?? string.Empty).Trim();
        return text.Length == 0 ? DefaultPrimaryLanguage : text;
    }
}
