using System;
using System.Collections.Generic;
using System.Windows;
using SlideCraft.Views;

namespace SlideCraft.Services;

/// <summary>
/// Implémentation réelle de <see cref="IGameSetupService"/> : ouvre la petite
/// fenêtre où l'on tape un joueur par ligne.
/// </summary>
public class GameSetupService : IGameSetupService
{
    public IReadOnlyList<string>? ConfigurePlayers(IReadOnlyList<string> current)
    {
        var window = new GameSetupWindow(current ?? Array.Empty<string>());

        if (Application.Current?.MainWindow is { } mainWindow
            && !ReferenceEquals(mainWindow, window))
        {
            window.Owner = mainWindow;
        }

        return window.ShowDialog() == true ? window.Players : null;
    }
}
