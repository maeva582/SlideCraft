namespace SlideCraft.Models;

/// <summary>
/// Une forme simple (rectangle, rectangle à coins arrondis).
/// Les ellipses et les flèches seront ajoutées à l'étape "formes".
/// </summary>
public class ShapeElement : SlideElement
{
    private string _fill = "#6366F1";
    private string _stroke = "#4338CA";
    private double _cornerRadius;

    public ShapeElement()
    {
        Name = "Shape";
    }

    /// <summary>Couleur de remplissage "#RRGGBB".</summary>
    public string Fill
    {
        get => _fill;
        set => SetProperty(ref _fill, value);
    }

    /// <summary>Couleur du contour "#RRGGBB".</summary>
    public string Stroke
    {
        get => _stroke;
        set => SetProperty(ref _stroke, value);
    }

    /// <summary>Arrondi des coins (0 = rectangle parfait).</summary>
    public double CornerRadius
    {
        get => _cornerRadius;
        set => SetProperty(ref _cornerRadius, value);
    }

    public override string TypeLabel => "Shape";

    /// <summary>Copie de la forme (mêmes couleurs).</summary>
    protected override SlideElement CreateCopy()
    {
        return new ShapeElement
        {
            Fill = Fill,
            Stroke = Stroke,
            CornerRadius = CornerRadius
        };
    }
}
