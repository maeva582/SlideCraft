using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SlideCraft.Models;

namespace SlideCraft.Services;

/// <summary>
/// SmartSlide : lit une diapositive et lui donne une note de 0 à 100,
/// avec des conseils concrets pour l'améliorer.
///
/// Tout est calculé ICI, sur votre ordinateur : aucune connexion Internet,
/// aucun service extérieur. Les règles sont volontairement simples et
/// lisibles, pour que vous puissiez comprendre chaque note :
///
///   - diapositive vide
///   - trop de texte (plus de 90 mots, puis plus de 150)
///   - paragraphe trop long d'un seul tenant
///   - police trop petite pour une projection
///   - contraste texte / fond insuffisant
///   - pas de vrai titre
///   - éléments qui se chevauchent
///   - élément qui dépasse de la diapositive
///   - trop d'éléments à l'écran
///   - tableau trop dense
///   - aucune note du présentateur (simple conseil, sans perte de points)
///
/// Classe « static » : elle ne garde aucun état entre deux analyses.
/// </summary>
public static class SmartSlideAnalyzer
{
    /// <summary>Taille du repère d'une diapositive (voir SlideElement).</summary>
    public const double SlideWidth = 960;
    public const double SlideHeight = 540;

    /// <summary>Analyse toutes les diapositives d'une présentation.</summary>
    public static IReadOnlyList<SlideAnalysis> Analyze(Presentation presentation)
    {
        var results = new List<SlideAnalysis>();

        if (presentation is null)
        {
            return results;
        }

        for (int index = 0; index < presentation.Slides.Count; index++)
        {
            results.Add(Analyze(presentation.Slides[index], index + 1));
        }

        return results;
    }

    /// <summary>
    /// Analyse une diapositive. <paramref name="slideNumber"/> sert seulement
    /// à l'affichage (1 = première diapositive).
    /// </summary>
    public static SlideAnalysis Analyze(Slide slide, int slideNumber)
    {
        if (slide is null)
        {
            throw new ArgumentNullException(nameof(slide));
        }

        var issues = new List<SlideIssue>();
        List<SlideElement> elements = slide.Elements.ToList();

        // Le texte vide n'est pas projeté : on ne compte que le texte visible.
        List<TextElement> texts = elements
            .OfType<TextElement>()
            .Where(text => !string.IsNullOrWhiteSpace(text.Text))
            .ToList();

        if (elements.Count == 0)
        {
            issues.Add(SlideIssue.Critical(
                "Empty slide",
                "This slide shows nothing. Add a title and a message, "
                + "or delete it so it does not break the rhythm.",
                45));
        }
        else
        {
            CheckTextVolume(texts, issues);
            CheckReadability(slide, texts, issues);
            CheckTitle(texts, issues);
            CheckOverlaps(elements, issues);
            CheckBounds(elements, issues);
            CheckDensity(elements, issues);
        }

        if (string.IsNullOrWhiteSpace(slide.Notes.PersonalNotes))
        {
            issues.Add(SlideIssue.Info(
                "No speaker notes",
                "Write your notes at the bottom of the window: they stay hidden "
                + "from the audience and keep you from reading the slide."));
        }

        int score = Math.Clamp(100 - issues.Sum(issue => issue.Penalty), 0, 100);
        return new SlideAnalysis(slideNumber, slide.Title, score, issues);
    }

    // ------------------------------------------------------------------
    // Les règles, une méthode par famille de défauts
    // ------------------------------------------------------------------

    /// <summary>Trop de texte : on ne lit pas une diapositive, on l'écoute.</summary>
    private static void CheckTextVolume(List<TextElement> texts, List<SlideIssue> issues)
    {
        int totalWords = texts.Sum(text => CountWords(text.Text));

        if (totalWords > 150)
        {
            issues.Add(SlideIssue.Critical(
                "Far too much text",
                $"{totalWords} words on a single slide. Keep one strong idea "
                + "and move the rest into your notes.",
                30));
        }
        else if (totalWords > 90)
        {
            issues.Add(SlideIssue.Warning(
                "Too much text",
                $"{totalWords} words: on screen, an audience reads 60 to 80 at most. "
                + "Cut what is not essential.",
                15));
        }

        TextElement? longest = texts
            .OrderByDescending(text => CountWords(text.Text))
            .FirstOrDefault();

        if (longest is not null && CountWords(longest.Text) > 60)
        {
            issues.Add(SlideIssue.Warning(
                "Paragraph too long",
                $"The box \"{longest.Name}\" holds {CountWords(longest.Text)} words "
                + "in one block: split it into short bullets.",
                10));
        }
    }

    /// <summary>Lisibilité : taille de police et contraste avec le fond.</summary>
    private static void CheckReadability(
        Slide slide,
        List<TextElement> texts,
        List<SlideIssue> issues)
    {
        TextElement? smallest = texts.OrderBy(text => text.FontSize).FirstOrDefault();

        if (smallest is not null && smallest.FontSize < 16)
        {
            issues.Add(SlideIssue.Warning(
                "Text unreadable when projected",
                $"\"{smallest.Name}\" is set at {smallest.FontSize:0} pt. Below 16 pt, "
                + "the back of the room cannot read it: use 20 pt or more.",
                15));
        }

        // On cherche le texte le moins contrasté avec l'arrière-plan.
        TextElement? worst = null;
        double worstRatio = double.MaxValue;

        foreach (TextElement text in texts)
        {
            double ratio = ContrastRatio(text.Foreground, slide.BackgroundColor);

            // NaN = couleur non reconnue (dégradé, nom de couleur...) :
            // on préfère ne rien dire plutôt que d'inventer un défaut.
            if (double.IsNaN(ratio))
            {
                continue;
            }

            if (ratio < worstRatio)
            {
                worstRatio = ratio;
                worst = text;
            }
        }

        if (worst is not null && worstRatio < 3.0)
        {
            issues.Add(SlideIssue.Warning(
                "Insufficient contrast",
                $"\"{worst.Name}\" ({worst.Foreground}) on a "
                + $"{slide.BackgroundColor} background: contrast {worstRatio:0.0}:1, below "
                + "the 3:1 minimum. Darken the text or lighten the background.",
                15));
        }
    }

    /// <summary>Un titre court et lisible structure la diapositive.</summary>
    private static void CheckTitle(List<TextElement> texts, List<SlideIssue> issues)
    {
        if (texts.Count == 0)
        {
            issues.Add(SlideIssue.Warning(
                "No text",
                "The audience has no entry point. Add at least a title, "
                + "even a short one.",
                15));
            return;
        }

        TextElement title = texts.OrderByDescending(text => text.FontSize).First();

        if (title.FontSize < 24)
        {
            issues.Add(SlideIssue.Warning(
                "No real title",
                $"The largest text is {title.FontSize:0} pt. A title should make the "
                + "slide understandable in three seconds: aim for 28 pt or more.",
                10));
        }

        string trimmed = title.Text.Trim();

        if (trimmed.Length > 90)
        {
            issues.Add(SlideIssue.Warning(
                "Title too long",
                $"{trimmed.Length} characters: a title fits on one short line "
                + "(about 60 characters).",
                8));
        }
    }

    /// <summary>Deux éléments qui se superposent cachent souvent du texte.</summary>
    private static void CheckOverlaps(List<SlideElement> elements, List<SlideIssue> issues)
    {
        double slideArea = SlideWidth * SlideHeight;

        // Une forme qui couvre presque toute la diapositive est un fond
        // décoratif : elle chevauche forcément tout, ce n'est pas un défaut.
        List<SlideElement> candidates = elements
            .Where(element => element.Width * element.Height < slideArea * 0.8)
            .ToList();

        int overlaps = 0;

        for (int first = 0; first < candidates.Count; first++)
        {
            for (int second = first + 1; second < candidates.Count; second++)
            {
                if (OverlapRatio(candidates[first], candidates[second]) > 0.25)
                {
                    overlaps++;
                }
            }
        }

        if (overlaps > 0)
        {
            issues.Add(SlideIssue.Warning(
                "Overlapping elements",
                $"{overlaps} pair(s) of elements overlap over more than a quarter "
                + "of their area: text may end up behind a shape.",
                Math.Min(24, overlaps * 8)));
        }
    }

    /// <summary>Un élément qui sort du cadre est coupé à la projection.</summary>
    private static void CheckBounds(List<SlideElement> elements, List<SlideIssue> issues)
    {
        SlideElement? outside = elements.FirstOrDefault(element =>
            element.X < -0.5
            || element.Y < -0.5
            || element.X + element.Width > SlideWidth + 0.5
            || element.Y + element.Height > SlideHeight + 0.5);

        if (outside is not null)
        {
            issues.Add(SlideIssue.Warning(
                "Element outside the slide",
                $"\"{outside.Name}\" sticks out of the frame: the overflowing part "
                + "will be cropped during the slide show.",
                10));
        }
    }

    /// <summary>Surcharge visuelle et tableaux trop denses.</summary>
    private static void CheckDensity(List<SlideElement> elements, List<SlideIssue> issues)
    {
        if (elements.Count > 9)
        {
            issues.Add(SlideIssue.Warning(
                "Visual overload",
                $"{elements.Count} elements on this slide: above 8, "
                + "the audience no longer knows where to look. Split it into two slides.",
                10));
        }

        foreach (TableElement table in elements.OfType<TableElement>())
        {
            if (table.Rows * table.Columns > 15)
            {
                issues.Add(SlideIssue.Warning(
                    "Table too dense",
                    $"Table of {table.Rows} × {table.Columns}: above 4 × 4, "
                    + "prefer a chart or split the table.",
                    8));

                // Un seul avertissement de ce type par diapositive suffit.
                break;
            }
        }
    }

    // ------------------------------------------------------------------
    // Petits calculs réutilisables
    // ------------------------------------------------------------------

    /// <summary>
    /// Nombre de mots d'un texte. Les puces et les tirets ne comptent pas
    /// comme des mots.
    /// </summary>
    public static int CountWords(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        char[] separators = { ' ', '\t', '\r', '\n', '•', '·', '–', '—' };
        return text.Split(separators, StringSplitOptions.RemoveEmptyEntries).Length;
    }

    /// <summary>
    /// Part du plus petit des deux éléments qui est recouverte par l'autre
    /// (0 = aucun chevauchement, 1 = le petit est entièrement caché).
    /// </summary>
    public static double OverlapRatio(SlideElement first, SlideElement second)
    {
        double left = Math.Max(first.X, second.X);
        double top = Math.Max(first.Y, second.Y);
        double right = Math.Min(first.X + first.Width, second.X + second.Width);
        double bottom = Math.Min(first.Y + first.Height, second.Y + second.Height);

        if (right <= left || bottom <= top)
        {
            return 0;
        }

        double intersection = (right - left) * (bottom - top);
        double smallestArea = Math.Min(
            first.Width * first.Height,
            second.Width * second.Height);

        return smallestArea <= 0 ? 0 : intersection / smallestArea;
    }

    /// <summary>
    /// Rapport de contraste entre deux couleurs, selon la formule WCAG
    /// (entre 1:1 pour deux fois la même couleur et 21:1 pour noir sur blanc).
    /// Renvoie NaN si une couleur n'est pas au format « #RRGGBB ».
    /// </summary>
    public static double ContrastRatio(string? foreground, string? background)
    {
        if (!TryParseColor(foreground, out (byte R, byte G, byte B) fg)
            || !TryParseColor(background, out (byte R, byte G, byte B) bg))
        {
            return double.NaN;
        }

        double first = RelativeLuminance(fg);
        double second = RelativeLuminance(bg);
        double lighter = Math.Max(first, second);
        double darker = Math.Min(first, second);

        return (lighter + 0.05) / (darker + 0.05);
    }

    /// <summary>Luminosité relative d'une couleur (0 = noir, 1 = blanc).</summary>
    private static double RelativeLuminance((byte R, byte G, byte B) color)
    {
        return 0.2126 * Channel(color.R)
            + 0.7152 * Channel(color.G)
            + 0.0722 * Channel(color.B);

        static double Channel(byte value)
        {
            double normalized = value / 255.0;
            return normalized <= 0.03928
                ? normalized / 12.92
                : Math.Pow((normalized + 0.055) / 1.055, 2.4);
        }
    }

    /// <summary>
    /// Lit une couleur « #RRGGBB ». On accepte aussi « #RGB » (forme courte)
    /// et « #AARRGGBB » (avec transparence), car les deux existent dans le XAML.
    /// </summary>
    public static bool TryParseColor(string? value, out (byte R, byte G, byte B) color)
    {
        color = default;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string hex = value.Trim().TrimStart('#');

        if (hex.Length == 3)
        {
            hex = string.Concat(hex[0], hex[0], hex[1], hex[1], hex[2], hex[2]);
        }

        if (hex.Length == 8)
        {
            hex = hex.Substring(2);   // on ignore l'opacité
        }

        if (hex.Length != 6)
        {
            return false;
        }

        if (!byte.TryParse(hex.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte red)
            || !byte.TryParse(hex.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte green)
            || !byte.TryParse(hex.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte blue))
        {
            return false;
        }

        color = (red, green, blue);
        return true;
    }
}
