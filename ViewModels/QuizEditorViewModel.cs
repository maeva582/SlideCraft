using System;
using System.Linq;
using SlideCraft.Models;

namespace SlideCraft.ViewModels;

/// <summary>
/// Le ViewModel de la petite fenêtre « Quiz ».
///
/// Une diapositive = une question, comme demandé : la question apparaît en
/// fin de diapositive pendant la projection. Quatre emplacements de réponses
/// sont proposés ; les emplacements vides sont simplement ignorés.
///
/// Aucune référence à WPF ici : la validation et la construction du
/// <see cref="Quiz"/> peuvent être testées sans Windows.
/// </summary>
public class QuizEditorViewModel : ObservableObject
{
    /// <summary>Nombre d'emplacements de réponses affichés.</summary>
    public const int MaxAnswers = 4;

    /// <summary>Points proposés par défaut.</summary>
    public const int DefaultPoints = 10;

    private readonly Quiz? _original;
    private readonly string[] _answers = new string[MaxAnswers];

    private string _questionText = string.Empty;
    private int _correctIndex;
    private int _points = DefaultPoints;
    private string _errorMessage = string.Empty;

    public QuizEditorViewModel(Quiz? quiz, string slideTitle)
    {
        // Un tableau de string vaut null par défaut : on met des chaînes vides
        // pour que la fenêtre ne reçoive jamais null dans ses zones de texte.
        Array.Fill(_answers, string.Empty);

        _original = quiz;
        SlideTitle = string.IsNullOrWhiteSpace(slideTitle) ? "this slide" : slideTitle;

        QuizQuestion? question = quiz?.Questions.FirstOrDefault();

        if (question is null)
        {
            // Proposition de départ : on ne laisse pas l'utilisateur devant
            // quatre champs vides sans modèle.
            _questionText = "What did you learn on this slide?";
            _correctIndex = 0;
            return;
        }

        _questionText = question.Text ?? string.Empty;
        _points = Math.Clamp(question.Points, 0, 1000);
        _correctIndex = Math.Clamp(question.CorrectAnswerIndex, 0, MaxAnswers - 1);

        for (int i = 0; i < MaxAnswers && i < question.Answers.Count; i++)
        {
            _answers[i] = question.Answers[i] ?? string.Empty;
        }
    }

    /// <summary>Titre de la diapositive concernée, affiché en haut de la fenêtre.</summary>
    public string SlideTitle { get; }

    /// <summary>Vrai si la diapositive avait déjà un quiz (bouton « Remove quiz »).</summary>
    public bool HasExistingQuiz => _original is not null;

    /// <summary>Énoncé de la question.</summary>
    public string QuestionText
    {
        get => _questionText;
        set
        {
            if (SetProperty(ref _questionText, value))
            {
                ErrorMessage = string.Empty;
            }
        }
    }

    public string Answer1
    {
        get => _answers[0];
        set => SetAnswer(0, value);
    }

    public string Answer2
    {
        get => _answers[1];
        set => SetAnswer(1, value);
    }

    public string Answer3
    {
        get => _answers[2];
        set => SetAnswer(2, value);
    }

    public string Answer4
    {
        get => _answers[3];
        set => SetAnswer(3, value);
    }

    /// <summary>Nombre de réponses réellement remplies.</summary>
    public int FilledAnswerCount => _answers.Count(text => !string.IsNullOrWhiteSpace(text));

    /// <summary>
    /// Index (0 à 3) de la bonne réponse. Les quatre cases « correct » sont
    /// liées aux propriétés IsAnswerXCorrect, qui écrivent ici.
    /// </summary>
    public int CorrectAnswerIndex
    {
        get => _correctIndex;
        set
        {
            int limited = Math.Clamp(value, 0, MaxAnswers - 1);

            if (SetProperty(ref _correctIndex, limited))
            {
                OnPropertyChanged(nameof(IsAnswer1Correct));
                OnPropertyChanged(nameof(IsAnswer2Correct));
                OnPropertyChanged(nameof(IsAnswer3Correct));
                OnPropertyChanged(nameof(IsAnswer4Correct));
            }
        }
    }

    public bool IsAnswer1Correct
    {
        get => _correctIndex == 0;
        set => SetCorrect(0, value);
    }

    public bool IsAnswer2Correct
    {
        get => _correctIndex == 1;
        set => SetCorrect(1, value);
    }

    public bool IsAnswer3Correct
    {
        get => _correctIndex == 2;
        set => SetCorrect(2, value);
    }

    public bool IsAnswer4Correct
    {
        get => _correctIndex == 3;
        set => SetCorrect(3, value);
    }

    /// <summary>Points donnés au joueur qui répond juste (0 à 1000).</summary>
    public int Points
    {
        get => _points;
        set
        {
            if (SetProperty(ref _points, Math.Clamp(value, 0, 1000)))
            {
                ErrorMessage = string.Empty;
            }
        }
    }

    /// <summary>Explication affichée en rouge quand la saisie est incomplète.</summary>
    public string ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    /// <summary>
    /// Vérifie la saisie. Renvoie false et remplit <see cref="ErrorMessage"/>
    /// si quelque chose manque : on ne ferme jamais la fenêtre en perdant
    /// silencieusement la question.
    /// </summary>
    public bool Validate()
    {
        if (string.IsNullOrWhiteSpace(_questionText))
        {
            ErrorMessage = "Write the question first.";
            return false;
        }

        if (FilledAnswerCount < 2)
        {
            ErrorMessage = "Give at least two answers.";
            return false;
        }

        // Les réponses doivent être remplies de haut en bas : un trou au
        // milieu produirait un ordre surprenant dans le diaporama.
        bool emptySeen = false;

        for (int i = 0; i < MaxAnswers; i++)
        {
            bool filled = !string.IsNullOrWhiteSpace(_answers[i]);

            if (!filled)
            {
                emptySeen = true;
            }
            else if (emptySeen)
            {
                ErrorMessage = "Fill the answers from top to bottom, without an empty line in between.";
                return false;
            }
        }

        if (string.IsNullOrWhiteSpace(_answers[_correctIndex]))
        {
            ErrorMessage = "Mark one of the filled answers as correct.";
            return false;
        }

        ErrorMessage = string.Empty;
        return true;
    }

    /// <summary>
    /// Construit un quiz neuf (une question) à partir de la saisie.
    /// Les réponses vides sont retirées et l'index de la bonne réponse est
    /// recalculé en conséquence.
    /// </summary>
    public Quiz BuildQuiz()
    {
        var quiz = new Quiz { Title = "Quiz" };
        var question = new QuizQuestion
        {
            Text = (_questionText ?? string.Empty).Trim(),
            Points = Math.Clamp(_points, 0, 1000),
            CorrectAnswerIndex = 0
        };

        int compactIndex = -1;

        for (int i = 0; i < MaxAnswers; i++)
        {
            string text = (_answers[i] ?? string.Empty).Trim();

            if (text.Length == 0)
            {
                continue;
            }

            compactIndex++;

            if (i == _correctIndex)
            {
                question.CorrectAnswerIndex = compactIndex;
            }

            question.Answers.Add(text);
        }

        quiz.Questions.Add(question);
        return quiz;
    }

    /// <summary>Efface la saisie (bouton « Clear »).</summary>
    public void Clear()
    {
        QuestionText = string.Empty;

        for (int i = 0; i < MaxAnswers; i++)
        {
            SetAnswer(i, string.Empty);
        }

        Points = DefaultPoints;
        CorrectAnswerIndex = 0;
        ErrorMessage = string.Empty;
    }

    private void SetAnswer(int index, string? value)
    {
        string text = value ?? string.Empty;

        if (_answers[index] == text)
        {
            return;
        }

        _answers[index] = text;
        OnPropertyChanged($"Answer{index + 1}");
        OnPropertyChanged(nameof(FilledAnswerCount));
        ErrorMessage = string.Empty;
    }

    private void SetCorrect(int index, bool value)
    {
        // Un clic sur la case déjà cochée ne doit pas tout décocher :
        // il y a toujours exactement une bonne réponse.
        if (value)
        {
            CorrectAnswerIndex = index;
        }
    }
}
