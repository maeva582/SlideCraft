namespace SlideCraft.Services;

/// <summary>
/// Phrase prononcée par le bouton « Hear this voice ».
///
/// On choisit une phrase dans la langue de la voix : une voix française qui
/// lit un texte anglais donne un accent incompréhensible, et l'utilisateur
/// croirait que la voix est cassée. C'est exactement le problème que la
/// fenêtre de choix de voix explique.
/// </summary>
public static class NarrationDemoText
{
    /// <summary>Phrase d'essai adaptée à la langue de <paramref name="voice"/>.</summary>
    public static string For(SpeechVoice? voice)
    {
        string culture = voice?.CultureName ?? "en";

        return culture.ToLowerInvariant() switch
        {
            var c when c.StartsWith("fr") =>
                "Bonjour. Je suis la voix qui lira vos diapositives. Choisissez-moi "
                + "si votre présentation est en français.",
            var c when c.StartsWith("es") =>
                "Hola. Soy la voz que leerá sus diapositivas. Elíjame si su "
                + "presentación está en español.",
            var c when c.StartsWith("de") =>
                "Hallo. Ich bin die Stimme, die Ihre Folien vorliest. Wählen Sie "
                + "mich, wenn Ihre Präsentation auf Deutsch ist.",
            var c when c.StartsWith("it") =>
                "Ciao. Sono la voce che leggerà le vostre diapositive. Sceglietemi "
                + "se la presentazione è in italiano.",
            var c when c.StartsWith("pt") =>
                "Olá. Sou a voz que vai ler os seus diapositivos. Escolha-me se a "
                + "apresentação for em português.",
            _ =>
                "Hello. I am the voice that will read your slides aloud. Choose me "
                + "if your presentation is in English."
        };
    }
}
