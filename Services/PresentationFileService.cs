using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Xml;
using System.Xml.Linq;
using SlideCraft.Models;

namespace SlideCraft.Services;

/// <summary>
/// Enregistre et relit une présentation dans un fichier « .scraft ».
///
/// Pourquoi du XML et pas un format binaire ? Parce que le fichier reste
/// lisible dans un éditeur de texte : on voit tout de suite ce qui a été
/// enregistré, ce qui aide beaucoup quand on débute. Aucune bibliothèque
/// externe n'est nécessaire, XML fait partie de .NET.
///
/// Le format de PowerPoint (.pptx) viendra plus tard : c'est un tout autre
/// métier (norme Open XML), et il ne doit pas empêcher la sauvegarde locale.
/// </summary>
public static class PresentationFileService
{
    /// <summary>Extension des présentations SlideCraft.</summary>
    public const string FileExtension = ".scraft";

    /// <summary>Filtre utilisé par les boîtes de dialogue Ouvrir / Enregistrer.</summary>
    public const string FileFilter =
        "SlideCraft presentation (*.scraft)|*.scraft|All files (*.*)|*.*";

    /// <summary>Numéro de version du format, pour pouvoir le faire évoluer.</summary>
    private const string FormatVersion = "1";

    // ------------------------------------------------------------------
    // Enregistrement
    // ------------------------------------------------------------------

    /// <summary>Écrit la présentation dans un fichier (créé ou écrasé).</summary>
    public static void Save(Presentation presentation, string path)
    {
        ArgumentNullException.ThrowIfNull(presentation);

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Le chemin du fichier est vide.", nameof(path));
        }

        // Avant d'écrire le XML, on s'assure que chaque vidéo est bien à côté
        // du projet : le fichier .scraft ne retiendra ensuite qu'un chemin
        // relatif, et le couple (projet + dossier _media) reste déplaçable.
        MediaLibrary.PrepareVideos(presentation, path);

        var root = new XElement("SlideCraft",
            new XAttribute("version", FormatVersion),
            new XAttribute("title", presentation.Title ?? string.Empty),
            presentation.Slides.Select(slide => SaveSlide(slide, path)));

        // On écrit d'abord à côté du fichier final, puis on remplace.
        // Si l'écriture échoue (disque plein, fichier ouvert ailleurs),
        // l'ancienne version de la présentation reste intacte.
        string temporaryPath = path + ".tmp";

        try
        {
            new XDocument(root).Save(temporaryPath);
            File.Move(temporaryPath, path, overwrite: true);
        }
        catch
        {
            TryDelete(temporaryPath);
            throw;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // Un fichier temporaire resté sur le disque n'empêche pas de continuer.
        }
    }

    private static XElement SaveSlide(Slide slide, string projectPath)
    {
        var element = new XElement("Slide",
            new XAttribute("title", slide.Title ?? string.Empty),
            new XAttribute("background", slide.BackgroundColor ?? "#FFFFFF"),
            // La transition est écrite en clair ("Fade", "Push"...) : le
            // fichier reste lisible et un nom inconnu retombe sur "None".
            new XAttribute("transition", TransitionCatalog.Label(slide.Transition)));

        element.Add(new XElement("Notes",
            new XAttribute("timeLimit", slide.Notes.TimeLimitSeconds),
            new XElement("Personal", slide.Notes.PersonalNotes ?? string.Empty),
            new XElement("Coach", slide.Notes.CoachTip ?? string.Empty),
            new XElement("Narration", slide.Notes.NarrationText ?? string.Empty)));

        // Le quiz fait partie du contenu : on l'enregistre avec la diapositive.
        if (slide.Quiz is not null)
        {
            element.Add(SaveQuiz(slide.Quiz));
        }

        // Les sous-titres bilingues aussi : c'est du texte écrit à la main,
        // il doit se retrouver tel quel à la réouverture.
        if (slide.Subtitles is not null)
        {
            element.Add(new XElement("Subtitles",
                new XAttribute("primaryLanguage", slide.Subtitles.PrimaryLanguage ?? string.Empty),
                new XAttribute("secondaryLanguage", slide.Subtitles.SecondaryLanguage ?? string.Empty),
                new XElement("Primary", slide.Subtitles.PrimaryText ?? string.Empty),
                new XElement("Secondary", slide.Subtitles.SecondaryText ?? string.Empty)));
        }

        foreach (SlideElement item in slide.Elements)
        {
            element.Add(SaveElement(item, projectPath));
        }

        return element;
    }

    /// <summary>
    /// Écrit le quiz d'une diapositive :
    /// &lt;Quiz title="..."&gt;&lt;Question correct="0" points="10"&gt;...
    /// </summary>
    private static XElement SaveQuiz(Quiz quiz)
    {
        var element = new XElement("Quiz",
            new XAttribute("title", quiz.Title ?? string.Empty));

        foreach (QuizQuestion question in quiz.Questions)
        {
            var item = new XElement("Question",
                new XAttribute("correct", question.CorrectAnswerIndex),
                new XAttribute("points", question.Points),
                new XElement("Text", question.Text ?? string.Empty));

            foreach (string answer in question.Answers)
            {
                item.Add(new XElement("Answer", answer ?? string.Empty));
            }

            element.Add(item);
        }

        return element;
    }

    private static XElement SaveElement(SlideElement item, string projectPath)
    {
        switch (item)
        {
            case TextElement text:
            {
                XElement element = SaveCommon("Text", item);
                element.Add(
                    new XAttribute("fontSize", XmlConvert.ToString(text.FontSize)),
                    new XAttribute("fontFamily", text.FontFamily ?? "Segoe UI"),
                    new XAttribute("fontWeight", text.FontWeight ?? "Normal"),
                    new XAttribute("foreground", text.Foreground ?? "#1F2937"),
                    new XElement("Body", text.Text ?? string.Empty));
                return element;
            }

            case ShapeElement shape:
            {
                XElement element = SaveCommon("Shape", item);
                element.Add(
                    new XAttribute("fill", shape.Fill ?? "#6366F1"),
                    new XAttribute("stroke", shape.Stroke ?? "#4338CA"),
                    new XAttribute("cornerRadius", XmlConvert.ToString(shape.CornerRadius)));
                return element;
            }

            case ImageElement image:
            {
                XElement element = SaveCommon("Image", item);
                element.Add(new XAttribute("path", image.ImagePath ?? string.Empty));

                // On joint aussi une copie du fichier image : la présentation
                // reste complète même si l'image d'origine est déplacée.
                string? data = ReadImageAsBase64(image.ImagePath);
                if (data is not null)
                {
                    element.Add(new XElement("Data", data));
                }

                return element;
            }

            case VideoElement video:
            {
                XElement element = SaveCommon("Video", item);

                // Seul le chemin est enregistré : une vidéo pèse trop lourd
                // pour être recopiée dans le document XML. Le fichier lui-même
                // a été copié dans le dossier « _media » juste avant, et on
                // écrit ici un chemin relatif à ce dossier.
                element.Add(new XAttribute(
                    "path",
                    MediaLibrary.ToStoredPath(video.VideoPath, projectPath)));
                return element;
            }

            case TableElement table:
            {
                XElement element = SaveCommon("Table", item);
                element.Add(
                    new XAttribute("columns", table.Columns),
                    new XAttribute("rows", table.Rows),
                    new XAttribute("fill", table.Fill ?? "#FFFFFF"),
                    new XAttribute("stroke", table.Stroke ?? "#D1D5DB"),
                    new XAttribute("fontSize", XmlConvert.ToString(table.FontSize)));

                foreach (TableCell cell in table.Cells)
                {
                    element.Add(new XElement("Cell", cell.Text ?? string.Empty));
                }

                return element;
            }

            default:
                // Type inconnu (ajouté plus tard) : on l'ignore à l'enregistrement.
                return SaveCommon("Unknown", item);
        }
    }

    /// <summary>Nom, position et taille : communs à tous les éléments.</summary>
    private static XElement SaveCommon(string tag, SlideElement item)
    {
        return new XElement(tag,
            new XAttribute("name", item.Name ?? string.Empty),
            new XAttribute("x", XmlConvert.ToString(item.X)),
            new XAttribute("y", XmlConvert.ToString(item.Y)),
            new XAttribute("width", XmlConvert.ToString(item.Width)),
            new XAttribute("height", XmlConvert.ToString(item.Height)));
    }

    private static string? ReadImageAsBase64(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        try
        {
            return Convert.ToBase64String(File.ReadAllBytes(path));
        }
        catch (IOException)
        {
            // Image verrouillée ou illisible : on gardera seulement son chemin.
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    // ------------------------------------------------------------------
    // Ouverture
    // ------------------------------------------------------------------

    /// <summary>
    /// Relit une présentation depuis un fichier.
    /// Lève une exception avec un message clair si le fichier est invalide.
    /// </summary>
    public static Presentation Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Le chemin du fichier est vide.", nameof(path));
        }

        XDocument document = XDocument.Load(path);
        XElement? root = document.Root;

        if (root is null || root.Name != "SlideCraft")
        {
            throw new InvalidDataException("This file is not a SlideCraft presentation.");
        }

        string version = (string?)root.Attribute("version") ?? string.Empty;
        if (version != FormatVersion)
        {
            throw new InvalidDataException(
                $"Unrecognised file version ({version}). Please update SlideCraft.");
        }

        var presentation = new Presentation
        {
            Title = (string?)root.Attribute("title") ?? "Untitled presentation"
        };

        foreach (XElement slideElement in root.Elements("Slide"))
        {
            presentation.Slides.Add(LoadSlide(slideElement));
        }

        if (presentation.Slides.Count == 0)
        {
            // Une présentation doit toujours contenir au moins une diapositive.
            presentation.Slides.Add(new Slide { Title = "Slide 1" });
        }

        // Les vidéos sont enregistrées avec un chemin relatif (par exemple
        // "MaPresentation_media/demo.mp4"). On le transforme en chemin absolu
        // pour que le lecteur multimédia retrouve le fichier.
        MediaLibrary.ResolveVideos(presentation, path);

        return presentation;
    }

    private static Slide LoadSlide(XElement element)
    {
        var slide = new Slide
        {
            Title = ReadString(element, "title", "Slide"),
            BackgroundColor = ReadString(element, "background", "#FFFFFF"),
            Transition = TransitionCatalog.Parse(element.Attribute("transition")?.Value)
        };

        XElement? notes = element.Element("Notes");
        if (notes is not null)
        {
            slide.Notes.PersonalNotes = notes.Element("Personal")?.Value ?? string.Empty;
            slide.Notes.CoachTip = notes.Element("Coach")?.Value ?? string.Empty;
            slide.Notes.NarrationText = notes.Element("Narration")?.Value ?? string.Empty;
            slide.Notes.TimeLimitSeconds = ReadInt(notes, "timeLimit", 60);
        }

        XElement? quiz = element.Element("Quiz");
        if (quiz is not null)
        {
            slide.Quiz = LoadQuiz(quiz);
        }

        slide.Subtitles = LoadSubtitles(element.Element("Subtitles"));

        foreach (XElement child in element.Elements())
        {
            SlideElement? item = LoadElement(child);
            if (item is not null)
            {
                slide.Elements.Add(item);
            }
        }

        return slide;
    }

    /// <summary>
    /// Relit le quiz d'une diapositive. Un quiz vide (aucune question
    /// lisible) est ignoré : la diapositive redevient une diapositive simple.
    /// </summary>
    private static Quiz? LoadQuiz(XElement element)
    {
        var quiz = new Quiz { Title = ReadString(element, "title", "Quiz") };

        foreach (XElement item in element.Elements("Question"))
        {
            var question = new QuizQuestion
            {
                Text = item.Element("Text")?.Value ?? string.Empty,
                CorrectAnswerIndex = Math.Max(0, ReadInt(item, "correct", 0)),
                Points = Math.Max(0, ReadInt(item, "points", 10))
            };

            foreach (XElement answer in item.Elements("Answer"))
            {
                question.Answers.Add(answer.Value);
            }

            if (question.Answers.Count >= 2)
            {
                quiz.Questions.Add(question);
            }
        }

        return quiz.Questions.Count == 0 ? null : quiz;
    }

    /// <summary>
    /// Relit les sous-titres d'une diapositive. Un bloc vide (les deux textes
    /// vides) est ignoré : la diapositive reste sans sous-titres.
    /// </summary>
    private static SlideSubtitles? LoadSubtitles(XElement? element)
    {
        if (element is null)
        {
            return null;
        }

        var subtitles = new SlideSubtitles
        {
            PrimaryLanguage = ReadString(element, "primaryLanguage", SlideSubtitles.DefaultPrimaryLanguage),
            SecondaryLanguage = ReadString(element, "secondaryLanguage", SlideSubtitles.DefaultSecondaryLanguage),
            PrimaryText = element.Element("Primary")?.Value ?? string.Empty,
            SecondaryText = element.Element("Secondary")?.Value ?? string.Empty
        };

        // Ancien fichier ou bloc abîmé : rien à afficher, on n'en garde rien.
        return subtitles.HasContent ? subtitles : null;
    }

    private static SlideElement? LoadElement(XElement element)    {
        switch (element.Name.LocalName)
        {
            case "Text":
            {
                var text = new TextElement
                {
                    Text = element.Element("Body")?.Value ?? string.Empty,
                    FontSize = ReadDouble(element, "fontSize", 24),
                    FontFamily = ReadString(element, "fontFamily", "Segoe UI"),
                    FontWeight = ReadString(element, "fontWeight", "Normal"),
                    Foreground = ReadString(element, "foreground", "#1F2937")
                };
                LoadCommon(element, text);
                return text;
            }

            case "Shape":
            {
                var shape = new ShapeElement
                {
                    Fill = ReadString(element, "fill", "#6366F1"),
                    Stroke = ReadString(element, "stroke", "#4338CA"),
                    CornerRadius = ReadDouble(element, "cornerRadius", 0)
                };
                LoadCommon(element, shape);
                return shape;
            }

            case "Image":
            {
                var image = new ImageElement
                {
                    ImagePath = ReadString(element, "path", string.Empty)
                };
                LoadCommon(element, image);

                // Si l'image d'origine n'existe plus, on la récupère depuis
                // la copie enregistrée dans le document.
                string? data = element.Element("Data")?.Value.Trim();
                if (!string.IsNullOrEmpty(data) && !File.Exists(image.ImagePath))
                {
                    image.ImagePath = RestoreImageFile(data, image.ImagePath);
                }

                return image;
            }

            case "Video":
            {
                var video = new VideoElement
                {
                    VideoPath = ReadString(element, "path", string.Empty)
                };
                LoadCommon(element, video);
                return video;
            }

            case "Table":
            {
                var table = new TableElement
                {
                    Columns = Math.Max(1, ReadInt(element, "columns", 2)),
                    Rows = Math.Max(1, ReadInt(element, "rows", 3)),
                    Fill = ReadString(element, "fill", "#FFFFFF"),
                    Stroke = ReadString(element, "stroke", "#D1D5DB"),
                    FontSize = ReadDouble(element, "fontSize", 16)
                };
                LoadCommon(element, table);

                foreach (XElement cell in element.Elements("Cell"))
                {
                    table.AddCell(cell.Value);
                }

                return table;
            }

            default:
                // Élément inconnu (fichier plus récent) : on l'ignore au lieu
                // de refuser toute la présentation.
                return null;
        }
    }

    private static void LoadCommon(XElement element, SlideElement item)
    {
        item.Name = ReadString(element, "name", item.TypeLabel);
        item.X = ReadDouble(element, "x", 60);
        item.Y = ReadDouble(element, "y", 60);
        item.Width = Math.Max(40, ReadDouble(element, "width", 400));
        item.Height = Math.Max(30, ReadDouble(element, "height", 120));
    }

    /// <summary>
    /// Recopie une image incluse dans le fichier vers le dossier cache de
    /// l'application et renvoie son chemin. Le nom du fichier est calculé à
    /// partir du contenu : la même image n'est donc jamais enregistrée deux fois.
    /// </summary>
    private static string RestoreImageFile(string base64, string originalPath)
    {
        byte[] bytes = Convert.FromBase64String(base64);

        string folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SlideCraft",
            "images");

        Directory.CreateDirectory(folder);

        string extension = Path.GetExtension(originalPath);
        if (string.IsNullOrWhiteSpace(extension) || extension.Length > 6)
        {
            extension = ".img";
        }

        string hash = Convert.ToHexString(SHA256.HashData(bytes))[..24];
        string target = Path.Combine(folder, hash + extension.ToLowerInvariant());

        if (!File.Exists(target))
        {
            File.WriteAllBytes(target, bytes);
        }

        return target;
    }

    // ------------------------------------------------------------------
    // Petites lectures tolérantes : une valeur absente ou abîmée ne doit
    // jamais faire échouer toute l'ouverture du document.
    // ------------------------------------------------------------------

    private static string ReadString(XElement element, string name, string fallback)
    {
        return (string?)element.Attribute(name) ?? fallback;
    }

    private static double ReadDouble(XElement element, string name, double fallback)
    {
        string? raw = (string?)element.Attribute(name);

        if (raw is null)
        {
            return fallback;
        }

        try
        {
            // XmlConvert lit les nombres avec un point décimal, quelle que
            // soit la langue de Windows (une virgule donnerait 12,5 -> 125).
            return XmlConvert.ToDouble(raw);
        }
        catch (FormatException)
        {
            return fallback;
        }
        catch (OverflowException)
        {
            return fallback;
        }
    }

    private static int ReadInt(XElement element, string name, int fallback)
    {
        string? raw = (string?)element.Attribute(name);

        if (raw is null)
        {
            return fallback;
        }

        try
        {
            return XmlConvert.ToInt32(raw);
        }
        catch (FormatException)
        {
            return fallback;
        }
        catch (OverflowException)
        {
            return fallback;
        }
    }
}
