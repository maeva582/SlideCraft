using System.Collections.Generic;

namespace SlideCraft.Services;

/// <summary>
/// Demande au présentateur la liste des joueurs du mode jeu.
/// Les noms restent sur le PC : aucun réseau n'est utilisé.
/// </summary>
public interface IGameSetupService
{
    /// <summary>
    /// Affiche la fenêtre de saisie des joueurs.
    /// Renvoie la nouvelle liste, ou null si l'utilisateur annule.
    /// </summary>
    IReadOnlyList<string>? ConfigurePlayers(IReadOnlyList<string> current);
}
