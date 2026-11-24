using System.Collections.Generic;
using System.Linq;
using SlideCraft.Commands;
using SlideCraft.Services;

namespace SlideCraft.ViewModels;

/// <summary>
/// Le ViewModel de la fenêtre « Narration voice ».
///
/// Il répond à la question que se pose un débutant : « pourquoi la voix parle
/// parfois plusieurs langues à la fois ? ». En réalité, chaque voix installée
/// dans Windows ne parle qu'UNE langue ; l'ancienne version prenait toujours
/// la première voix trouvée, si bien qu'un texte anglais pouvait être lu avec
/// une voix française, ce qui donne un mélange incompréhensible.
///
/// Ici, l'utilisateur voit toutes les voix, leur langue, et choisit celle qui
/// correspond à sa présentation.
/// </summary>
public class NarrationVoiceViewModel : ObservableObject
{
    private readonly NarrationService _narration;
    private SpeechVoice? _selectedVoice;
    private string _previewStatus = string.Empty;

    public NarrationVoiceViewModel(NarrationService narration)
    {
        _narration = narration;
        Voices = narration.Voices;

        _selectedVoice = Voices.FirstOrDefault(voice =>
                             voice.Name == narration.VoiceName)
                         ?? Voices.FirstOrDefault();

        PreviewCommand = new RelayCommand(_ => Preview());
    }

    /// <summary>Les voix installées sur cet ordinateur.</summary>
    public IReadOnlyList<SpeechVoice> Voices { get; }

    /// <summary>Vrai si au moins une voix est installée.</summary>
    public bool HasVoices => Voices.Count > 0;

    /// <summary>Vrai si aucune voix n'est installée (message d'aide affiché).</summary>
    public bool HasNoVoice => Voices.Count == 0;

    /// <summary>Voix sélectionnée dans la liste.</summary>
    public SpeechVoice? SelectedVoice
    {
        get => _selectedVoice;
        set
        {
            if (SetProperty(ref _selectedVoice, value))
            {
                OnPropertyChanged(nameof(SelectedVoiceLabel));
                PreviewStatus = string.Empty;
            }
        }
    }

    /// <summary>Résumé « nom (langue) » de la voix sélectionnée.</summary>
    public string SelectedVoiceLabel => SelectedVoice is null
        ? "No voice selected"
        : $"{SelectedVoice.Name} — {SelectedVoice.Language}";

    /// <summary>Texte d'explication affiché en haut de la fenêtre.</summary>
    public string Explanation =>
        "A voice speaks only one language. SlideCraft reads each slide aloud with "
        + "the voice you pick here: choose a voice whose language matches the text "
        + "of your slides. If an English slide is read by a French voice, you hear "
        + "English words with a French accent, which sounds like a mix of languages.";

    /// <summary>Deuxième explication, plus concrète.</summary>
    public string WhatIsRead =>
        "What is read: the narration script of the slide. If you did not write one, "
        + "SlideCraft reads the title and the text boxes of the slide. Images and "
        + "videos are not described.";

    /// <summary>Message quand aucune voix n'est installée.</summary>
    public string NoVoiceMessage =>
        "No speech voice is installed on this computer. Open Windows Settings, "
        + "Time & Language, Speech, and add a voice. Then reopen this window.";

    /// <summary>Résultat de l'écoute d'essai.</summary>
    public string PreviewStatus
    {
        get => _previewStatus;
        private set => SetProperty(ref _previewStatus, value);
    }

    public RelayCommand PreviewCommand { get; }

    /// <summary>Fait écouter la voix sélectionnée sur une phrase d'essai.</summary>
    public void Preview()
    {
        if (SelectedVoice is null)
        {
            PreviewStatus = "Choose a voice first.";
            return;
        }

        if (!_narration.SelectVoice(SelectedVoice.Name))
        {
            PreviewStatus = "This voice is no longer available. Reopen the window.";
            return;
        }

        PreviewStatus = _narration.Speak(NarrationDemoText.For(SelectedVoice))
            ? "Reading the sample sentence…"
            : "The voice could not start. Check the Windows sound settings.";
    }

    /// <summary>
    /// Applique la voix choisie. Renvoie false quand aucune voix utilisable
    /// n'est sélectionnée (la fenêtre affiche alors un message).
    /// </summary>
    public bool Apply()
    {
        return SelectedVoice is not null && _narration.SelectVoice(SelectedVoice.Name);
    }
}
