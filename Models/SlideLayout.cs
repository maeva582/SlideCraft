using System.Collections.Generic;

namespace SlideCraft.Models;

/// <summary>
/// Les dispositions proposées dans l'onglet « Conception ».
///
/// Une disposition est un simple modèle : elle vide la diapositive puis
/// y place des éléments déjà positionnés (titre, colonnes, image...).
/// C'est exactement le principe des « dispositions » de PowerPoint.
/// </summary>
public enum SlideLayoutKind
{
    Blank,
    Title,
    TitleAndContent,
    TwoColumns,
    TextAndImage
}

/// <summary>
/// Une disposition prête à l'emploi : un nom, une description et le code
/// qui remplit la diapositive.
/// </summary>
public class SlideLayout
{
    public SlideLayout(SlideLayoutKind kind, string name, string description)
    {
        Kind = kind;
        Name = name;
        Description = description;
    }

    /// <summary>Identifiant interne de la disposition.</summary>
    public SlideLayoutKind Kind { get; }

    /// <summary>Nom affiché dans le menu (ex. « Deux colonnes »).</summary>
    public string Name { get; }

    /// <summary>Petite explication affichée sous le nom.</summary>
    public string Description { get; }

    /// <summary>
    /// La liste des dispositions, dans l'ordre du menu.
    /// Elle est fixe : on la crée une seule fois (static).
    /// </summary>
    public static IReadOnlyList<SlideLayout> Catalog { get; } = new List<SlideLayout>
    {
        new(SlideLayoutKind.TitleAndContent, "Title and Content", "A title and a bulleted list"),
        new(SlideLayoutKind.Title, "Title Slide", "Large centered title and subtitle"),
        new(SlideLayoutKind.TwoColumns, "Two Content", "A title and two blocks side by side"),
        new(SlideLayoutKind.TextAndImage, "Text and Picture", "Text on the left, picture on the right"),
        new(SlideLayoutKind.Blank, "Blank", "No element, empty page")
    };

    /// <summary>Retrouve une disposition par son identifiant (utile pour les tests).</summary>
    public static SlideLayout Get(SlideLayoutKind kind)
    {
        foreach (SlideLayout layout in Catalog)
        {
            if (layout.Kind == kind)
            {
                return layout;
            }
        }

        return Catalog[0];
    }

    /// <summary>
    /// Applique la disposition à une diapositive :
    /// on efface les éléments existants, puis on crée les nouveaux.
    ///
    /// Attention : le contenu précédent est perdu (pas encore d'annulation,
    /// elle arrive à l'étape 3).
    /// </summary>
    public void Apply(Slide slide)
    {
        if (slide is null)
        {
            return;
        }

        slide.Elements.Clear();

        switch (Kind)
        {
            case SlideLayoutKind.Title:
                AddTitleSlide(slide);
                break;

            case SlideLayoutKind.TitleAndContent:
                AddTitleAndContent(slide);
                break;

            case SlideLayoutKind.TwoColumns:
                AddTwoColumns(slide);
                break;

            case SlideLayoutKind.TextAndImage:
                AddTextAndImage(slide);
                break;

            default:
                // Blank : on ne remet rien, la diapositive reste vide.
                break;
        }
    }

    // ------------------------------------------------------------------
    // Contenu de chaque disposition
    // Toutes les coordonnées utilisent le repère 960 x 540 de la diapositive.
    // ------------------------------------------------------------------

    private static void AddTitleSlide(Slide slide)
    {
        slide.Elements.Add(new ShapeElement
        {
            Name = "Accent bar",
            X = 90, Y = 170, Width = 90, Height = 6,
            Fill = "#6366F1", Stroke = "#6366F1", CornerRadius = 3
        });
        slide.Elements.Add(new TextElement
        {
            Name = "Title",
            X = 90, Y = 195, Width = 780, Height = 110,
            Text = "Presentation title",
            FontSize = 48, FontWeight = "Bold", Foreground = "#111827"
        });
        slide.Elements.Add(new TextElement
        {
            Name = "Subtitle",
            X = 92, Y = 305, Width = 760, Height = 60,
            Text = "Subtitle or presenter name",
            FontSize = 22, Foreground = "#6B7280"
        });
    }

    private static void AddTitleAndContent(Slide slide)
    {
        slide.Elements.Add(new TextElement
        {
            Name = "Title",
            X = 70, Y = 50, Width = 820, Height = 80,
            Text = "Slide title",
            FontSize = 34, FontWeight = "Bold", Foreground = "#111827"
        });
        slide.Elements.Add(new TextElement
        {
            Name = "Content",
            X = 70, Y = 150, Width = 820, Height = 330,
            Text = "• First point\n• Second point\n• Third point",
            FontSize = 22, Foreground = "#374151"
        });
    }

    private static void AddTwoColumns(Slide slide)
    {
        slide.Elements.Add(new TextElement
        {
            Name = "Title",
            X = 70, Y = 45, Width = 820, Height = 70,
            Text = "Slide title",
            FontSize = 32, FontWeight = "Bold", Foreground = "#111827"
        });
        slide.Elements.Add(new TextElement
        {
            Name = "Left column",
            X = 70, Y = 140, Width = 390, Height = 320,
            Text = "• Left column point\n• Second point",
            FontSize = 20, Foreground = "#374151"
        });
        slide.Elements.Add(new TextElement
        {
            Name = "Right column",
            X = 500, Y = 140, Width = 390, Height = 320,
            Text = "• Right column point\n• Second point",
            FontSize = 20, Foreground = "#374151"
        });
    }

    private static void AddTextAndImage(Slide slide)
    {
        slide.Elements.Add(new TextElement
        {
            Name = "Title",
            X = 70, Y = 45, Width = 820, Height = 70,
            Text = "Slide title",
            FontSize = 32, FontWeight = "Bold", Foreground = "#111827"
        });
        slide.Elements.Add(new TextElement
        {
            Name = "Text",
            X = 70, Y = 140, Width = 360, Height = 320,
            Text = "• Point to explain\n• Second point",
            FontSize = 20, Foreground = "#374151"
        });
        // Une forme grise sert de cadre pour l'image (étape 3 : vraie image).
        slide.Elements.Add(new ShapeElement
        {
            Name = "Picture placeholder",
            X = 470, Y = 140, Width = 420, Height = 280,
            Fill = "#E5E7EB", Stroke = "#D1D5DB", CornerRadius = 6
        });
        slide.Elements.Add(new TextElement
        {
            Name = "Caption",
            X = 470, Y = 430, Width = 420, Height = 40,
            Text = "Picture caption",
            FontSize = 15, Foreground = "#6B7280"
        });
    }
}
