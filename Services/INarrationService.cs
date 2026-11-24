namespace SlideCraft.Services;

/// <summary>
/// Le « haut-parleur » du diaporama : il prononce un texte et peut se taire.
///
/// Le ViewModel ne connaît que cette interface, jamais la classe Windows qui
/// utilise réellement les voix du système. C'est ce qui permet de tester la
/// narration sans haut-parleur (avec une doublure qui se contente de noter
/// ce qu'on lui demande de dire).
/// </summary>
public interface INarrationService
{
    /// <summary>Vrai si au moins une voix est installée sur la machine.</summary>
    bool IsAvailable { get; }

    /// <summary>Nom de la voix utilisée (par exemple « Microsoft David »), vide si aucune.</summary>
    string VoiceName { get; }

    /// <summary>
    /// Commence à lire <paramref name="text"/> à voix haute. Une lecture en
    /// cours est interrompue : la nouvelle diapositive ne se superpose pas à
    /// l'ancienne. Renvoie false si la voix n'a pas pu démarrer.
    /// </summary>
    bool Speak(string text);

    /// <summary>Coupe immédiatement la lecture en cours (s'il n'y en a pas, ne fait rien).</summary>
    void Stop();
}
