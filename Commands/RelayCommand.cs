using System;
using System.Windows.Input;

namespace SlideCraft.Commands;

/// <summary>
/// Une commande "prête à l'emploi" pour le MVVM.
///
/// En WPF, un Button peut appeler une commande au lieu d'un événement Click.
/// L'avantage : la logique reste dans le ViewModel, pas dans la fenêtre.
///
/// - execute     : le code à exécuter quand on clique.
/// - canExecute  : une condition optionnelle qui active/désactive le bouton.
/// </summary>
public class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Predicate<object?>? _canExecute;

    public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    /// <summary>WPF appelle cette méthode pour savoir si le bouton est cliquable.</summary>
    public bool CanExecute(object? parameter)
    {
        return _canExecute is null || _canExecute(parameter);
    }

    /// <summary>Exécute l'action associée au bouton.</summary>
    public void Execute(object? parameter)
    {
        _execute(parameter);
    }

    /// <summary>
    /// Branché sur CommandManager : WPF réévalue automatiquement CanExecute
    /// quand l'utilisateur interagit avec l'interface (clic, touche...).
    /// </summary>
    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    /// <summary>
    /// Demande tout de suite à WPF de redemander si la commande est disponible.
    ///
    /// Nécessaire quand l'état change SANS interaction de l'utilisateur :
    /// par exemple, dans le diaporama, la touche « → » change de diapositive
    /// et le bouton « Suivant » doit se désactiver sur la dernière. Sans cet
    /// appel, il faudrait bouger la souris pour que WPF s'en aperçoive.
    /// </summary>
    public void RaiseCanExecuteChanged()
    {
        CommandManager.InvalidateRequerySuggested();
    }
}
