using System.Collections.ObjectModel;
using SlideCraft.ViewModels;

namespace SlideCraft.Models;

/// <summary>
/// Une présentation complète : un titre et une liste de diapositives.
/// C'est l'objet racine que le ViewModel expose à l'interface.
/// </summary>
public class Presentation : ObservableObject
{
    private string _title = "Untitled presentation";
    private string? _filePath;
    private bool _isModified;

    /// <summary>Titre affiché dans la barre de titre.</summary>
    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    /// <summary>
    /// Chemin du fichier .scraft (ou .pptx) sur le disque.
    /// null tant que la présentation n'a jamais été enregistrée.
    /// </summary>
    public string? FilePath
    {
        get => _filePath;
        set => SetProperty(ref _filePath, value);
    }

    /// <summary>Vrai quand des modifications n'ont pas encore été enregistrées.</summary>
    public bool IsModified
    {
        get => _isModified;
        set => SetProperty(ref _isModified, value);
    }

    /// <summary>Diapositives de la présentation, dans l'ordre d'affichage.</summary>
    public ObservableCollection<Slide> Slides { get; } = new();
}
