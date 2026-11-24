using System;
using System.IO;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using SlideCraft.Models;
using A = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;
using P14 = DocumentFormat.OpenXml.Office2010.PowerPoint;

namespace SlideCraft.Services;

/// <summary>
/// Exporte une présentation SlideCraft vers un vrai fichier PowerPoint (.pptx).
///
/// Un .pptx n'est pas un fichier unique mais une archive ZIP de plusieurs
/// parties XML reliées entre elles par des « relations » :
///
///   presentation.xml        la liste des diapositives et la taille de page
///   slideMaster1.xml        le modèle commun (couleurs, styles)
///   slideLayout1.xml        la disposition utilisée par les diapositives
///   theme1.xml              la palette et les polices du thème
///   slides/slideN.xml       le contenu de chaque diapositive
///   media/imageN.png        les images
///
/// Le SDK Open XML construit cette arborescence à notre place : on parle en
/// objets (SlidePart, Shape, Table...) au lieu d'écrire du XML à la main.
///
/// Les coordonnées SlideCraft (repère 960 x 540) sont converties en EMU,
/// l'unité de mesure de PowerPoint (1 pouce = 914 400 EMU).
/// </summary>
public static class PowerPointExporter
{
    /// <summary>Extension du fichier exporté.</summary>
    public const string FileExtension = ".pptx";

    /// <summary>Filtre des boîtes de dialogue pour l'export.</summary>
    public const string FileFilter =
        "PowerPoint presentation (*.pptx)|*.pptx|All files (*.*)|*.*";

    /// <summary>1 unité du repère SlideCraft = 1 point = 12 700 EMU.</summary>
    private const long EmuParUnite = 12700;

    private const int LargeurPageEmu = 12192000;   // 16:9 (13,33 pouces)
    private const int HauteurPageEmu = 6858000;    // 7,5 pouces

    /// <summary>
    /// Affiche statique d'une vidéo : un petit PNG uni (8 x 8 pixels) que
    /// PowerPoint étire sur toute la forme. Il est affiché tant que la vidéo
    /// n'a pas démarré, exactement comme le fait PowerPoint.
    /// </summary>
    private const string AfficheVideoPng =
        "iVBORw0KGgoAAAANSUhEUgAAAAgAAAAICAIAAABLbSncAAAAEUlEQVR42mNQUFTBihiGlgQA3U8ZQeBeBnwAAAAASUVORK5CYII=";

    /// <summary>
    /// Écrit la présentation dans un fichier .pptx (créé ou remplacé).
    /// Le document SlideCraft lui-même n'est pas modifié : l'export est une copie.
    /// </summary>
    public static void Export(Presentation presentation, string path)
    {
        ArgumentNullException.ThrowIfNull(presentation);

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Le chemin du fichier est vide.", nameof(path));
        }

        // On construit d'abord un fichier temporaire : si l'export échoue,
        // aucun fichier à moitié écrit ne reste sur le disque.
        string temporaryPath = path + ".tmp";

        try
        {
            using (PresentationDocument document = PresentationDocument.Create(
                temporaryPath, PresentationDocumentType.Presentation))
            {
                Remplir(document, presentation);
            }

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

    // ------------------------------------------------------------------
    // Structure générale du document
    // ------------------------------------------------------------------

    private static void Remplir(PresentationDocument document, Presentation presentation)
    {
        PresentationPart presentationPart = document.AddPresentationPart();

        var listeDiapositives = new P.SlideIdList();

        // L'ordre des enfants compte : maîtres, diapositives, taille de page.
        presentationPart.Presentation = new P.Presentation(
            new P.SlideMasterIdList(),
            listeDiapositives,
            new P.SlideSize { Cx = LargeurPageEmu, Cy = HauteurPageEmu },
            new P.NotesSize { Cx = 6858000, Cy = 9144000 });

        // --- Modèle (master), disposition (layout) et thème ---
        SlideMasterPart masterPart = presentationPart.AddNewPart<SlideMasterPart>();

        // Il faut remplir le modèle AVANT de lui rattacher une disposition :
        // le SDK déduit les parties autorisées de son contenu. La première
        // relation ajoutée à un modèle s'appelle toujours « rId1 » ; on corrige
        // l'identifiant juste après, au cas où.
        masterPart.SlideMaster = new P.SlideMaster(XmlMaster("rId1"));

        SlideLayoutPart layoutPart = masterPart.AddNewPart<SlideLayoutPart>();
        layoutPart.SlideLayout = new P.SlideLayout(XmlLayout);

        // Le thème (couleurs, polices) est rattaché au modèle, pas à la
        // disposition : c'est ainsi que PowerPoint organise ses fichiers.
        ThemePart themePart = masterPart.AddNewPart<ThemePart>();
        themePart.Theme = new A.Theme(XmlTheme);

        string layoutRelationshipId = masterPart.GetIdOfPart(layoutPart);
        masterPart.SlideMaster.SlideLayoutIdList!
            .Elements<P.SlideLayoutId>()
            .First()
            .RelationshipId = layoutRelationshipId;

        presentationPart.Presentation.SlideMasterIdList!.Append(new P.SlideMasterId
        {
            Id = 2147483648U,
            RelationshipId = presentationPart.GetIdOfPart(masterPart)
        });

        // --- Une partie par diapositive ---
        uint numero = 256;   // PowerPoint réserve les petits identifiants

        foreach (Slide slide in presentation.Slides)
        {
            SlidePart slidePart = presentationPart.AddNewPart<SlidePart>();
            slidePart.Slide = ConstruireDiapositive(slide, slidePart);

            listeDiapositives.Append(new P.SlideId
            {
                Id = numero++,
                RelationshipId = presentationPart.GetIdOfPart(slidePart)
            });
        }
    }

    // ------------------------------------------------------------------
    // Une diapositive et ses formes
    // ------------------------------------------------------------------

    private static P.Slide ConstruireDiapositive(Slide slide, SlidePart slidePart)
    {
        var arbre = new P.ShapeTree(
            new P.NonVisualGroupShapeProperties(
                new P.NonVisualDrawingProperties { Id = 1U, Name = string.Empty },
                new P.NonVisualGroupShapeDrawingProperties(),
                new P.ApplicationNonVisualDrawingProperties()),
            new P.GroupShapeProperties(new A.TransformGroup(
                new A.Offset { X = 0, Y = 0 },
                new A.Extents { Cx = 0, Cy = 0 },
                new A.ChildOffset { X = 0, Y = 0 },
                new A.ChildExtents { Cx = 0, Cy = 0 })));

        uint identifiant = 2;

        foreach (SlideElement element in slide.Elements)
        {
            OpenXmlElement? forme = element switch
            {
                TextElement texte => ConstruireTexte(texte, identifiant++),
                ShapeElement forme2 => ConstruireForme(forme2, identifiant++),
                ImageElement image => ConstruireImage(image, slidePart, identifiant++),
                VideoElement video => ConstruireVideo(video, slidePart, identifiant++),
                TableElement tableau => ConstruireTableau(tableau, identifiant++),
                _ => null
            };

            if (forme is not null)
            {
                arbre.Append(forme);
            }
        }

        var contenu = new P.CommonSlideData(arbre);

        // Arrière-plan de la diapositive.
        contenu.Background = new P.Background(
            new P.BackgroundProperties(
                new A.SolidFill(new A.RgbColorModelHex { Val = Hex(slide.BackgroundColor) })));

        var diapositive = new P.Slide(
            contenu,
            new P.ColorMapOverride(new A.MasterColorMapping()));

        AjouterTransition(diapositive, slide.Transition);
        return diapositive;
    }

    /// <summary>
    /// Écrit la transition de la diapositive dans le .pptx.
    ///
    /// Une transition est un enfant direct de &lt;p:sld&gt;, après le contenu
    /// et le plan de couleurs. L'export ne pose que les trois effets que
    /// SlideCraft sait aussi rejouer : un fichier exporté puis rouvert donne
    /// donc exactement le même diaporama.
    /// </summary>
    private static void AjouterTransition(P.Slide diapositive, TransitionKind transition)
    {
        switch (transition)
        {
            case TransitionKind.Fade:
                diapositive.Append(new P.Transition(new P.FadeTransition()));
                break;

            case TransitionKind.Push:
                diapositive.Append(new P.Transition(new P.PushTransition()));
                break;

            case TransitionKind.Wipe:
                diapositive.Append(new P.Transition(new P.WipeTransition()));
                break;

            // Aucune transition : on n'écrit rien, ce qui est le
            // comportement par défaut de PowerPoint.
        }
    }

    /// <summary>Bloc de texte : une zone sans bordure ni remplissage.</summary>
    private static P.Shape ConstruireTexte(TextElement texte, uint id)
    {
        int taille = CentiemesDePoint(texte.FontSize);

        var corps = new P.TextBody(
            new A.BodyProperties
            {
                Wrap = A.TextWrappingValues.Square,
                LeftInset = 0,
                TopInset = 0,
                RightInset = 0,
                BottomInset = 0
            },
            new A.ListStyle());

        // Un retour à la ligne dans SlideCraft = un paragraphe dans PowerPoint :
        // c'est la façon naturelle de faire, et le texte reste modifiable.
        string[] lignes = (texte.Text ?? string.Empty).Split('\n');

        foreach (string ligne in lignes)
        {
            var proprietes = new A.RunProperties
            {
                Language = "fr-FR",
                FontSize = taille
            };

            if (EstGras(texte.FontWeight))
            {
                proprietes.Bold = true;
            }

            proprietes.Append(new A.SolidFill(
                new A.RgbColorModelHex { Val = Hex(texte.Foreground) }));

            if (!string.IsNullOrWhiteSpace(texte.FontFamily))
            {
                proprietes.Append(new A.LatinFont { Typeface = texte.FontFamily });
            }

            corps.Append(new A.Paragraph(
                new A.Run(proprietes, new A.Text(ligne))));
        }

        return new P.Shape(
            new P.NonVisualShapeProperties(
                new P.NonVisualDrawingProperties { Id = id, Name = Nom(texte.Name, "Texte") },
                new P.NonVisualShapeDrawingProperties(),
                new P.ApplicationNonVisualDrawingProperties()),
            new P.ShapeProperties(
                Transform(texte),
                Geometrie(A.ShapeTypeValues.Rectangle)),
            corps);
    }

    /// <summary>Forme pleine (rectangle ou rectangle à coins arrondis).</summary>
    private static P.Shape ConstruireForme(ShapeElement forme, uint id)
    {
        A.ShapeTypeValues type = forme.CornerRadius > 0
            ? A.ShapeTypeValues.RoundRectangle
            : A.ShapeTypeValues.Rectangle;

        var proprietes = new P.ShapeProperties(
            Transform(forme),
            Geometrie(type),
            new A.SolidFill(new A.RgbColorModelHex { Val = Hex(forme.Fill) }));

        if (!string.IsNullOrWhiteSpace(forme.Stroke))
        {
            proprietes.Append(new A.Outline(
                new A.SolidFill(new A.RgbColorModelHex { Val = Hex(forme.Stroke) }))
            {
                Width = (int)EmuParUnite
            });
        }

        return new P.Shape(
            new P.NonVisualShapeProperties(
                new P.NonVisualDrawingProperties { Id = id, Name = Nom(forme.Name, "Forme") },
                new P.NonVisualShapeDrawingProperties(),
                new P.ApplicationNonVisualDrawingProperties()),
            proprietes,
            new P.TextBody(
                new A.BodyProperties(),
                new A.ListStyle(),
                new A.Paragraph()));
    }

    /// <summary>Image importée : le fichier est copié dans le dossier media du .pptx.</summary>
    private static P.Picture? ConstruireImage(ImageElement image, SlidePart slidePart, uint id)
    {
        if (string.IsNullOrWhiteSpace(image.ImagePath) || !File.Exists(image.ImagePath))
        {
            // Image introuvable : on l'ignore plutôt que d'échouer sur tout l'export.
            return null;
        }

        ImagePart imagePart = slidePart.AddImagePart(TypeImage(image.ImagePath));

        using (FileStream flux = File.OpenRead(image.ImagePath))
        {
            imagePart.FeedData(flux);
        }

        return new P.Picture(
            new P.NonVisualPictureProperties(
                new P.NonVisualDrawingProperties { Id = id, Name = Nom(image.Name, "Image") },
                new P.NonVisualPictureDrawingProperties(new A.PictureLocks { NoChangeAspect = true }),
                new P.ApplicationNonVisualDrawingProperties()),
            new P.BlipFill(
                new A.Blip { Embed = slidePart.GetIdOfPart(imagePart) },
                new A.Stretch(new A.FillRectangle())),
            new P.ShapeProperties(
                Transform(image),
                Geometrie(A.ShapeTypeValues.Rectangle)));
    }

    /// <summary>
    /// Vidéo insérée : le fichier est copié dans l'archive du .pptx et
    /// PowerPoint affiche une image d'attente jusqu'au démarrage.
    ///
    /// La vidéo est déclarée par deux relations différentes, comme dans un
    /// fichier créé par PowerPoint :
    ///   • a:videoFile r:link  → relation « video » (le média à lire) ;
    ///   • p14:media r:embed   → relation « media » (stockage Office 2010+).
    /// </summary>
    private static P.Picture? ConstruireVideo(VideoElement video, SlidePart slidePart, uint id)
    {
        if (string.IsNullOrWhiteSpace(video.VideoPath) || !File.Exists(video.VideoPath))
        {
            // Vidéo introuvable : on l'ignore plutôt que d'échouer sur tout l'export.
            return null;
        }

        try
        {
            MediaDataPart mediaPart = slidePart.OpenXmlPackage
                .CreateMediaDataPart(TypeVideo(video.VideoPath));

            using (FileStream flux = File.OpenRead(video.VideoPath))
            {
                mediaPart.FeedData(flux);
            }

            VideoReferenceRelationship relationVideo =
                slidePart.AddVideoReferenceRelationship(mediaPart);
            MediaReferenceRelationship relationMedia =
                slidePart.AddMediaReferenceRelationship(mediaPart);

            ImagePart affiche = slidePart.AddImagePart(ImagePartType.Png);

            using (var memoire = new MemoryStream(Convert.FromBase64String(AfficheVideoPng)))
            {
                affiche.FeedData(memoire);
            }

            var proprietes = new P.ApplicationNonVisualDrawingProperties(
                new A.VideoFromFile { Link = relationVideo.Id },
                new P.ApplicationNonVisualDrawingPropertiesExtensionList(
                    new P.ApplicationNonVisualDrawingPropertiesExtension(
                        new P14.Media { Embed = relationMedia.Id })
                    {
                        Uri = "{DAA4B4D4-6D71-4841-9C94-3DE7FCFB9230}"
                    }));

            return new P.Picture(
                new P.NonVisualPictureProperties(
                    new P.NonVisualDrawingProperties { Id = id, Name = Nom(video.Name, "Video") },
                    new P.NonVisualPictureDrawingProperties(new A.PictureLocks { NoChangeAspect = true }),
                    proprietes),
                new P.BlipFill(
                    new A.Blip { Embed = slidePart.GetIdOfPart(affiche) },
                    new A.Stretch(new A.FillRectangle())),
                new P.ShapeProperties(
                    Transform(video),
                    Geometrie(A.ShapeTypeValues.Rectangle)));
        }
        catch (Exception exception) when (
            exception is IOException
                or UnauthorizedAccessException
                or OpenXmlPackageException)
        {
            // Vidéo verrouillée ou illisible : on garde le reste de l'export.
            return null;
        }
    }

    /// <summary>Type MIME utilisé pour déclarer la vidéo dans l'archive.</summary>
    private static string TypeVideo(string chemin)
    {
        return Path.GetExtension(chemin).ToLowerInvariant() switch
        {
            ".wmv" => "video/x-ms-wmv",
            ".avi" => "video/x-msvideo",
            ".mov" => "video/quicktime",
            ".mpg" or ".mpeg" => "video/mpeg",
            ".webm" => "video/webm",
            _ => "video/mp4"
        };
    }

    /// <summary>Tableau : les cellules sont réparties régulièrement.</summary>
    private static P.GraphicFrame ConstruireTableau(TableElement tableau, uint id)
    {
        int colonnes = Math.Max(1, tableau.Columns);
        int lignes = Math.Max(1, tableau.Rows);

        long largeurCellule = Math.Max(1, (long)(tableau.Width * EmuParUnite) / colonnes);
        long hauteurLigne = Math.Max(1, (long)(tableau.Height * EmuParUnite) / lignes);

        var grille = new A.TableGrid();
        for (int c = 0; c < colonnes; c++)
        {
            grille.Append(new A.GridColumn { Width = largeurCellule });
        }

        var table = new A.Table(
            new A.TableProperties { FirstRow = true, BandRow = true },
            grille);

        int taille = CentiemesDePoint(tableau.FontSize);

        for (int r = 0; r < lignes; r++)
        {
            var ligne = new A.TableRow { Height = hauteurLigne };

            for (int c = 0; c < colonnes; c++)
            {
                int index = (r * colonnes) + c;
                string contenu = index < tableau.Cells.Count ? tableau.Cells[index].Text : string.Empty;

                ligne.Append(new A.TableCell(
                    new A.TextBody(
                        new A.BodyProperties(),
                        new A.Paragraph(new A.Run(
                            new A.RunProperties { Language = "fr-FR", FontSize = taille },
                            new A.Text(contenu)))),
                    new A.TableCellProperties()));
            }

            table.Append(ligne);
        }

        return new P.GraphicFrame(
            new P.NonVisualGraphicFrameProperties(
                new P.NonVisualDrawingProperties { Id = id, Name = Nom(tableau.Name, "Tableau") },
                new P.NonVisualGraphicFrameDrawingProperties(),
                new P.ApplicationNonVisualDrawingProperties()),
            new P.Transform(
                new A.Offset { X = Emu(tableau.X), Y = Emu(tableau.Y) },
                new A.Extents { Cx = Emu(tableau.Width), Cy = Emu(tableau.Height) }),
            new A.Graphic(new A.GraphicData(table) { Uri = "http://schemas.openxmlformats.org/drawingml/2006/table" }));
    }

    // ------------------------------------------------------------------
    // Petits outils partagés
    // ------------------------------------------------------------------

    /// <summary>Position et taille d'un élément, converties en EMU.</summary>
    private static A.Transform2D Transform(SlideElement element)
    {
        return new A.Transform2D(
            new A.Offset { X = Emu(element.X), Y = Emu(element.Y) },
            new A.Extents { Cx = Emu(element.Width), Cy = Emu(element.Height) });
    }

    private static A.PresetGeometry Geometrie(A.ShapeTypeValues type)
    {
        return new A.PresetGeometry(new A.AdjustValueList()) { Preset = type };
    }

    private static long Emu(double unites) => (long)Math.Round(unites * EmuParUnite);

    /// <summary>Taille de police : PowerPoint compte en centièmes de point.</summary>
    private static int CentiemesDePoint(double taille)
    {
        return (int)Math.Round(Math.Clamp(taille, 1, 400) * 100);
    }

    private static bool EstGras(string? graisse)
    {
        return graisse is not null
            && (graisse.Contains("Bold", StringComparison.OrdinalIgnoreCase)
                || graisse.Equals("Black", StringComparison.OrdinalIgnoreCase));
    }

    private static string Nom(string? nom, string secours)
    {
        return string.IsNullOrWhiteSpace(nom) ? secours : nom;
    }

    /// <summary>Transforme « #RRGGBB » en « RRGGBB » (PowerPoint n'aime pas le #).</summary>
    private static string Hex(string? couleur)
    {
        if (string.IsNullOrWhiteSpace(couleur))
        {
            return "000000";
        }

        string valeur = couleur.Trim().TrimStart('#');

        if (valeur.Length == 8)
        {
            valeur = valeur[^6..];   // on ignore l'éventuel canal alpha
        }

        if (valeur.Length != 6 || !valeur.All(Uri.IsHexDigit))
        {
            return "000000";
        }

        return valeur.ToUpperInvariant();
    }

    private static PartTypeInfo TypeImage(string chemin)
    {
        return Path.GetExtension(chemin).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => ImagePartType.Jpeg,
            ".gif" => ImagePartType.Gif,
            ".bmp" => ImagePartType.Bmp,
            ".tif" or ".tiff" => ImagePartType.Tiff,
            _ => ImagePartType.Png
        };
    }

    // ------------------------------------------------------------------
    // XML minimal du modèle, de la disposition et du thème.
    // Ces trois parties sont imposées par la norme : PowerPoint refuse un
    // fichier sans elles. Les écrire ici évite de créer des parties vides.
    // ------------------------------------------------------------------

    private static string XmlMaster(string layoutRelationshipId) => $$"""
        <p:sldMaster xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main"
                     xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"
                     xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main">
          <p:cSld>
            <p:bg>
              <p:bgPr>
                <a:solidFill><a:schemeClr val="bg1"/></a:solidFill>
                <a:effectLst/>
              </p:bgPr>
            </p:bg>
            <p:spTree>
              <p:nvGrpSpPr>
                <p:cNvPr id="1" name=""/>
                <p:cNvGrpSpPr/>
                <p:nvPr/>
              </p:nvGrpSpPr>
              <p:grpSpPr>
                <a:xfrm>
                  <a:off x="0" y="0"/>
                  <a:ext cx="0" cy="0"/>
                  <a:chOff x="0" y="0"/>
                  <a:chExt cx="0" cy="0"/>
                </a:xfrm>
              </p:grpSpPr>
            </p:spTree>
          </p:cSld>
          <p:clrMap bg1="lt1" tx1="dk1" bg2="lt2" tx2="dk2"
                    accent1="accent1" accent2="accent2" accent3="accent3"
                    accent4="accent4" accent5="accent5" accent6="accent6"
                    hlink="hlink" folHlink="folHlink"/>
          <p:sldLayoutIdLst>
            <p:sldLayoutId id="2147483649" r:id="{{layoutRelationshipId}}"/>
          </p:sldLayoutIdLst>
        </p:sldMaster>
        """;

    private const string XmlLayout = """
        <p:sldLayout xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main"
                     xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"
                     xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main"
                     type="blank" preserve="1">
          <p:cSld name="Blank slide">
            <p:spTree>
              <p:nvGrpSpPr>
                <p:cNvPr id="1" name=""/>
                <p:cNvGrpSpPr/>
                <p:nvPr/>
              </p:nvGrpSpPr>
              <p:grpSpPr>
                <a:xfrm>
                  <a:off x="0" y="0"/>
                  <a:ext cx="0" cy="0"/>
                  <a:chOff x="0" y="0"/>
                  <a:chExt cx="0" cy="0"/>
                </a:xfrm>
              </p:grpSpPr>
            </p:spTree>
          </p:cSld>
          <p:clrMapOvr><a:masterClrMapping/></p:clrMapOvr>
        </p:sldLayout>
        """;

    private const string XmlTheme = """
        <a:theme xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" name="SlideCraft">
          <a:themeElements>
            <a:clrScheme name="SlideCraft">
              <a:dk1><a:sysClr val="windowText" lastClr="000000"/></a:dk1>
              <a:lt1><a:sysClr val="window" lastClr="FFFFFF"/></a:lt1>
              <a:dk2><a:srgbClr val="111827"/></a:dk2>
              <a:lt2><a:srgbClr val="F3F4F7"/></a:lt2>
              <a:accent1><a:srgbClr val="4F46E5"/></a:accent1>
              <a:accent2><a:srgbClr val="10B981"/></a:accent2>
              <a:accent3><a:srgbClr val="F59E0B"/></a:accent3>
              <a:accent4><a:srgbClr val="EF4444"/></a:accent4>
              <a:accent5><a:srgbClr val="6366F1"/></a:accent5>
              <a:accent6><a:srgbClr val="6B7280"/></a:accent6>
              <a:hlink><a:srgbClr val="0563C1"/></a:hlink>
              <a:folHlink><a:srgbClr val="954F72"/></a:folHlink>
            </a:clrScheme>
            <a:fontScheme name="SlideCraft">
              <a:majorFont>
                <a:latin typeface="Segoe UI Semibold"/>
                <a:ea typeface=""/>
                <a:cs typeface=""/>
              </a:majorFont>
              <a:minorFont>
                <a:latin typeface="Segoe UI"/>
                <a:ea typeface=""/>
                <a:cs typeface=""/>
              </a:minorFont>
            </a:fontScheme>
            <a:fmtScheme name="SlideCraft">
              <a:fillStyleLst>
                <a:solidFill><a:schemeClr val="phClr"/></a:solidFill>
                <a:solidFill><a:schemeClr val="phClr"><a:tint val="60000"/></a:schemeClr></a:solidFill>
                <a:solidFill><a:schemeClr val="phClr"><a:shade val="80000"/></a:schemeClr></a:solidFill>
              </a:fillStyleLst>
              <a:lnStyleLst>
                <a:ln w="6350" cap="flat" cmpd="sng" algn="ctr">
                  <a:solidFill><a:schemeClr val="phClr"/></a:solidFill>
                  <a:prstDash val="solid"/>
                </a:ln>
                <a:ln w="12700" cap="flat" cmpd="sng" algn="ctr">
                  <a:solidFill><a:schemeClr val="phClr"/></a:solidFill>
                  <a:prstDash val="solid"/>
                </a:ln>
                <a:ln w="19050" cap="flat" cmpd="sng" algn="ctr">
                  <a:solidFill><a:schemeClr val="phClr"/></a:solidFill>
                  <a:prstDash val="solid"/>
                </a:ln>
              </a:lnStyleLst>
              <a:effectStyleLst>
                <a:effectStyle><a:effectLst/></a:effectStyle>
                <a:effectStyle><a:effectLst/></a:effectStyle>
                <a:effectStyle><a:effectLst/></a:effectStyle>
              </a:effectStyleLst>
              <a:bgFillStyleLst>
                <a:solidFill><a:schemeClr val="phClr"/></a:solidFill>
                <a:solidFill><a:schemeClr val="phClr"><a:tint val="95000"/></a:schemeClr></a:solidFill>
                <a:solidFill><a:schemeClr val="phClr"><a:shade val="90000"/></a:schemeClr></a:solidFill>
              </a:bgFillStyleLst>
            </a:fmtScheme>
          </a:themeElements>
        </a:theme>
        """;
}
