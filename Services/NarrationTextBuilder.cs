using System;
using System.Collections.Generic;
using System.Linq;
using SlideCraft.Models;

namespace SlideCraft.Services;

/// <summary>
/// Prépare le texte que la voix lit pour une diapositive.
///
/// Deux cas :
///   1. L'utilisateur a écrit un script de narration → on le lit tel quel.
///   2. Il n'a rien écrit → on lit automatiquement le titre et les textes
///      affichés sur la diapositive (les puces et les retours à la ligne
///      sont transformés en phrases).
///
/// Cette classe ne dépend ni de WPF ni de Windows : elle est donc testable
/// sur n'importe quelle machine, et la voix n'est qu'un « haut-parleur »
/// branché à la fin.
/// </summary>
public static class NarrationTextBuilder
{
    /// <summary>Caractères de puce que l'on ne veut pas entendre dire.</summary>
    private static readonly char[] BulletChars = { '•', '-', '*', '·', '–', '—' };

    /// <summary>
    /// Texte à prononcer pour <paramref name="slide"/>.
    /// Renvoie une chaîne vide si la diapositive n'a rien à dire.
    /// </summary>
    public static string Build(Slide slide)
    {
        if (slide is null)
        {
            throw new ArgumentNullException(nameof(slide));
        }

        string script = Clean(slide.Notes.NarrationText);
        if (script.Length > 0)
        {
            return EnsureSentenceEnd(script);
        }

        return BuildFromContent(slide);
    }

    /// <summary>
    /// Texte lu quand aucun script n'est écrit : titre puis blocs de texte
    /// et cellules de tableau, dans l'ordre de la diapositive.
    /// </summary>
    public static string BuildFromContent(Slide slide)
    {
        if (slide is null)
        {
            throw new ArgumentNullException(nameof(slide));
        }

        var parts = new List<string>();

        foreach (SlideElement element in slide.Elements)
        {
            string text = ElementText(element);
            if (text.Length > 0)
            {
                parts.Add(text);
            }
        }

        // Le titre interne est un texte d'organisation (« About the software ») :
        // il est utile à l'oral, mais on évite de le répéter s'il est déjà
        // écrit en gros sur la diapositive.
        string title = Clean(slide.Title);
        if (title.Length > 0
            && !string.Equals(title, "Untitled slide", StringComparison.OrdinalIgnoreCase)
            && !parts.Any(part => part.Contains(title, StringComparison.OrdinalIgnoreCase)))
        {
            parts.Insert(0, title);
        }

        if (parts.Count == 0)
        {
            return string.Empty;
        }

        return EnsureSentenceEnd(string.Join(" ", parts.Select(EnsureSentenceEnd)));
    }

    /// <summary>
    /// Nettoie un texte pour la lecture : supprime les puces, transforme les
    /// retours à la ligne en virgules et réduit les espaces multiples.
    /// Exemple : "• Un\n• Deux" devient "Un, Deux".
    /// </summary>
    public static string Clean(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        string unified = raw!.Replace("\r\n", "\n").Replace('\r', '\n');

        IEnumerable<string> lines = unified
            .Split('\n')
            .Select(line => line.Trim().TrimStart(BulletChars).Trim())
            .Where(line => line.Length > 0);

        string joined = string.Join(", ", lines);

        while (joined.Contains("  ", StringComparison.Ordinal))
        {
            joined = joined.Replace("  ", " ", StringComparison.Ordinal);
        }

        return joined.Trim();
    }

    /// <summary>Ajoute un point final si le texte n'a pas déjà une ponctuation forte.</summary>
    public static string EnsureSentenceEnd(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        string trimmed = text.TrimEnd();
        char last = trimmed[^1];

        return last is '.' or '!' or '?' or ':' or ';' ? trimmed : trimmed + ".";
    }

    /// <summary>Texte prononçable d'un élément (vide pour une image ou une forme).</summary>
    private static string ElementText(SlideElement element)
    {
        switch (element)
        {
            case TextElement text:
                return Clean(text.Text);

            case TableElement table:
                return string.Join(", ",
                    table.Cells
                         .Select(cell => Clean(cell.Text))
                         .Where(cell => cell.Length > 0));

            default:
                return string.Empty;
        }
    }
}
