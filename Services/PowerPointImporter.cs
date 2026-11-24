using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using SlideCraft.Models;
using A = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;
using P14 = DocumentFormat.OpenXml.Office2010.PowerPoint;

namespace SlideCraft.Services;

/// <summary>
/// Ouvre un fichier PowerPoint (.pptx) et le transforme en présentation
/// SlideCraft.
///
/// Ce qui est repris :
///   • les diapositives, dans le bon ordre, avec la couleur de fond ;
///   • les zones de texte (contenu, taille approximative, gras, couleur) ;
///   • les images (recopiées dans le dossier de travail de l'application) ;
///   • les vidéos insérées (recopiées puis relues par le lecteur multimédia) ;
///   • les tableaux ;
///   • les notes du présentateur.
///
/// Ce qui est volontairement ignoré : les animations, les graphiques et les
/// SmartArt. SlideCraft affiche alors la diapositive sans ces effets, au lieu
/// de refuser le fichier.
///
/// Un .pptx est une archive ZIP de XML. Le SDK Open XML fait la lecture du
/// format ; ce fichier se contente de traduire ses objets vers nos modèles.
/// </summary>
public static class PowerPointImporter
{
    /// <summary>Extension des fichiers acceptés.</summary>
    public const string FileExtension = ".pptx";

    /// <summary>Filtre des boîtes de dialogue Ouvrir / Importer.</summary>
    public const string FileFilter =
        "PowerPoint presentation (*.pptx)|*.pptx|All files (*.*)|*.*";

    /// <summary>
    /// 1 unité SlideCraft = 1 point = 12 700 EMU (même conversion que
    /// l'export, pour qu'un aller-retour garde les mêmes proportions).
    /// </summary>
    private const double EmuPerUnit = 12700.0;

    private const double SlideWidth = 960;
    private const double SlideHeight = 540;

    /// <summary>
    /// Lit <paramref name="path"/> et renvoie une présentation prête à être
    /// affichée. Lève une exception avec un message clair si le fichier n'est
    /// pas un .pptx lisible.
    /// </summary>
    public static Presentation Import(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("The file path is empty.", nameof(path));
        }

        if (!File.Exists(path))
        {
            throw new FileNotFoundException("This PowerPoint file does not exist.", path);
        }

        PresentationDocument document;

        try
        {
            document = PresentationDocument.Open(path, false);
        }
        catch (Exception exception) when (
            exception is OpenXmlPackageException
                or InvalidDataException
                or IOException
                or UnauthorizedAccessException)
        {
            throw new InvalidDataException(
                "This file is not a valid PowerPoint presentation (.pptx).",
                exception);
        }

        using (document)
        {
            PresentationPart? presentationPart = document.PresentationPart;

            if (presentationPart?.Presentation is null)
            {
                throw new InvalidDataException(
                    "This file is not a valid PowerPoint presentation (.pptx).");
            }

            // La taille de page varie d'un fichier à l'autre : on calcule un
            // facteur pour toujours remplir notre repère de 960 x 540.
            double pageWidth = ReadEmu(presentationPart.Presentation.SlideSize?.Cx, 12192000);
            double pageHeight = ReadEmu(presentationPart.Presentation.SlideSize?.Cy, 6858000);

            double scaleX = SlideWidth / (pageWidth / EmuPerUnit);
            double scaleY = SlideHeight / (pageHeight / EmuPerUnit);

            var presentation = new Presentation
            {
                Title = Path.GetFileNameWithoutExtension(path)
            };

            int number = 1;

            foreach (P.SlideId? slideId in presentationPart.Presentation
                         .SlideIdList?.Elements<P.SlideId>() ?? Enumerable.Empty<P.SlideId>())
            {
                if (slideId?.RelationshipId?.Value is not { } relationshipId)
                {
                    continue;
                }

                if (presentationPart.GetPartById(relationshipId) is not SlidePart slidePart)
                {
                    continue;
                }

                Slide slide = ReadSlide(slidePart, number, scaleX, scaleY);
                presentation.Slides.Add(slide);
                number++;
            }

            if (presentation.Slides.Count == 0)
            {
                throw new InvalidDataException(
                    "This PowerPoint file contains no slide.");
            }

            return presentation;
        }
    }

    // ------------------------------------------------------------------
    // Une diapositive
    // ------------------------------------------------------------------

    private static Slide ReadSlide(SlidePart slidePart, int number, double scaleX, double scaleY)
    {
        var slide = new Slide
        {
            Title = $"Slide {number}",
            BackgroundColor = ReadBackgroundColor(slidePart),
            // PowerPoint enregistre aussi les transitions : on les reprend
            // quand SlideCraft sait les rejouer (fondu, glissement, balayage).
            Transition = ReadTransition(slidePart)
        };

        P.ShapeTree? tree = slidePart.Slide?.CommonSlideData?.ShapeTree;

        if (tree is not null)
        {
            // Position de départ : le coin haut-gauche de la diapositive.
            var map = new Placement(0, 0, scaleX, scaleY);
            AddShapes(tree.ChildElements, slidePart, slide, map);
        }

        // Titre interne, dans cet ordre de préférence :
        //   1. la zone de titre écrite dans PowerPoint (placeholder « title ») ;
        //   2. le premier texte de la diapositive ;
        //   3. « Slide N ».
        // Le titre sert à retrouver la diapositive dans les propriétés et la
        // liste des miniatures ; le premier texte est souvent une puce, d'où
        // la priorité donnée au vrai titre.
        string? titleText = ReadTitlePlaceholder(tree);

        if (string.IsNullOrWhiteSpace(titleText))
        {
            titleText = slide.Elements
                .OfType<TextElement>()
                .Select(element => element.Text)
                .FirstOrDefault(text => !string.IsNullOrWhiteSpace(text));
        }

        slide.Title = Shorten(FirstNonEmptyLine(titleText), 60, $"Slide {number}");

        ReadNotes(slidePart, slide);
        return slide;
    }

    /// <summary>
    /// Texte de la zone de titre de la diapositive, ou null quand la
    /// diapositive n'en a pas (mise en page sans titre).
    /// </summary>
    private static string? ReadTitlePlaceholder(P.ShapeTree? tree)
    {
        if (tree is null)
        {
            return null;
        }

        foreach (P.Shape shape in tree.Elements<P.Shape>())
        {
            string? placeholder = ReadPlaceholderType(shape);

            bool isTitle = string.Equals(placeholder, "title", StringComparison.OrdinalIgnoreCase)
                || string.Equals(placeholder, "ctrTitle", StringComparison.OrdinalIgnoreCase);

            if (!isTitle)
            {
                continue;
            }

            string text = ReadTextBody(shape.TextBody);

            if (text.Length > 0)
            {
                return text;
            }
        }

        return null;
    }

    /// <summary>Première ligne non vide d'un texte, ou une chaîne vide.</summary>
    private static string FirstNonEmptyLine(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        return text
            .Replace("\r\n", "\n")
            .Split('\n')
            .Select(line => line.Trim())
            .FirstOrDefault(line => line.Length > 0) ?? string.Empty;
    }

    /// <summary>
    /// Raccourcit un titre trop long. Un titre de 200 caractères rendrait la
    /// liste des miniatures illisible.
    /// </summary>
    private static string Shorten(string text, int maxLength, string fallback)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return fallback;
        }

        return text.Length > maxLength ? text[..maxLength] : text;
    }

    /// <summary>
    /// Transition de la diapositive, lue sur &lt;p:sld&gt;. PowerPoint en
    /// connaît beaucoup ; seules celles que le diaporama sait jouer sont
    /// reprises, les autres retombent sur « aucune transition ».
    /// </summary>
    private static TransitionKind ReadTransition(SlidePart slidePart)
    {
        P.Transition? transition = slidePart.Slide?.GetFirstChild<P.Transition>();

        if (transition is null)
        {
            return TransitionKind.None;
        }

        if (transition.GetFirstChild<P.FadeTransition>() is not null)
        {
            return TransitionKind.Fade;
        }

        if (transition.GetFirstChild<P.PushTransition>() is not null)
        {
            return TransitionKind.Push;
        }

        if (transition.GetFirstChild<P.WipeTransition>() is not null)
        {
            return TransitionKind.Wipe;
        }

        return TransitionKind.None;
    }

    /// <summary>Couleur de fond de la diapositive, blanc si elle n'est pas définie.</summary>
    private static string ReadBackgroundColor(SlidePart slidePart)
    {
        A.SolidFill? fill = slidePart.Slide?.CommonSlideData?
            .Background?.BackgroundProperties?.GetFirstChild<A.SolidFill>();

        string? hex = fill?.RgbColorModelHex?.Val?.Value;

        return string.IsNullOrWhiteSpace(hex) ? "#FFFFFF" : "#" + hex.ToUpperInvariant();
    }

    /// <summary>Notes du présentateur écrites dans PowerPoint.</summary>
    private static void ReadNotes(SlidePart slidePart, Slide slide)
    {
        P.ShapeTree? tree = slidePart.NotesSlidePart?.NotesSlide?.CommonSlideData?.ShapeTree;

        if (tree is null)
        {
            return;
        }

        foreach (P.Shape shape in tree.Elements<P.Shape>())
        {
            // Le type d'emplacement est écrit tel quel dans le XML
            // ("body" pour la zone des notes). On compare le texte brut
            // plutôt qu'une énumération, qui varie selon les versions du SDK.
            string? placeholder = ReadPlaceholderType(shape);

            if (!string.Equals(placeholder, "body", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string text = ReadTextBody(shape.TextBody);

            if (text.Length > 0)
            {
                slide.Notes.PersonalNotes = text;
            }
        }
    }

    // ------------------------------------------------------------------
    // Formes, images, tableaux, groupes
    // ------------------------------------------------------------------

    /// <summary>
    /// Position d'un élément dans le repère de la diapositive. Les groupes
    /// PowerPoint ont leur propre repère : il faut le convertir à chaque niveau.
    /// </summary>
    private readonly record struct Placement(double OffsetX, double OffsetY, double ScaleX, double ScaleY)
    {
        public double X(double emu) => OffsetX + emu / EmuPerUnit * ScaleX;

        public double Y(double emu) => OffsetY + emu / EmuPerUnit * ScaleY;
    }

    private static void AddShapes(
        IEnumerable<OpenXmlElement> children,
        SlidePart slidePart,
        Slide slide,
        Placement map)
    {
        foreach (OpenXmlElement child in children)
        {
            switch (child)
            {
                case P.Shape shape:
                    AddTextShape(shape, slide, map);
                    break;

                case P.Picture picture:
                    // Dans PowerPoint, une vidéo EST une image (p:pic) qui pointe
                    // vers un fichier média. On la traite en premier pour ne pas
                    // importer deux fois la même forme (vidéo + affiche).
                    if (!AddVideo(picture, slidePart, slide, map))
                    {
                        AddPicture(picture, slidePart, slide, map);
                    }
                    break;

                case P.GraphicFrame frame:
                    AddTable(frame, slide, map);
                    break;

                case P.GroupShape group:
                    AddGroup(group, slidePart, slide, map);
                    break;
            }
        }
    }

    private static void AddTextShape(P.Shape shape, Slide slide, Placement map)
    {
        string text = ReadTextBody(shape.TextBody);

        // Une forme sans texte (rectangle décoratif) n'a rien à montrer :
        // SlideCraft ne gère pas encore les formes pleines de PowerPoint.
        if (text.Length == 0)
        {
            return;
        }

        var (x, y, width, height) = ReadGeometry(shape.ShapeProperties?.Transform2D, map);
        A.RunProperties? run = FirstRunProperties(shape.TextBody);

        var element = new TextElement
        {
            Name = shape.NonVisualShapeProperties?
                .NonVisualDrawingProperties?.Name?.Value ?? "Text",
            X = x,
            Y = y,
            Width = width,
            Height = height,
            Text = text,
            FontSize = ReadFontSize(run),
            FontWeight = run?.Bold?.Value == true ? "Bold" : "Normal",
            Foreground = ReadColor(run) ?? "#1F2937",
            FontFamily = run?.GetFirstChild<A.LatinFont>()?.Typeface?.Value ?? "Segoe UI"
        };

        slide.Elements.Add(element);
    }

    private static void AddPicture(P.Picture picture, SlidePart slidePart, Slide slide, Placement map)
    {
        string? relationshipId = picture.BlipFill?.Blip?.Embed?.Value;

        if (relationshipId is null
            || slidePart.GetPartById(relationshipId) is not ImagePart imagePart)
        {
            return;
        }

        string? path = SaveImagePart(imagePart);

        if (path is null)
        {
            return;
        }

        var (x, y, width, height) = ReadGeometry(picture.ShapeProperties?.Transform2D, map);

        slide.Elements.Add(new ImageElement
        {
            Name = picture.NonVisualPictureProperties?
                .NonVisualDrawingProperties?.Name?.Value ?? "Picture",
            ImagePath = path,
            X = x,
            Y = y,
            Width = width,
            Height = height
        });
    }

    /// <summary>
    /// Importe une forme qui est en réalité une vidéo. Renvoie true quand
    /// c'était bien une vidéo (et qu'elle a pu être récupérée).
    /// </summary>
    private static bool AddVideo(P.Picture picture, SlidePart slidePart, Slide slide, Placement map)
    {
        P.ApplicationNonVisualDrawingProperties? proprietes =
            picture.NonVisualPictureProperties?.ApplicationNonVisualDrawingProperties;

        if (proprietes is null)
        {
            return false;
        }

        // Deux déclarations possibles selon la version de PowerPoint :
        // a:videoFile (relation « video ») et p14:media (relation « media »).
        string? lien = proprietes.GetFirstChild<A.VideoFromFile>()?.Link?.Value;
        string? embarque = proprietes
            .GetFirstChild<P.ApplicationNonVisualDrawingPropertiesExtensionList>()?
            .Descendants<P14.Media>()
            .FirstOrDefault()?.Embed?.Value;

        MediaDataPart? media = FindMediaPart(slidePart, lien)
            ?? FindMediaPart(slidePart, embarque);

        if (media is null)
        {
            return false;
        }

        string? path = SaveVideoPart(media);

        if (path is null)
        {
            return false;
        }

        var (x, y, width, height) = ReadGeometry(picture.ShapeProperties?.Transform2D, map);

        slide.Elements.Add(new VideoElement
        {
            Name = picture.NonVisualPictureProperties?
                .NonVisualDrawingProperties?.Name?.Value ?? "Video",
            VideoPath = path,
            X = x,
            Y = y,
            Width = width,
            Height = height
        });

        return true;
    }

    /// <summary>Part média derrière un identifiant de relation, ou null.</summary>
    private static MediaDataPart? FindMediaPart(SlidePart slidePart, string? relationshipId)
    {
        if (string.IsNullOrWhiteSpace(relationshipId))
        {
            return null;
        }

        try
        {
            return slidePart.GetReferenceRelationship(relationshipId)
                is DataPartReferenceRelationship reference
                    ? reference.DataPart as MediaDataPart
                    : null;
        }
        catch (Exception exception) when (
            exception is KeyNotFoundException
                or ArgumentException
                or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>
    /// Recopie la vidéo hors du .pptx, dans le dossier de travail de
    /// l'application (comme les images importées). Elle sera ensuite copiée
    /// à côté du projet au premier enregistrement en .scraft.
    /// </summary>
    private static string? SaveVideoPart(MediaDataPart mediaPart)
    {
        string extension = mediaPart.ContentType switch
        {
            "video/mp4" => ".mp4",
            "video/x-ms-wmv" => ".wmv",
            "video/avi" or "video/x-msvideo" => ".avi",
            "video/quicktime" => ".mov",
            "video/mpeg" => ".mpg",
            "video/webm" => ".webm",
            "audio/mpeg" => ".mp3",
            _ => string.Empty
        };

        if (extension.Length == 0)
        {
            // Format média inconnu : on l'ignore sans faire échouer l'import.
            return null;
        }

        try
        {
            using Stream stream = mediaPart.GetStream();
            using var memory = new MemoryStream();
            stream.CopyTo(memory);

            byte[] bytes = memory.ToArray();
            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SlideCraft",
                "imported");

            Directory.CreateDirectory(folder);

            string name = Convert.ToHexString(SHA256.HashData(bytes))[..24]
                + extension;
            string target = Path.Combine(folder, name.ToLowerInvariant());

            if (!File.Exists(target))
            {
                File.WriteAllBytes(target, bytes);
            }

            return target;
        }
        catch (Exception exception) when (
            exception is IOException
                or UnauthorizedAccessException
                or OpenXmlPackageException)
        {
            // Une vidéo illisible est ignorée : importer le reste vaut mieux
            // que refuser toute la présentation.
            return null;
        }
    }

    private static void AddTable(P.GraphicFrame frame, Slide slide, Placement map)
    {
        A.Table? source = frame.Graphic?.GraphicData?.Descendants<A.Table>().FirstOrDefault();

        if (source is null)
        {
            // Graphique, SmartArt ou autre objet : ignoré sans faire échouer
            // l'import, la diapositive reste lisible.
            return;
        }

        var rows = source.Elements<A.TableRow>().ToList();
        int rowCount = rows.Count;
        int columnCount = rows.Count == 0
            ? 0
            : rows.Max(row => row.Elements<A.TableCell>().Count());

        if (rowCount == 0 || columnCount == 0)
        {
            return;
        }

        var (x, y, width, height) = ReadTransform(frame.Transform, map);

        var table = new TableElement
        {
            Name = frame.NonVisualGraphicFrameProperties?
                .NonVisualDrawingProperties?.Name?.Value ?? "Table",
            X = x,
            Y = y,
            Width = width,
            Height = height,
            Columns = columnCount,
            Rows = rowCount,
            FontSize = 14
        };

        foreach (A.TableRow row in rows)
        {
            List<A.TableCell> cells = row.Elements<A.TableCell>().ToList();

            for (int column = 0; column < columnCount; column++)
            {
                string text = column < cells.Count
                    ? ReadTextBody(cells[column].TextBody)
                    : string.Empty;

                table.AddCell(text);
            }
        }

        slide.Elements.Add(table);
    }

    /// <summary>
    /// Un groupe PowerPoint contient ses propres coordonnées : on les convertit
    /// vers celles de la diapositive avant de traiter les enfants.
    /// </summary>
    private static void AddGroup(
        P.GroupShape group,
        SlidePart slidePart,
        Slide slide,
        Placement map)
    {
        A.TransformGroup? transform = group.GroupShapeProperties?.TransformGroup;

        double offsetX = map.X(transform?.Offset?.X?.Value ?? 0);
        double offsetY = map.Y(transform?.Offset?.Y?.Value ?? 0);

        double widthEmu = transform?.Extents?.Cx?.Value ?? 0;
        double heightEmu = transform?.Extents?.Cy?.Value ?? 0;

        double childWidthEmu = transform?.ChildOffset is null || transform.ChildExtents is null
            ? widthEmu
            : Math.Max(1, transform.ChildExtents.Cx?.Value ?? widthEmu);

        double childHeightEmu = transform?.ChildOffset is null || transform.ChildExtents is null
            ? heightEmu
            : Math.Max(1, transform.ChildExtents.Cy?.Value ?? heightEmu);

        // Un groupe mal formé (taille 0, fichier abîmé, forme vide) ferait
        // une division par zéro : toutes les positions deviendraient NaN et
        // le groupe disparaîtrait. On garde alors l'échelle de la diapositive.
        double scaleX = widthEmu > 0 && childWidthEmu > 0
            ? map.ScaleX * (widthEmu / childWidthEmu)
            : map.ScaleX;

        double scaleY = heightEmu > 0 && childHeightEmu > 0
            ? map.ScaleY * (heightEmu / childHeightEmu)
            : map.ScaleY;

        var childMap = new Placement(offsetX, offsetY, scaleX, scaleY);

        // Les enfants d'un groupe sont exprimés dans le repère « child » :
        // on retire son origine avant d'appliquer la conversion.
        foreach (OpenXmlElement child in group.ChildElements)
        {
            switch (child)
            {
                case P.Shape shape:
                    AddTextShape(shape, slide, OffsetPlacement(childMap, transform));
                    break;

                case P.Picture picture:
                    AddPicture(picture, slidePart, slide, OffsetPlacement(childMap, transform));
                    break;

                case P.GraphicFrame frame:
                    AddTable(frame, slide, OffsetPlacement(childMap, transform));
                    break;

                case P.GroupShape inner:
                    AddGroup(inner, slidePart, slide, OffsetPlacement(childMap, transform));
                    break;
            }
        }
    }

    /// <summary>
    /// Retire l'origine du repère enfant : les positions d'un groupe sont
    /// relatives à <c>chOff</c>, pas au coin de la diapositive.
    /// </summary>
    private static Placement OffsetPlacement(Placement placement, A.TransformGroup? transform)
    {
        double childOffsetX = transform?.ChildOffset?.X?.Value ?? 0;
        double childOffsetY = transform?.ChildOffset?.Y?.Value ?? 0;

        return new Placement(
            placement.OffsetX - childOffsetX / EmuPerUnit * placement.ScaleX,
            placement.OffsetY - childOffsetY / EmuPerUnit * placement.ScaleY,
            placement.ScaleX,
            placement.ScaleY);
    }

    // ------------------------------------------------------------------
    // Conversions et petites lectures tolérantes
    // ------------------------------------------------------------------

    private static (double X, double Y, double Width, double Height) ReadGeometry(
        A.Transform2D? transform,
        Placement map)
    {
        if (transform is null)
        {
            return (60, 60, 400, 100);
        }

        double x = map.X(transform.Offset?.X?.Value ?? 0);
        double y = map.Y(transform.Offset?.Y?.Value ?? 0);

        // La taille d'origine est conservée même si elle dépasse : l'utilisateur
        // peut toujours la réduire avec la poignée de redimensionnement.
        double width = Math.Max(20, (transform.Extents?.Cx?.Value ?? 400 * EmuPerUnit) / EmuPerUnit * map.ScaleX);
        double height = Math.Max(20, (transform.Extents?.Cy?.Value ?? 100 * EmuPerUnit) / EmuPerUnit * map.ScaleY);

        return (Math.Round(x, 1), Math.Round(y, 1), Math.Round(width, 1), Math.Round(height, 1));
    }

    private static (double X, double Y, double Width, double Height) ReadTransform(
        P.Transform? transform,
        Placement map)
    {
        if (transform is null)
        {
            return (60, 60, 700, 240);
        }

        double x = map.X(transform.Offset?.X?.Value ?? 0);
        double y = map.Y(transform.Offset?.Y?.Value ?? 0);
        double width = Math.Max(40, (transform.Extents?.Cx?.Value ?? 700 * EmuPerUnit) / EmuPerUnit * map.ScaleX);
        double height = Math.Max(30, (transform.Extents?.Cy?.Value ?? 240 * EmuPerUnit) / EmuPerUnit * map.ScaleY);

        return (Math.Round(x, 1), Math.Round(y, 1), Math.Round(width, 1), Math.Round(height, 1));
    }

    /// <summary>
    /// Texte d'un bloc PowerPoint : un paragraphe par ligne, puces retirées.
    /// </summary>
    private static string ReadTextBody(OpenXmlElement? body)
    {
        if (body is null)
        {
            return string.Empty;
        }

        IEnumerable<string> lines = body.Elements<A.Paragraph>()
            .Select(ReadParagraph)
            .Where(line => line.Length > 0);

        return string.Join("\n", lines);
    }

    /// <summary>
    /// Texte d'un paragraphe PowerPoint.
    ///
    /// Les morceaux de texte (runs) sont mis bout à bout. Un saut de ligne
    /// dans le paragraphe (Maj+Entrée dans PowerPoint) devient un vrai retour
    /// à la ligne : sans cela « Bonjour » et « le monde » se colleraient en
    /// « Bonjourle monde ».
    /// </summary>
    private static string ReadParagraph(A.Paragraph paragraph)
    {
        var text = new System.Text.StringBuilder();

        foreach (OpenXmlElement node in paragraph.Descendants())
        {
            switch (node)
            {
                case A.Text chunk:
                    text.Append(chunk.Text);
                    break;

                case A.Break:
                    text.Append('\n');
                    break;
            }
        }

        return text.ToString().Trim();
    }

    private static A.RunProperties? FirstRunProperties(OpenXmlElement? body)
    {
        return body?.Descendants<A.Run>()
            .Select(run => run.RunProperties)
            .FirstOrDefault(properties => properties is not null);
    }

    /// <summary>
    /// Taille de police : PowerPoint la stocke en centièmes de point, alors
    /// que SlideCraft utilise des points (1 unité = 1 point).
    /// </summary>
    private static double ReadFontSize(A.RunProperties? run)
    {
        int? hundredths = run?.FontSize?.Value;

        if (hundredths is not > 0)
        {
            return 20;
        }

        double points = hundredths.Value / 100.0;

        // Une taille aberrante (fichier abîmé) ne doit pas rendre la
        // diapositive illisible.
        return Math.Clamp(points, 8, 96);
    }

    private static string? ReadColor(A.RunProperties? run)
    {
        string? hex = run?.GetFirstChild<A.SolidFill>()?
            .RgbColorModelHex?.Val?.Value;

        return string.IsNullOrWhiteSpace(hex) ? null : "#" + hex.ToUpperInvariant();
    }

    private static string? ReadPlaceholderType(P.Shape shape)
    {
        return shape.NonVisualShapeProperties?
            .ApplicationNonVisualDrawingProperties?
            .PlaceholderShape?.Type?.InnerText;
    }

    private static double ReadEmu(Int32Value? value, int fallback)
    {
        return value?.Value is > 0 ? value.Value : fallback;
    }

    /// <summary>
    /// Recopie une image du .pptx dans le dossier de travail de l'application.
    /// Le nom vient du contenu : la même image n'est jamais écrite deux fois.
    /// Renvoie null pour un format que WPF ne sait pas afficher (EMF...).
    /// </summary>
    private static string? SaveImagePart(ImagePart imagePart)
    {
        string extension = imagePart.ContentType switch
        {
            "image/png" => ".png",
            "image/jpeg" => ".jpg",
            "image/gif" => ".gif",
            "image/bmp" => ".bmp",
            "image/tiff" => ".tif",
            _ => string.Empty
        };

        if (extension.Length == 0)
        {
            return null;
        }

        try
        {
            using Stream stream = imagePart.GetStream();
            using var memory = new MemoryStream();
            stream.CopyTo(memory);

            byte[] bytes = memory.ToArray();
            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SlideCraft",
                "imported");

            Directory.CreateDirectory(folder);

            string name = Convert.ToHexString(SHA256.HashData(bytes))[..24]
                + extension;
            string target = Path.Combine(folder, name.ToLowerInvariant());

            if (!File.Exists(target))
            {
                File.WriteAllBytes(target, bytes);
            }

            return target;
        }
        catch (Exception exception) when (
            exception is IOException
                or UnauthorizedAccessException
                or OpenXmlPackageException)
        {
            // Une image illisible est simplement ignorée : importer le reste
            // de la présentation vaut mieux que tout refuser.
            return null;
        }
    }
}
