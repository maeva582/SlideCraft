using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SlideCraft.ViewModels;

/// <summary>
/// Classe de base qui permet à l'interface (XAML) d'être prévenue quand une
/// valeur change. C'est le coeur du fonctionnement "binding" de WPF.
///
/// Les modèles affichés à l'écran (Slide, SlideElement, Presentation...)
/// héritent aussi de cette classe : quand on modifie une propriété depuis le
/// code ou depuis l'interface, le visuel se met à jour tout seul.
/// </summary>
public abstract class ObservableObject : INotifyPropertyChanged
{
    /// <summary>Événement écouté par WPF pour rafraîchir l'écran.</summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Déclenche l'événement en indiquant le nom de la propriété modifiée.</summary>
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Affecte une valeur à un champ et notifie l'interface seulement si la
    /// valeur a réellement changé.
    /// </summary>
    /// <returns>true si la valeur a changé.</returns>
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
