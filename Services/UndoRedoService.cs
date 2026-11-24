using System;
using System.Collections.Generic;

namespace SlideCraft.Services;

/// <summary>
/// Une action annulable : un nom lisible (« Ajout d'une diapositive ») et
/// deux méthodes, l'une pour annuler, l'autre pour rétablir.
///
/// Les deux méthodes sont fournies par le ViewModel sous forme de lambdas :
/// elles se contentent de remettre en place un instantané de la présentation.
/// </summary>
public sealed class EditEntry
{
    public EditEntry(string name, Action undo, Action redo)
    {
        Name = name ?? string.Empty;
        Undo = undo ?? throw new ArgumentNullException(nameof(undo));
        Redo = redo ?? throw new ArgumentNullException(nameof(redo));
    }

    /// <summary>Nom de l'action (affiché dans la barre d'état).</summary>
    public string Name { get; }

    /// <summary>Remet la présentation dans l'état d'avant l'action.</summary>
    public Action Undo { get; }

    /// <summary>Remet la présentation dans l'état d'après l'action.</summary>
    public Action Redo { get; }
}

/// <summary>
/// Historique d'annulation / rétablissement (boutons « Annuler » et « Rétablir »).
///
/// Principe : on empile les actions au fur et à mesure. Annuler dépile une
/// action de la pile « annuler » et la range dans la pile « rétablir ».
/// Toute nouvelle action vide la pile « rétablir », car il n'y a alors plus
/// de futur à retrouver.
///
/// On utilise des listes plutôt que des piles : la dernière action est alors
/// simplement le dernier élément, et surtout on peut oublier la plus ancienne
/// quand l'historique devient trop long. Chaque action garde une copie des
/// diapositives ; sans limite, une longue séance ferait grossir la mémoire
/// sans fin.
/// </summary>
public class UndoRedoService
{
    /// <summary>Nombre maximal d'actions mémorisées (au-delà, on oublie la plus ancienne).</summary>
    public const int MaxSavedActions = 50;

    private readonly List<EditEntry> _undo = new();
    private readonly List<EditEntry> _redo = new();

    /// <summary>Vrai si une action peut être annulée.</summary>
    public bool CanUndo => _undo.Count > 0;

    /// <summary>Vrai si une action annulée peut être rétablie.</summary>
    public bool CanRedo => _redo.Count > 0;

    /// <summary>Nombre d'actions dans l'historique (utile pour les tests).</summary>
    public int UndoCount => _undo.Count;

    /// <summary>
    /// Noms des actions mémorisées, la plus récente d'abord.
    /// C'est la liste affichée par l'onglet Review (fenêtre Review summary) :
    /// elle montre exactement ce que Ctrl+Z déferait, dans l'ordre.
    /// </summary>
    public IReadOnlyList<string> RecentActions
    {
        get
        {
            var names = new List<string>(_undo.Count);

            for (int index = _undo.Count - 1; index >= 0; index--)
            {
                names.Add(_undo[index].Name);
            }

            return names;
        }
    }

    /// <summary>
    /// L'action qui se trouve au sommet de l'historique, ou null si l'on est
    /// revenu à l'état de départ.
    /// Sert à savoir si le document a été modifié depuis le dernier
    /// enregistrement : on compare simplement cette référence.
    /// </summary>
    public EditEntry? CurrentEntry => _undo.Count > 0 ? _undo[^1] : null;

    /// <summary>Enregistre une nouvelle action.</summary>
    public void Record(EditEntry entry)
    {
        if (entry is null)
        {
            return;
        }

        _undo.Add(entry);

        if (_undo.Count > MaxSavedActions)
        {
            _undo.RemoveAt(0);
        }

        _redo.Clear();
    }

    /// <summary>Annule la dernière action et renvoie son nom (null s'il n'y en a pas).</summary>
    public string? Undo()
    {
        if (_undo.Count == 0)
        {
            return null;
        }

        EditEntry entry = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);

        entry.Undo();
        _redo.Add(entry);

        return entry.Name;
    }

    /// <summary>Rétablit la dernière action annulée et renvoie son nom.</summary>
    public string? Redo()
    {
        if (_redo.Count == 0)
        {
            return null;
        }

        EditEntry entry = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);

        entry.Redo();
        _undo.Add(entry);

        return entry.Name;
    }

    /// <summary>Noms des actions annulées puis rétablies, la plus récente d'abord.</summary>
    public IReadOnlyList<string> RedoActions
    {
        get
        {
            var names = new List<string>(_redo.Count);

            for (int index = _redo.Count - 1; index >= 0; index--)
            {
                names.Add(_redo[index].Name);
            }

            return names;
        }
    }

    /// <summary>Vide l'historique (par exemple à l'ouverture d'un fichier).</summary>
    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
    }
}
