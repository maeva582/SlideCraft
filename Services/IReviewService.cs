using System.Collections.Generic;
using System.Windows.Input;
using SlideCraft.Models;

namespace SlideCraft.Services;

/// <summary>
/// Ouvre la fenêtre « Review summary » (onglet Review).
///
/// Même principe que les autres services de fenêtres : le ViewModel principal
/// ne crée jamais de fenêtre WPF lui-même, sinon il ne pourrait plus être
/// testé sans interface graphique.
/// </summary>
public interface IReviewService
{
    /// <summary>
    /// Affiche le résumé de la présentation et l'historique des
    /// modifications, en modal.
    ///
    /// <paramref name="history"/> contient les noms des actions annulables,
    /// la plus récente d'abord. Les deux commandes sont celles de l'éditeur :
    /// les boutons de la fenêtre annulent donc réellement la dernière action.
    /// </summary>
    void Show(
        Presentation presentation,
        IReadOnlyList<string> history,
        ICommand? undoCommand,
        ICommand? redoCommand);
}
