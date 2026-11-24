using System;
using System.IO;
using System.Text;
using SlideCraft.Models;

namespace SlideCraft.Services;

/// <summary>
/// Écrit les notes du présentateur et les sous-titres bilingues dans un
/// fichier texte (.txt), à côté du .pptx exporté.
///
/// Pourquoi un fichier séparé ? Parce que PowerPoint n'affiche pas les
/// sous-titres SlideCraft et que les notes sont parfois plus pratiques à
/// imprimer qu'à lire à l'écran. Le texte reste lisible dans le Bloc-notes,
/// sans aucune bibliothèque ni connexion réseau.
///
/// Encodage : UTF-8 avec BOM. Le BOM est ce qui permet au Bloc-notes de
/// Windows d'afficher correctement les accents et les caractères comme « é ».
/// </summary>
public static class NotesExporter
{
    /// <summary>Fin ajoutée au nom du .pptx : « deck.pptx » donne « deck-notes.txt ».</summary>
    public const string FileSuffix = "-notes";

    /// <summary>Extension du fichier texte.</summary>
    public const string FileExtension = ".txt";

    /// <summary>
    /// Calcule le chemin du fichier texte compagnon d'un .pptx :
    /// même dossier, même nom de base, suffixe « -notes ».
    /// </summary>
    public static string BuildCompanionPath(string pptxPath)
    {
        if (string.IsNullOrWhiteSpace(pptxPath))
        {
            throw new ArgumentException("The file path is empty.", nameof(pptxPath));
        }

        string folder = Path.GetDirectoryName(pptxPath) ?? string.Empty;
        string name = Path.GetFileNameWithoutExtension(pptxPath) + FileSuffix + FileExtension;

        return folder.Length == 0 ? name : Path.Combine(folder, name);
    }

    /// <summary>
    /// Écrit le fichier texte (créé ou remplacé).
    /// Comme pour les autres enregistrements, on passe par un fichier
    /// temporaire : un disque plein ne laisse pas un texte à moitié écrit.
    /// </summary>
    public static void Export(Presentation presentation, string path)
    {
        ArgumentNullException.ThrowIfNull(presentation);

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("The file path is empty.", nameof(path));
        }

        string temporaryPath = path + ".tmp";

        try
        {
            // writeBom: true -> Bloc-notes reconnaît l'UTF-8 et garde les accents.
            File.WriteAllText(
                temporaryPath,
                BuildText(presentation),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

            File.Move(temporaryPath, path, overwrite: true);
        }
        catch
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }

            throw;
        }
    }

    /// <summary>
    /// Construit tout le texte en mémoire. Séparé de l'écriture pour pouvoir
    /// être vérifié sans toucher au disque.
    /// </summary>
    public static string BuildText(Presentation presentation)
    {
        ArgumentNullException.ThrowIfNull(presentation);

        var text = new StringBuilder();

        // Les retours à la ligne sont explicites : le fichier doit rester
        // identique quel que soit le système qui l'écrit.
        text.Append("SlideCraft - presenter notes and subtitles\r\n");
        text.Append("Presentation: ").Append(Line(presentation.Title)).Append("\r\n");
        text.Append("Slides: ").Append(presentation.Slides.Count).Append("\r\n");

        for (int i = 0; i < presentation.Slides.Count; i++)
        {
            Slide slide = presentation.Slides[i];

            // Une ligne vide avant chaque diapositive. Les sections de la
            // précédente se terminent déjà par une ligne vide : on n'en
            // rajoute pas une seconde.
            if (i == 0)
            {
                text.Append("\r\n");
            }

            text.Append("==============================================================\r\n");
            text.Append("SLIDE ").Append(i + 1).Append('/').Append(presentation.Slides.Count)
                .Append(" - ").Append(Line(slide.Title)).Append("\r\n");
            text.Append("==============================================================\r\n\r\n");

            text.Append("Suggested time: ")
                .Append(Math.Max(0, slide.Notes.TimeLimitSeconds))
                .Append(" seconds\r\n\r\n");

            WriteNotes(text, slide);
            WriteSubtitles(text, slide);
        }

        return text.ToString();
    }

    // ------------------------------------------------------------------
    // Notes du présentateur
    // ------------------------------------------------------------------

    private static void WriteNotes(StringBuilder text, Slide slide)
    {
        PresenterNotes notes = slide.Notes;

        bool hasNotes = !string.IsNullOrWhiteSpace(notes.PersonalNotes)
            || !string.IsNullOrWhiteSpace(notes.CoachTip)
            || !string.IsNullOrWhiteSpace(notes.NarrationText);

        if (!hasNotes)
        {
            text.Append("No presenter notes on this slide.\r\n\r\n");
            return;
        }

        WriteSection(text, "Presenter notes", notes.PersonalNotes);
        WriteSection(text, "Coach tip", notes.CoachTip);
        WriteSection(text, "Narration script", notes.NarrationText);
    }

    /// <summary>
    /// Un titre de section souligné, puis son contenu. Un champ vide est
    /// écrit « (none) » : on voit tout de suite qu'il n'a pas été oublié.
    /// </summary>
    private static void WriteSection(StringBuilder text, string title, string? content)
    {
        text.Append(title).Append("\r\n");
        text.Append(new string('-', title.Length)).Append("\r\n");

        if (string.IsNullOrWhiteSpace(content))
        {
            text.Append("(none)\r\n\r\n");
            return;
        }

        text.Append(Normalize(content).TrimEnd()).Append("\r\n\r\n");
    }

    // ------------------------------------------------------------------
    // Sous-titres bilingues
    // ------------------------------------------------------------------

    private static void WriteSubtitles(StringBuilder text, Slide slide)
    {
        SlideSubtitles? subtitles = slide.Subtitles;

        if (subtitles is null || !subtitles.HasContent)
        {
            text.Append("No subtitles on this slide.\r\n\r\n");
            return;
        }

        string primary = Language(subtitles.PrimaryLanguage, SlideSubtitles.DefaultPrimaryLanguage);
        string secondary = Language(subtitles.SecondaryLanguage, SlideSubtitles.DefaultSecondaryLanguage);

        string title = subtitles.HasSecondary
            ? $"Subtitles ({primary} / {secondary})"
            : $"Subtitles ({primary})";

        text.Append(title).Append("\r\n");
        text.Append(new string('-', title.Length)).Append("\r\n");

        if (subtitles.HasPrimary)
        {
            WriteLanguageLine(text, primary, subtitles.PrimaryText);
        }

        if (subtitles.HasSecondary)
        {
            WriteLanguageLine(text, secondary, subtitles.SecondaryText);
        }

        text.Append("\r\n");
    }

    /// <summary>
    /// Écrit « French: première ligne », puis les lignes suivantes alignées
    /// sous le texte : un sous-titre peut contenir plusieurs lignes.
    /// </summary>
    private static void WriteLanguageLine(StringBuilder text, string language, string? content)
    {
        string label = language + ": ";
        string value = Normalize(content ?? string.Empty);

        string[] lines = value.Split('\n');
        text.Append(label).Append(lines[0].TrimEnd()).Append("\r\n");

        string indent = new string(' ', label.Length);

        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i].TrimEnd();

            if (line.Length > 0)
            {
                text.Append(indent).Append(line).Append("\r\n");
            }
        }
    }

    // ------------------------------------------------------------------
    // Petits outils
    // ------------------------------------------------------------------

    /// <summary>Une seule ligne de titre : les retours à la ligne sont aplatis.</summary>
    private static string Line(string? value)
    {
        return Normalize(value ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
    }

    /// <summary>Uniformise les retours à la ligne (Windows, anciens Mac, Unix).</summary>
    private static string Normalize(string value)
    {
        return value.Replace("\r\n", "\n").Replace('\r', '\n');
    }

    /// <summary>Étiquette de langue nettoyée, avec la valeur par défaut si vide.</summary>
    private static string Language(string? language, string fallback)
    {
        string value = (language ?? string.Empty).Trim();
        return value.Length == 0 ? fallback : value;
    }
}
