using System;
using System.Collections.Generic;
using System.Linq;
using System.Speech.Synthesis;

namespace SlideCraft.Services;

/// <summary>
/// Narration réelle : elle utilise les voix installées dans Windows
/// (celles de « Paramètres → Heure et langue → Parole »), via la classe
/// <see cref="SpeechSynthesizer"/> du .NET.
///
/// Aucun Internet, aucun serveur, aucune IA : la voix est un moteur de
/// synthèse vocale fourni par Windows. C'est pour cela que cette classe
/// n'existe que sur Windows et qu'elle est exclue des tests automatiques.
/// </summary>
public sealed class NarrationService : INarrationService, IDisposable
{
    /// <summary>Moteur de synthèse, null si aucune voix n'a pu être créée.</summary>
    private readonly SpeechSynthesizer? _synthesizer;

    /// <summary>Explication lisible quand la narration n'est pas disponible.</summary>
    public string ErrorMessage { get; } = string.Empty;

    /// <summary>
    /// Toutes les voix installées dans Windows, avec leur langue. La fenêtre
    /// « Narration voice » les affiche pour que l'utilisateur choisisse celle
    /// qui correspond à la langue de sa présentation.
    /// </summary>
    public IReadOnlyList<SpeechVoice> Voices { get; private set; } = Array.Empty<SpeechVoice>();

    public NarrationService()
    {
        try
        {
            _synthesizer = new SpeechSynthesizer();

            List<InstalledVoice> installed = _synthesizer
                .GetInstalledVoices()
                .Where(candidate => candidate.Enabled)
                .ToList();

            Voices = installed
                .Select(candidate => new SpeechVoice(
                    candidate.VoiceInfo.Name,
                    candidate.VoiceInfo.Culture?.DisplayName ?? "Unknown language",
                    candidate.VoiceInfo.Culture?.Name ?? "en",
                    candidate.VoiceInfo.Gender.ToString()))
                .ToList();

            if (installed.Count == 0)
            {
                _synthesizer.Dispose();
                _synthesizer = null;
                ErrorMessage = "No speech voice is installed on this computer. "
                    + "Add one in Windows settings (Time & Language → Speech).";
                return;
            }

            // La voix choisie la dernière fois est retrouvée par son nom ;
            // sinon on prend la première voix active. Choisir explicitement
            // évite qu'une voix désactivée soit sélectionnée et reste muette.
            string? preferred = NarrationVoiceStore.Load();

            InstalledVoice voice = installed.FirstOrDefault(candidate =>
                    string.Equals(candidate.VoiceInfo.Name, preferred, StringComparison.OrdinalIgnoreCase))
                ?? installed[0];

            _synthesizer.SelectVoice(voice.VoiceInfo.Name);
            _synthesizer.Rate = 0;    // débit normal
            _synthesizer.Volume = 100;

            VoiceName = voice.VoiceInfo.Name;
            IsAvailable = true;
        }
        catch (Exception exception)
        {
            // Un poste sans voix, ou une voix cassée, ne doit jamais empêcher
            // la présentation de démarrer : on se contente d'un diaporama muet.
            _synthesizer = null;
            ErrorMessage = "Voice narration is unavailable: " + exception.Message;
            IsAvailable = false;
            VoiceName = string.Empty;
        }
    }

    public bool IsAvailable { get; }

    public string VoiceName { get; private set; } = string.Empty;

    /// <summary>
    /// Change la voix utilisée et retient le choix pour les prochaines
    /// sessions. Renvoie false si le nom ne correspond à aucune voix
    /// installée (le moteur vocal reste alors sur la voix actuelle).
    /// </summary>
    public bool SelectVoice(string voiceName)
    {
        if (_synthesizer is null || string.IsNullOrWhiteSpace(voiceName))
        {
            return false;
        }

        bool exists = _synthesizer.GetInstalledVoices().Any(candidate =>
            candidate.Enabled
            && string.Equals(candidate.VoiceInfo.Name, voiceName, StringComparison.OrdinalIgnoreCase));

        if (!exists)
        {
            return false;
        }

        try
        {
            _synthesizer.SpeakAsyncCancelAll();
            _synthesizer.SelectVoice(voiceName);
            VoiceName = voiceName;
            NarrationVoiceStore.Save(voiceName);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public bool Speak(string text)
    {
        if (_synthesizer is null || string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        try
        {
            // On coupe la phrase précédente avant de lancer la nouvelle :
            // sinon les diapositives s'empileraient dans la file de lecture.
            _synthesizer.SpeakAsyncCancelAll();
            _synthesizer.SpeakAsync(text);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public void Stop()
    {
        try
        {
            _synthesizer?.SpeakAsyncCancelAll();
        }
        catch (Exception)
        {
            // Arrêter une voix déjà arrêtée n'est pas une erreur à signaler.
        }
    }

    public void Dispose()
    {
        Stop();
        _synthesizer?.Dispose();
    }
}
