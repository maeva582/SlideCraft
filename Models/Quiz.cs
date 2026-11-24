using System;
using System.Collections.ObjectModel;
using SlideCraft.ViewModels;

namespace SlideCraft.Models;

/// <summary>
/// Un quiz rattaché à une diapositive (fonction QuizSlide).
/// Pour cette première étape, les classes existent mais l'interface
/// d'édition du quiz arrivera à l'étape QuizSlide.
/// </summary>
public class Quiz : ObservableObject
{
    private string _title = "Quiz";

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    /// <summary>Liste des questions du quiz.</summary>
    public ObservableCollection<QuizQuestion> Questions { get; } = new();

    /// <summary>
    /// Vrai si les deux quiz ont exactement le même contenu (titre, questions,
    /// propositions, bonne réponse, points).
    ///
    /// Cela évite de marquer le document comme modifié quand on ouvre
    /// l'éditeur de quiz puis qu'on le referme sans rien changer.
    /// </summary>
    public bool SameContentAs(Quiz? other)
    {
        if (other is null)
        {
            return false;
        }

        if (!string.Equals(Title, other.Title, StringComparison.Ordinal)
            || Questions.Count != other.Questions.Count)
        {
            return false;
        }

        for (int i = 0; i < Questions.Count; i++)
        {
            QuizQuestion mine = Questions[i];
            QuizQuestion theirs = other.Questions[i];

            if (mine.Text != theirs.Text
                || mine.CorrectAnswerIndex != theirs.CorrectAnswerIndex
                || mine.Points != theirs.Points
                || mine.Answers.Count != theirs.Answers.Count)
            {
                return false;
            }

            for (int a = 0; a < mine.Answers.Count; a++)
            {
                if (mine.Answers[a] != theirs.Answers[a])
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>Copie indépendante du quiz (duplication, instantané d'annulation).</summary>
    public Quiz Clone()
    {
        var copy = new Quiz { Title = Title };

        foreach (QuizQuestion question in Questions)
        {
            copy.Questions.Add(question.Clone());
        }

        return copy;
    }
}
