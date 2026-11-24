using System.Collections.Generic;
using SlideCraft.Models;

namespace SlideCraft.Services;

/// <summary>
/// Ouvre la fenêtre de projection en plein écran.
///
/// Comme pour <see cref="IFileDialogService"/>, le ViewModel principal ne
/// peut pas créer une fenêtre WPF lui-même : il perdrait sa faculté d'être
/// testé sans interface graphique. Il appelle donc cette petite interface,
/// et la vraie fenêtre est ouverte par <see cref="SlideShowService"/>.
/// </summary>
public interface ISlideShowService
{
    /// <summary>
    /// Projette la présentation en plein écran, en commençant à
    /// <paramref name="startIndex"/> (0 = première diapositive).
    /// La méthode ne rend la main que lorsque l'utilisateur quitte le
    /// diaporama (Échap).
    ///
    /// <paramref name="players"/> active le mode jeu : les diapositives qui
    /// ont un quiz affichent la question, et les points vont aux joueurs.
    /// Une liste vide ou null laisse le diaporama en mode normal.
    /// </summary>
    void Show(
        Presentation presentation,
        int startIndex,
        IReadOnlyList<string>? players = null);
}
