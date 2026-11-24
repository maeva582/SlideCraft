using System;
using System.Collections.Generic;

namespace SlideCraft.Models;

/// <summary>
/// Transition jouée quand une diapositive apparaît pendant le diaporama.
///
/// Une transition est un effet d'ENTRÉE : elle ne dure qu'un instant, au
/// moment où l'on passe d'une diapositive à la suivante. Ce n'est pas une
/// animation (un texte qui bouge à l'intérieur de la diapositive).
/// </summary>
public enum TransitionKind
{
    /// <summary>Aucun effet : la diapositive suivante apparaît d'un coup.</summary>
    None,

    /// <summary>La nouvelle diapositive apparaît en fondu (opacité croissante).</summary>
    Fade,

    /// <summary>La nouvelle diapositive glisse depuis le côté de l'écran.</summary>
    Push,

    /// <summary>La nouvelle diapositive est découverte depuis un côté (balayage).</summary>
    Wipe
}

/// <summary>
/// Petites fonctions communes sur les transitions : le nom affiché, la
/// phrase d'explication et la lecture d'un nom écrit dans un fichier.
///
/// Tout est ici pour que le ruban, le diaporama et les fichiers (.scraft)
/// parlent exactement le même langage.
/// </summary>
public static class TransitionCatalog
{
    /// <summary>Les transitions proposées dans l'onglet Transitions, dans l'ordre.</summary>
    public static IReadOnlyList<TransitionKind> All { get; } = new[]
    {
        TransitionKind.None,
        TransitionKind.Fade,
        TransitionKind.Push,
        TransitionKind.Wipe
    };

    /// <summary>Nom court affiché dans le ruban et dans les rapports.</summary>
    public static string Label(TransitionKind kind) => kind switch
    {
        TransitionKind.Fade => "Fade",
        TransitionKind.Push => "Push",
        TransitionKind.Wipe => "Wipe",
        _ => "None"
    };

    /// <summary>
    /// Explication en une phrase, écrite pour quelqu'un qui n'a jamais
    /// utilisé de logiciel de présentation.
    /// </summary>
    public static string Description(TransitionKind kind) => kind switch
    {
        TransitionKind.Fade => "The next slide appears gradually, in a fade.",
        TransitionKind.Push => "The next slide slides in from the side of the screen.",
        TransitionKind.Wipe => "The next slide is revealed from one side, like a curtain.",
        _ => "The next slide appears instantly, with no effect."
    };

    /// <summary>
    /// Lit un nom enregistré dans un fichier (« Fade », « Push »...).
    /// Un nom inconnu ou absent donne <see cref="TransitionKind.None"/> :
    /// un vieux fichier reste ouvrable.
    /// </summary>
    public static TransitionKind Parse(string? text, TransitionKind fallback = TransitionKind.None)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return fallback;
        }

        foreach (TransitionKind kind in All)
        {
            if (string.Equals(Label(kind), text.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return kind;
            }
        }

        return fallback;
    }

    /// <summary>
    /// Convertit le choix d'un bouton du ruban (le texte de son paramètre)
    /// en transition. Utilisé par la commande du ViewModel.
    /// </summary>
    public static bool TryParse(object? parameter, out TransitionKind kind)
    {
        kind = Parse(parameter as string, TransitionKind.None);
        return parameter is string;
    }
}
