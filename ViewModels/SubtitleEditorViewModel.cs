using System;
using SlideCraft.Models;

namespace SlideCraft.ViewModels;

/// <summary>
/// Le ViewModel de la fenêtre « Subtitles ».
///
/// Rien de WPF ici : la saisie, le nettoyage des textes et la construction
/// du <see cref="SlideSubtitles"/> peuvent être testés sans Windows.
///
/// Les deux langues sont écrites à la main par le présentateur. Aucune
/// traduction automatique n'est faite, aucun réseau n'est utilisé.
/// </summary>
public class SubtitleEditorViewModel : ObservableObject
{
    private readonly bool _hadSubtitles;

    private string _primaryLanguage = SlideSubtitles.DefaultPrimaryLanguage;
    private string _secondaryLanguage = SlideSubtitles.DefaultSecondaryLanguage;
    private string _primaryText = string.Empty;
    private string _secondaryText = string.Empty;
    private string _errorMessage = string.Empty;

    public SubtitleEditorViewModel(SlideSubtitles? subtitles, string slideTitle)
    {
        _hadSubtitles = subtitles is not null;
        SlideTitle = string.IsNullOrWhiteSpace(slideTitle) ? "this slide" : slideTitle;

        if (subtitles is null)
        {
            return;   // nouvelle paire de sous-titres, champs vides
        }

        _primaryLanguage = Clean(subtitles.PrimaryLanguage, SlideSubtitles.DefaultPrimaryLanguage);
        _secondaryLanguage = Clean(subtitles.SecondaryLanguage, SlideSubtitles.DefaultSecondaryLanguage);
        _primaryText = subtitles.PrimaryText ?? string.Empty;
        _secondaryText = subtitles.SecondaryText ?? string.Empty;
    }

    /// <summary>Titre de la diapositive concernée, affiché en haut.</summary>
    public string SlideTitle { get; }

    /// <summary>Vrai si la diapositive avait déjà des sous-titres (bouton « Remove »).</summary>
    public bool HasExistingSubtitles => _hadSubtitles;

    /// <summary>Nom de la première langue (« English » par défaut).</summary>
    public string PrimaryLanguage
    {
        get => _primaryLanguage;
        set
        {
            if (SetProperty(ref _primaryLanguage, value ?? string.Empty))
            {
                ErrorMessage = string.Empty;
            }
        }
    }

    /// <summary>Nom de la seconde langue (« French » par défaut).</summary>
    public string SecondaryLanguage
    {
        get => _secondaryLanguage;
        set
        {
            if (SetProperty(ref _secondaryLanguage, value ?? string.Empty))
            {
                ErrorMessage = string.Empty;
            }
        }
    }

    /// <summary>Sous-titre principal, écrit à la main.</summary>
    public string PrimaryText
    {
        get => _primaryText;
        set
        {
            if (SetProperty(ref _primaryText, value ?? string.Empty))
            {
                ErrorMessage = string.Empty;
            }
        }
    }

    /// <summary>Sous-titre dans la seconde langue, écrit à la main.</summary>
    public string SecondaryText
    {
        get => _secondaryText;
        set
        {
            if (SetProperty(ref _secondaryText, value ?? string.Empty))
            {
                ErrorMessage = string.Empty;
            }
        }
    }

    /// <summary>Explication rouge quand les deux zones sont vides.</summary>
    public string ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    /// <summary>
    /// Vrai si au moins une langue est remplie. Une seule ligne suffit :
    /// certains présentateurs ne sous-titrent qu'une partie de leur propos.
    /// </summary>
    public bool HasContent =>
        !string.IsNullOrWhiteSpace(_primaryText) || !string.IsNullOrWhiteSpace(_secondaryText);

    /// <summary>
    /// Vérifie la saisie avant de fermer la fenêtre. Enregistrer deux zones
    /// vides n'a aucun sens : le message invite à écrire ou à supprimer.
    /// </summary>
    public bool Validate()
    {
        if (!HasContent)
        {
            ErrorMessage = "Write at least one subtitle line, or use \"Remove subtitles\".";
            return false;
        }

        ErrorMessage = string.Empty;
        return true;
    }

    /// <summary>
    /// Construit les sous-titres à enregistrer. Les noms de langue vides
    /// reprennent la valeur par défaut ; les textes sont seulement nettoyés
    /// aux extrémités, les retours à la ligne sont conservés.
    /// </summary>
    public SlideSubtitles Build()
    {
        return new SlideSubtitles
        {
            PrimaryLanguage = Clean(_primaryLanguage, SlideSubtitles.DefaultPrimaryLanguage),
            SecondaryLanguage = Clean(_secondaryLanguage, SlideSubtitles.DefaultSecondaryLanguage),
            PrimaryText = CleanText(_primaryText),
            SecondaryText = CleanText(_secondaryText)
        };
    }

    /// <summary>Efface les deux textes mais garde les langues choisies.</summary>
    public void Clear()
    {
        PrimaryText = string.Empty;
        SecondaryText = string.Empty;
        ErrorMessage = string.Empty;
    }

    private static string Clean(string? value, string fallback)
    {
        string text = (value ?? string.Empty).Trim();
        return text.Length == 0 ? fallback : text;
    }

    private static string CleanText(string? value)
    {
        return (value ?? string.Empty).Trim();
    }
}
