using System;
using System.IO;

namespace SlideCraft.Services;

/// <summary>
/// Retient la voix choisie par l'utilisateur, d'une session à l'autre.
///
/// Le choix est enregistré dans un petit fichier texte du profil Windows,
/// pas dans la présentation : le nom d'une voix (« Microsoft Hortense »)
/// n'existe que sur la machine où elle est installée.
/// </summary>
public static class NarrationVoiceStore
{
    /// <summary>Chemin du fichier de réglage.</summary>
    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SlideCraft",
        "narration-voice.txt");

    /// <summary>Nom de la voix choisie, null si l'utilisateur n'a jamais choisi.</summary>
    public static string? Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return null;
            }

            string name = File.ReadAllText(FilePath).Trim();
            return name.Length == 0 ? null : name;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// Enregistre la voix choisie. Un échec d'écriture n'empêche jamais la
    /// narration de fonctionner : le choix vaut simplement pour cette session.
    /// </summary>
    public static void Save(string voiceName)
    {
        try
        {
            string folder = Path.GetDirectoryName(FilePath)!;
            Directory.CreateDirectory(folder);
            File.WriteAllText(FilePath, voiceName);
        }
        catch (IOException)
        {
            // Réglage non enregistré : ce n'est pas une raison pour bloquer.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
