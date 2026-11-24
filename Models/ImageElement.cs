namespace SlideCraft.Models;

/// <summary>
/// Une image importée depuis le disque de l'utilisateur.
/// On ne stocke que le chemin du fichier ; l'image est chargée par WPF.
/// </summary>
public class ImageElement : SlideElement
{
    private string _imagePath = string.Empty;

    public ImageElement()
    {
        Name = "Image";
    }

    /// <summary>Chemin complet du fichier image (jpg, png, bmp...).</summary>
    public string ImagePath
    {
        get => _imagePath;
        set => SetProperty(ref _imagePath, value);
    }

    public override string TypeLabel => "Image";

    /// <summary>Copie de l'image (même fichier source).</summary>
    protected override SlideElement CreateCopy()
    {
        return new ImageElement { ImagePath = ImagePath };
    }
}
