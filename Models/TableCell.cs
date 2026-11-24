using SlideCraft.ViewModels;

namespace SlideCraft.Models;

/// <summary>
/// Une cellule de tableau : son texte, plus l'état « en cours d'édition ».
///
/// Pourquoi une classe et pas une simple chaîne ? Parce que l'interface a
/// besoin de savoir QUELLE cellule est en train d'être modifiée pour afficher
/// un champ de saisie à sa place. Une chaîne ne peut pas porter cette
/// information, un petit modèle si.
/// </summary>
public class TableCell : ObservableObject
{
    private string _text = string.Empty;
    private bool _isEditing;

    /// <summary>Texte affiché dans la cellule.</summary>
    public string Text
    {
        get => _text;
        set => SetProperty(ref _text, value);
    }

    /// <summary>
    /// Vrai pendant l'édition directe : le XAML remplace alors le texte par
    /// une zone de saisie.
    /// </summary>
    public bool IsEditing
    {
        get => _isEditing;
        set => SetProperty(ref _isEditing, value);
    }

    /// <summary>Copie indépendante de la cellule (duplication, annulation).</summary>
    public TableCell Clone()
    {
        return new TableCell { Text = Text };
    }
}
