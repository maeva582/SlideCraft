using SlideCraft.Models;

namespace SlideCraft.Services;

/// <summary>
/// Fabrique des présentations de démonstration.
///
/// Pourquoi un service ? Pour ne pas écrire de données de test dans le
/// ViewModel. À l'étape "Nouvelle présentation", cette classe servira aussi
/// à créer une présentation vide.
/// </summary>
public static class PresentationFactory
{
    /// <summary>Crée une présentation vide avec une seule diapositive.</summary>
    public static Presentation CreateEmptyPresentation()
    {
        var presentation = new Presentation { Title = "Untitled presentation" };
        var slide = new Slide { Title = "Slide 1", BackgroundColor = "#FFFFFF" };
        presentation.Slides.Add(slide);
        return presentation;
    }

    /// <summary>
    /// Crée la présentation de démonstration affichée au démarrage.
    /// Elle sert à vérifier que l'interface fonctionne (miniatures, texte,
    /// formes, tableau).
    /// </summary>
    public static Presentation CreateSamplePresentation()
    {
        var presentation = new Presentation { Title = "Welcome to SlideCraft" };

        // ---- Diapositive 1 : page de titre ----
        var titleSlide = new Slide { Title = "Title", BackgroundColor = "#111827" };
        titleSlide.Elements.Add(new ShapeElement
        {
            Name = "Accent bar",
            X = 70, Y = 120, Width = 120, Height = 8,
            Fill = "#6366F1", Stroke = "#6366F1", CornerRadius = 4
        });
        titleSlide.Elements.Add(new TextElement
        {
            Name = "Main title",
            X = 70, Y = 150, Width = 820, Height = 110,
            Text = "SlideCraft",
            FontSize = 60, FontWeight = "Bold", Foreground = "#F9FAFB"
        });
        titleSlide.Elements.Add(new TextElement
        {
            Name = "Subtitle",
            X = 74, Y = 270, Width = 760, Height = 70,
            Text = "Your own presentation software",
            FontSize = 24, Foreground = "#A5B4FC"
        });
        titleSlide.Notes.PersonalNotes = "Welcome the audience and announce the outline of the presentation.";
        titleSlide.Notes.NarrationText =
            "Welcome everyone. Today I will show you SlideCraft, a presentation tool "
            + "that runs entirely on your own computer, without Internet.";

        // Sous-titres de démonstration : deux langues écrites à la main.
        // Pendant la projection, la touche T choisit ce qui s'affiche.
        titleSlide.Subtitles = new SlideSubtitles
        {
            PrimaryLanguage = "English",
            SecondaryLanguage = "French",
            PrimaryText = "Welcome everyone. Today I will show you SlideCraft.",
            SecondaryText = "Bienvenue à tous. Aujourd'hui, je vous présente SlideCraft."
        };
        presentation.Slides.Add(titleSlide);

        // ---- Diapositive 2 : contenu ----
        var contentSlide = new Slide { Title = "About the software", BackgroundColor = "#FFFFFF" };
        contentSlide.Elements.Add(new TextElement
        {
            Name = "Title",
            X = 60, Y = 50, Width = 840, Height = 70,
            Text = "What SlideCraft does",
            FontSize = 36, FontWeight = "Bold", Foreground = "#111827"
        });
        contentSlide.Elements.Add(new TextElement
        {
            Name = "Liste",
            X = 64, Y = 150, Width = 820, Height = 320,
            Text = "• Create and organize slides\n" +
                   "• Add text, pictures and tables\n" +
                   "• Check quality with SmartSlide\n" +
                   "• Rehearse with Presenter Coach",
            FontSize = 24, Foreground = "#374151"
        });
        contentSlide.Notes.PersonalNotes = "Emphasize the original features.";

        // Quiz de démonstration : la question apparaît en fin de diapositive
        // pendant la projection (mode jeu).
        var demoQuiz = new Quiz { Title = "Quick check" };
        var demoQuestion = new QuizQuestion
        {
            Text = "Which SlideCraft feature checks the quality of a slide?",
            CorrectAnswerIndex = 0,
            Points = 10
        };
        demoQuestion.Answers.Add("SmartSlide");
        demoQuestion.Answers.Add("TimeSlide");
        demoQuestion.Answers.Add("DesignGuard");
        demoQuiz.Questions.Add(demoQuestion);
        contentSlide.Quiz = demoQuiz;
        contentSlide.Notes.NarrationText =
            "SlideCraft lets you create and organize slides, add text, pictures and tables, "
            + "check the quality of each slide with SmartSlide, and rehearse your talk "
            + "with Presenter Coach.";

        contentSlide.Subtitles = new SlideSubtitles
        {
            PrimaryLanguage = "English",
            SecondaryLanguage = "French",
            PrimaryText = "SlideCraft works without Internet and saves on your computer.",
            SecondaryText = "SlideCraft fonctionne sans Internet et enregistre sur votre ordinateur."
        };
        presentation.Slides.Add(contentSlide);

        // ---- Diapositive 3 : tableau ----
        var tableSlide = new Slide { Title = "Comparison", BackgroundColor = "#F5F3FF" };
        tableSlide.Elements.Add(new TextElement
        {
            Name = "Title",
            X = 60, Y = 40, Width = 840, Height = 70,
            Text = "SlideCraft at a glance",
            FontSize = 32, FontWeight = "Bold", Foreground = "#312E81"
        });
        var table = new TableElement
        {
            Name = "Feature table",
            X = 60, Y = 140, Width = 700, Height = 240,
            Columns = 3, Rows = 3, FontSize = 18
        };
        table.AddCell("Feature");
        table.AddCell("Purpose");
        table.AddCell("Where?");
        table.AddCell("SmartSlide");
        table.AddCell("Score the quality");
        table.AddCell("Design tab");
        table.AddCell("PresenterCoach");
        table.AddCell("Rehearse the talk");
        table.AddCell("Slide Show tab");
        tableSlide.Elements.Add(table);
        tableSlide.Notes.NarrationText =
            "Here is a quick comparison: SmartSlide scores the quality of a slide, "
            + "and Presenter Coach helps you rehearse from the Slide Show tab.";

        // Deuxième question : de quoi montrer un classement avec plusieurs points.
        var tableQuiz = new Quiz { Title = "Quick check" };
        var tableQuestion = new QuizQuestion
        {
            Text = "On which tab do you start the slide show?",
            CorrectAnswerIndex = 1,
            Points = 20
        };
        tableQuestion.Answers.Add("Design");
        tableQuestion.Answers.Add("Slide Show");
        tableQuestion.Answers.Add("Insert");
        tableQuiz.Questions.Add(tableQuestion);
        tableSlide.Quiz = tableQuiz;
        presentation.Slides.Add(tableSlide);

        return presentation;
    }
}
