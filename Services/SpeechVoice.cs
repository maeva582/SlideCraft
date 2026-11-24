namespace SlideCraft.Services;

/// <summary>
/// Une voix installée dans Windows, telle qu'elle est présentée à
/// l'utilisateur dans la fenêtre « Narration voice ».
///
/// <paramref name="Language"/> est le nom lisible de la langue
/// (« French (France) ») ; <paramref name="CultureName"/> est son code
/// technique (« fr-FR »), utilisé pour choisir une phrase d'essai.
/// </summary>
public sealed record SpeechVoice(
    string Name,
    string Language,
    string CultureName,
    string Gender);
