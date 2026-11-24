using System.Collections.ObjectModel;

namespace SlideCraft.Models;

/// <summary>
/// Un tableau simple. Les cellules sont stockées ligne par ligne dans une
/// liste de textes : la première ligne contient les en-têtes.
///
/// Exemple pour 2 colonnes et 3 lignes :
/// 0 = "Produit", 1 = "Prix", 2 = "Stylo", 3 = "2 €", ...
/// </summary>
public class TableElement : SlideElement
{
    private int _columns = 2;
    private int _rows = 3;
    private string _fill = "#FFFFFF";
    private string _stroke = "#D1D5DB";
    private double _fontSize = 16;

    public TableElement()
    {
        Name = "Table";
    }

    /// <summary>Nombre de colonnes.</summary>
    public int Columns
    {
        get => _columns;
        set => SetProperty(ref _columns, value);
    }

    /// <summary>Nombre de lignes.</summary>
    public int Rows
    {
        get => _rows;
        set => SetProperty(ref _rows, value);
    }

    /// <summary>Contenu des cellules, de gauche à droite puis de haut en bas.</summary>
    public ObservableCollection<TableCell> Cells { get; } = new();

    /// <summary>Ajoute une cellule avec son texte (raccourci lisible).</summary>
    public void AddCell(string text)
    {
        Cells.Add(new TableCell { Text = text });
    }

    /// <summary>Couleur de fond des cellules.</summary>
    public string Fill
    {
        get => _fill;
        set => SetProperty(ref _fill, value);
    }

    /// <summary>Couleur des traits du tableau.</summary>
    public string Stroke
    {
        get => _stroke;
        set => SetProperty(ref _stroke, value);
    }

    /// <summary>Taille du texte des cellules.</summary>
    public double FontSize
    {
        get => _fontSize;
        set => SetProperty(ref _fontSize, value);
    }

    public override string TypeLabel => "Table";

    /// <summary>
    /// Copie du tableau : on recopie les cellules une par une, sinon les deux
    /// tableaux partageraient la même liste (modifier l'un modifierait l'autre).
    /// </summary>
    protected override SlideElement CreateCopy()
    {
        var copy = new TableElement
        {
            Columns = Columns,
            Rows = Rows,
            Fill = Fill,
            Stroke = Stroke,
            FontSize = FontSize
        };

        foreach (TableCell cell in Cells)
        {
            copy.Cells.Add(cell.Clone());
        }

        return copy;
    }
}
