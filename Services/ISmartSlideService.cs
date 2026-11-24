using SlideCraft.Models;

namespace SlideCraft.Services;

/// <summary>
/// Ouvre la fenêtre de rapport SmartSlide.
///
/// Même principe que <see cref="ISlideShowService"/> : le ViewModel principal
/// ne crée jamais de fenêtre WPF lui-même, sinon il ne pourrait plus être
/// testé sans interface graphique. Il appelle cette interface, et la vraie
/// fenêtre est ouverte par <see cref="SmartSlideService"/>.
/// </summary>
public interface ISmartSlideService
{
    /// <summary>
    /// Analyse la présentation et affiche le rapport, ouvert sur la
    /// diapositive d'indice <paramref name="startIndex"/> (0 = la première).
    /// La méthode ne rend la main qu'à la fermeture du rapport.
    /// </summary>
    void Show(Presentation presentation, int startIndex);
}
