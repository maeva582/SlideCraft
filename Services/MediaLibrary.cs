using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SlideCraft.Models;

namespace SlideCraft.Services;

/// <summary>
/// Gère les fichiers médias (aujourd'hui les vidéos) qui accompagnent un
/// projet SlideCraft.
///
/// Pourquoi un dossier à côté du fichier .scraft ? Une vidéo pèse souvent
/// plusieurs mégaoctets : la recopier en base64 dans le XML rendrait le
/// document énorme et lent à ouvrir. On la copie donc dans un dossier voisin
/// (« MaPresentation_media ») et le .scraft ne retient qu'un chemin relatif.
///
/// Résultat : si l'utilisateur déplace le .scraft ET son dossier _media
/// ensemble, la présentation reste lisible, même sur un autre disque.
/// </summary>
public static class MediaLibrary
{
    /// <summary>Suffixe du dossier créé à côté du fichier .scraft.</summary>
    public const string FolderSuffix = "_media";

    /// <summary>
    /// Dossier des vidéos d'un projet : « dossier du .scraft / nom_media ».
    /// </summary>
    public static string FolderFor(string projectPath)
    {
        string full = Path.GetFullPath(projectPath);
        string folder = Path.GetDirectoryName(full) ?? ".";
        string name = Path.GetFileNameWithoutExtension(full);

        return Path.Combine(folder, name + FolderSuffix);
    }

    /// <summary>
    /// Copie <paramref name="sourcePath"/> dans le dossier média du projet et
    /// renvoie le chemin absolu de la copie. Renvoie null si la copie est
    /// impossible (fichier absent, disque plein, droits refusés).
    ///
    /// Une vidéo déjà présente dans le dossier n'est pas recopiée : on la
    /// réutilise, ce qui évite de dupliquer le fichier à chaque enregistrement.
    /// </summary>
    public static string? CopyIntoProject(string sourcePath, string projectPath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || string.IsNullOrWhiteSpace(projectPath))
        {
            return null;
        }

        if (!File.Exists(sourcePath))
        {
            return null;
        }

        try
        {
            string source = Path.GetFullPath(sourcePath);
            string folder = FolderFor(projectPath);
            Directory.CreateDirectory(folder);

            string target = Path.Combine(folder, Path.GetFileName(source));

            // Déjà au bon endroit : rien à faire.
            if (string.Equals(source, Path.GetFullPath(target), StringComparison.OrdinalIgnoreCase))
            {
                return source;
            }

            target = FindAvailableName(target, source);
            File.Copy(source, target, overwrite: false);

            return target;
        }
        catch (Exception exception) when (
            exception is IOException
                or UnauthorizedAccessException
                or NotSupportedException
                or ArgumentException)
        {
            // On préfère garder l'ancien chemin que perdre la vidéo.
            return null;
        }
    }

    /// <summary>
    /// Nom libre dans le dossier : si deux vidéos différentes portent le même
    /// nom de fichier, la seconde devient « video-2.mp4 », etc.
    /// </summary>
    private static string FindAvailableName(string target, string source)
    {
        if (!File.Exists(target))
        {
            return target;
        }

        long sourceSize = new FileInfo(source).Length;

        // Même nom et même taille : c'est très probablement la même vidéo.
        if (new FileInfo(target).Length == sourceSize)
        {
            return target;
        }

        string folder = Path.GetDirectoryName(target) ?? ".";
        string name = Path.GetFileNameWithoutExtension(target);
        string extension = Path.GetExtension(target);

        for (int index = 2; index < 1000; index++)
        {
            string candidate = Path.Combine(folder, $"{name}-{index}{extension}");

            if (!File.Exists(candidate))
            {
                return candidate;
            }

            if (new FileInfo(candidate).Length == sourceSize)
            {
                return candidate;
            }
        }

        // Cas absurde (1000 homonymes) : on écrase le dernier nom.
        return Path.Combine(folder, $"{name}-{Guid.NewGuid():N}{extension}");
    }

    /// <summary>
    /// Chemin écrit dans le .scraft : relatif au dossier du projet quand la
    /// vidéo s'y trouve, absolu sinon (fichier sur un autre disque).
    /// </summary>
    public static string ToStoredPath(string videoPath, string projectPath)
    {
        if (string.IsNullOrWhiteSpace(videoPath) || string.IsNullOrWhiteSpace(projectPath))
        {
            return videoPath ?? string.Empty;
        }

        try
        {
            string fullVideo = Path.GetFullPath(videoPath);
            string projectFolder = Path.GetDirectoryName(Path.GetFullPath(projectPath)) ?? ".";

            string relative = Path.GetRelativePath(projectFolder, fullVideo);

            // Un chemin qui remonte (..\..\) n'apporte rien : on garde l'absolu.
            return relative.StartsWith("..", StringComparison.Ordinal) ? fullVideo : relative;
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return videoPath;
        }
    }

    /// <summary>
    /// Relit un chemin du .scraft : un chemin relatif repart du dossier du
    /// projet, ce qui rend le couple (.scraft + dossier _media) déplaçable.
    /// </summary>
    public static string ResolvePath(string storedPath, string projectPath)
    {
        if (string.IsNullOrWhiteSpace(storedPath) || Path.IsPathRooted(storedPath))
        {
            return storedPath;
        }

        if (string.IsNullOrWhiteSpace(projectPath))
        {
            return storedPath;
        }

        try
        {
            string folder = Path.GetDirectoryName(Path.GetFullPath(projectPath)) ?? ".";
            return Path.GetFullPath(Path.Combine(folder, storedPath));
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return storedPath;
        }
    }

    /// <summary>Toutes les vidéos de la présentation, quelle que soit la diapositive.</summary>
    public static IEnumerable<VideoElement> AllVideos(Presentation presentation)
    {
        return presentation.Slides
            .SelectMany(slide => slide.Elements)
            .OfType<VideoElement>();
    }

    /// <summary>
    /// Appelé juste avant l'enregistrement : chaque vidéo encore stockée
    /// ailleurs est recopiée dans le dossier « _media » du projet.
    /// </summary>
    public static void PrepareVideos(Presentation presentation, string projectPath)
    {
        foreach (VideoElement video in AllVideos(presentation))
        {
            if (string.IsNullOrWhiteSpace(video.VideoPath))
            {
                continue;
            }

            string? copied = CopyIntoProject(video.VideoPath, projectPath);

            if (copied is not null)
            {
                video.VideoPath = copied;
            }
        }
    }

    /// <summary>
    /// Appelé juste après l'ouverture : les chemins relatifs du fichier
    /// redeviennent des chemins absolus utilisables par le lecteur vidéo.
    /// </summary>
    public static void ResolveVideos(Presentation presentation, string projectPath)
    {
        foreach (VideoElement video in AllVideos(presentation))
        {
            video.VideoPath = ResolvePath(video.VideoPath, projectPath);
        }
    }
}
