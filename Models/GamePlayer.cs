using System;
using SlideCraft.ViewModels;

namespace SlideCraft.Models;

/// <summary>
/// Un joueur du mode jeu, avec son score.
///
/// Les joueurs sont saisis par le présentateur avant la projection
/// (bouton « Game Setup »). Rien n'est envoyé sur un réseau : les votes
/// se font à main levée et le présentateur clique la réponse.
/// </summary>
public class GamePlayer : ObservableObject
{
    private int _score;

    public GamePlayer(string name)
    {
        Name = string.IsNullOrWhiteSpace(name) ? "Player" : name.Trim();
    }

    /// <summary>Nom affiché sur les boutons et dans le classement.</summary>
    public string Name { get; }

    /// <summary>Points cumulés.</summary>
    public int Score
    {
        get => _score;
        private set
        {
            if (SetProperty(ref _score, value))
            {
                OnPropertyChanged(nameof(ScoreLabel));
            }
        }
    }

    /// <summary>Texte « 30 pts » du classement.</summary>
    public string ScoreLabel => $"{Score} pt{(Score == 1 ? string.Empty : "s")}";

    /// <summary>Ajoute des points (jamais de points négatifs).</summary>
    public void AddPoints(int points)
    {
        Score += Math.Max(0, points);
    }
}
