using SlideCraft.ViewModels;

namespace SlideCraft.Models;

/// <summary>
/// Classe de base de tous les éléments posés sur une diapositive
/// (texte, image, forme, tableau).
///
/// Toutes les positions sont exprimées dans un repère fixe de 960 x 540
/// (la taille "virtuelle" d'une diapositive au format 16/9).
/// L'affichage (miniature ou plein écran) met ensuite cet ensemble à
/// l'échelle avec un Viewbox.
/// </summary>
public abstract class SlideElement : ObservableObject
{
    private string _name = "Element";
    private double _x = 60;
    private double _y = 60;
    private double _width = 400;
    private double _height = 120;
    private bool _isSelected;
    private bool _isEditing;

    /// <summary>Nom affiché dans le panneau de propriétés.</summary>
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    /// <summary>Position horizontale (en pixels du repère 960 x 540).</summary>
    public double X
    {
        get => _x;
        set => SetProperty(ref _x, value);
    }

    /// <summary>Position verticale (en pixels du repère 960 x 540).</summary>
    public double Y
    {
        get => _y;
        set => SetProperty(ref _y, value);
    }

    /// <summary>Largeur de l'élément.</summary>
    public double Width
    {
        get => _width;
        set => SetProperty(ref _width, value);
    }

    /// <summary>Hauteur de l'élément.</summary>
    public double Height
    {
        get => _height;
        set => SetProperty(ref _height, value);
    }

    /// <summary>
    /// Vrai quand l'élément est sélectionné : cela dessine un cadre bleu
    /// autour de lui dans la zone de travail.
    /// </summary>
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    /// <summary>
    /// Vrai pendant l'édition directe du contenu sur la diapositive
    /// (double-clic). Le XAML affiche alors une zone de saisie à la place
    /// du texte affiché.
    /// </summary>
    public bool IsEditing
    {
        get => _isEditing;
        set => SetProperty(ref _isEditing, value);
    }

    /// <summary>Type lisible ("Texte", "Image"...) affiché dans les propriétés.</summary>
    public abstract string TypeLabel { get; }

    /// <summary>
    /// Crée une copie indépendante de l'élément (duplication, instantané
    /// d'annulation, copier/coller).
    ///
    /// La position et la taille sont recopiées ici, une fois pour toutes :
    /// chaque type d'élément n'a donc que ses propres valeurs à recopier
    /// dans <see cref="CreateCopy"/>.
    /// </summary>
    public SlideElement Clone()
    {
        SlideElement copy = CreateCopy();
        copy.Name = Name;
        copy.X = X;
        copy.Y = Y;
        copy.Width = Width;
        copy.Height = Height;
        return copy;
    }

    /// <summary>
    /// Crée une copie vide du bon type (new TextElement(), new ShapeElement()...).
    /// Chaque type recopie ensuite uniquement ses propres propriétés.
    /// </summary>
    protected abstract SlideElement CreateCopy();
}
