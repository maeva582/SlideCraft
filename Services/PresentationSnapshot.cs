using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using SlideCraft.Models;

namespace SlideCraft.Services;

/// <summary>
/// Photo de l'état des diapositives à un instant donné.
///
/// Pourquoi une photo plutôt qu'un code « inverse » pour chaque action ?
/// Parce qu'une seule mécanique suffit alors pour tout annuler : ajout,
/// suppression, disposition, déplacement, changement de texte... C'est plus
/// simple à comprendre et beaucoup moins risqué qu'une dizaine d'inverses.
///
/// Le prix à payer : chaque action mémorise une copie des diapositives.
/// C'est parfaitement acceptable pour une présentation de quelques dizaines
/// de diapositives, qui est la cible de SlideCraft.
/// </summary>
public sealed class PresentationSnapshot
{
    private readonly List<Slide> _slides;

    /// <summary>
    /// Prend une photo des diapositives.
    /// </summary>
    /// <param name="slides">Diapositives à copier.</param>
    /// <param name="selectedIndex">Index de la diapositive sélectionnée (-1 si aucune).</param>
    public PresentationSnapshot(IEnumerable<Slide> slides, int selectedIndex)
    {
        _slides = slides.Select(slide => slide.Clone()).ToList();
        SelectedIndex = selectedIndex;
    }

    /// <summary>Index de la diapositive qui était sélectionnée.</summary>
    public int SelectedIndex { get; }

    /// <summary>
    /// Remplace le contenu de <paramref name="target"/> par la photo.
    /// On reclone à chaque restauration pour que la photo reste intacte
    /// et puisse resservir (annuler, rétablir, annuler...).
    /// </summary>
    public void Restore(ObservableCollection<Slide> target)
    {
        target.Clear();

        foreach (Slide slide in _slides)
        {
            target.Add(slide.Clone());
        }
    }
}
