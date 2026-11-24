using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace SlideCraft.Views;

/// <summary>
/// La fenêtre de saisie des joueurs : une zone de texte, un joueur par ligne.
///
/// Le découpage des lignes est fait ici, à la validation, parce que c'est
/// un geste de fenêtre (le bouton « Start game » ferme la boîte de dialogue).
/// </summary>
public partial class GameSetupWindow : Window
{
    public GameSetupWindow(IReadOnlyList<string> players)
    {
        InitializeComponent();

        PlayersBox.Text = string.Join(Environment.NewLine, players ?? Array.Empty<string>());

        // Le curseur est placé à la fin pour pouvoir continuer une liste.
        PlayersBox.CaretIndex = PlayersBox.Text.Length;
        PlayersBox.Focus();
    }

    /// <summary>Liste validée par l'utilisateur (noms vides ignorés).</summary>
    public IReadOnlyList<string> Players { get; private set; } = Array.Empty<string>();

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        Players = PlayersBox.Text
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .ToList();

        DialogResult = true;
    }
}
