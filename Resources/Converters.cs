using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SlideCraft.Resources;

/// <summary>
/// Ajoute 1 à un nombre. Utilisé pour afficher le numéro des miniatures
/// (la liste commence à 0, l'utilisateur veut voir 1, 2, 3...).
/// </summary>
public class IndexPlusOneConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is int index ? (index + 1).ToString() : string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
/// Donne la largeur d'une colonne de panneau selon qu'il est affiché ou non.
///
/// L'onglet View peut masquer les miniatures ou le panneau des propriétés.
/// Une simple Visibility ne suffirait pas : la colonne garderait sa largeur
/// et laisserait un grand vide. Le paramètre du convertisseur est la largeur
/// en pixels à utiliser quand le panneau est visible ("196", "272"...).
/// </summary>
public class PanelWidthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool visible = value is not false;

        double width = parameter is string text
            && double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out double parsed)
            ? parsed
            : 0;

        return visible ? new GridLength(width) : new GridLength(0);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
/// Affiche l'élément quand la valeur est FAUSSE, le cache quand elle est vraie.
/// Utile pour le message "Aucun élément sélectionné".
/// </summary>
public class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool isTrue = value is true;
        return isTrue ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
