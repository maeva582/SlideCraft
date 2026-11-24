using System;
using System.Collections.ObjectModel;
using SlideCraft.ViewModels;

namespace SlideCraft.Models;

/// <summary>
/// Une question à choix multiple avec sa bonne réponse.
/// L'index de la bonne réponse commence à 0 (première proposition).
/// </summary>
public class QuizQuestion : ObservableObject
{
    private string _text = "Question ?";
    private int _correctAnswerIndex;
    private int _points = 10;

    /// <summary>Énoncé de la question.</summary>
    public string Text
    {
        get => _text;
        set => SetProperty(ref _text, value);
    }

    /// <summary>Propositions affichées.</summary>
    public ObservableCollection<string> Answers { get; } = new();

    /// <summary>Index (0-based) de la bonne réponse dans <see cref="Answers"/>.</summary>
    public int CorrectAnswerIndex
    {
        get => _correctAnswerIndex;
        set => SetProperty(ref _correctAnswerIndex, value);
    }

    /// <summary>Points donnés au joueur qui répond juste (0 à 1000).</summary>
    public int Points
    {
        get => _points;
        set => SetProperty(ref _points, Math.Clamp(value, 0, 1000));
    }

    /// <summary>Copie indépendante de la question, propositions comprises.</summary>
    public QuizQuestion Clone()
    {
        var copy = new QuizQuestion
        {
            Text = Text,
            CorrectAnswerIndex = CorrectAnswerIndex,
            Points = Points
        };

        foreach (string answer in Answers)
        {
            copy.Answers.Add(answer);
        }

        return copy;
    }
}
