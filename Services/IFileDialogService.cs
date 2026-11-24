namespace SlideCraft.Services;

/// <summary>
/// Ouvre les fenêtres de dialogue de Windows (choisir un fichier, confirmer,
/// signaler une erreur).
///
/// Le ViewModel ne peut pas appeler directement WPF (OpenFileDialog,
/// MessageBox) : il perdrait sa faculté d'être testé sans interface
/// graphique. On passe donc par cette petite interface, et la vraie
/// implémentation est fournie par <see cref="FileDialogService"/>.
/// </summary>
public interface IFileDialogService
{
    /// <summary>
    /// Demande à l'utilisateur une image.
    /// Renvoie le chemin complet, ou null s'il annule.
    /// </summary>
    string? PickImageFile();

    /// <summary>
    /// Demande à l'utilisateur une vidéo (mp4, wmv, avi...).
    /// Renvoie le chemin complet, ou null s'il annule.
    /// </summary>
    string? PickVideoFile();

    /// <summary>
    /// Demande quel fichier de présentation ouvrir.
    /// Renvoie le chemin complet, ou null si l'utilisateur annule.
    /// </summary>
    string? PickOpenFile();

    /// <summary>
    /// Demande quel fichier PowerPoint (.pptx) importer.
    /// Renvoie le chemin complet, ou null si l'utilisateur annule.
    /// </summary>
    string? PickPowerPointFile();

    /// <summary>
    /// Demande où enregistrer un fichier (présentation ou export).
    /// <paramref name="filter"/> décrit les types proposés, par exemple
    /// « Présentation SlideCraft (*.scraft)|*.scraft ».
    /// Renvoie le chemin complet, ou null si l'utilisateur annule.
    /// </summary>
    string? PickSaveFile(string suggestedName, string filter, string defaultExtension);

    /// <summary>Pose une question à l'utilisateur (Oui = true).</summary>
    bool Confirm(string message);

    /// <summary>Affiche un message d'erreur.</summary>
    void ShowError(string message);
}
