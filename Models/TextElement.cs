namespace SlideCraft.Models;

/// <summary>
/// Un bloc de texte libre posé sur une diapositive
/// (titre, sous-titre, puces, paragraphe...).
/// </summary>
public class TextElement : SlideElement
{
    private string _text = string.Empty;
    private double _fontSize = 24;
    private string _fontFamily = "Segoe UI";
    private string _foreground = "#1F2937";
    private string _fontWeight = "Normal";

    public TextElement()
    {
        Name = "Text";
    }

    /// <summary>Contenu affiché.</summary>
    public string Text
    {
        get => _text;
        set => SetProperty(ref _text, value);
    }

    /// <summary>Taille de la police (dans le repère 960 x 540).</summary>
    public double FontSize
    {
        get => _fontSize;
        set => SetProperty(ref _fontSize, value);
    }

    /// <summary>Nom de la police, par exemple "Segoe UI" ou "Georgia".</summary>
    public string FontFamily
    {
        get => _fontFamily;
        set => SetProperty(ref _fontFamily, value);
    }

    /// <summary>Couleur du texte au format hexadécimal "#RRGGBB".</summary>
    public string Foreground
    {
        get => _foreground;
        set => SetProperty(ref _foreground, value);
    }

    /// <summary>"Normal", "Bold", "SemiBold"... (converti par WPF).</summary>
    public string FontWeight
    {
        get => _fontWeight;
        set => SetProperty(ref _fontWeight, value);
    }

    public override string TypeLabel => "Text";

    /// <summary>Copie du bloc de texte (même contenu, même style).</summary>
    protected override SlideElement CreateCopy()
    {
        return new TextElement
        {
            Text = Text,
            FontSize = FontSize,
            FontFamily = FontFamily,
            Foreground = Foreground,
            FontWeight = FontWeight
        };
    }
}
