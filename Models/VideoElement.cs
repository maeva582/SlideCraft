namespace SlideCraft.Models;

/// <summary>
/// Une vidéo insérée depuis le disque de l'utilisateur.
///
/// Comme pour les images, on ne stocke que le chemin du fichier : recopier
/// une vidéo dans le document le rendrait énorme. Le fichier est lu par le
/// lecteur multimédia de Windows pendant la projection.
/// </summary>
public class VideoElement : SlideElement
{
    private string _videoPath = string.Empty;

    public VideoElement()
    {
        Name = "Video";
        Width = 640;
        Height = 360;
    }

    /// <summary>Chemin complet du fichier vidéo (mp4, wmv, avi...).</summary>
    public string VideoPath
    {
        get => _videoPath;
        set => SetProperty(ref _videoPath, value);
    }

    public override string TypeLabel => "Video";

    /// <summary>Copie de la vidéo (même fichier source).</summary>
    protected override SlideElement CreateCopy()
    {
        return new VideoElement { VideoPath = VideoPath };
    }
}
