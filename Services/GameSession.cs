using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using SlideCraft.Models;
using SlideCraft.ViewModels;

namespace SlideCraft.Services;

/// <summary>
/// Le « cerveau » du mode jeu, pour UNE projection.
///
/// Il ne connaît ni WPF ni la souris : il reçoit une question, une réponse
/// cliquée et un joueur, puis calcule le score et le classement. C'est pour
/// cela qu'on peut le tester sans Windows.
///
/// Fonctionnement hors ligne (choix retenu) :
///   1. la salle vote à main levée ;
///   2. le présentateur clique la réponse choisie (ou tape 1-4) ;
///   3. s'il y a des joueurs, il clique le nom de celui qui a répondu.
/// Aucun téléphone, aucun serveur, aucune connexion Internet.
/// </summary>
public class GameSession : ObservableObject
{
    private string _questionText = string.Empty;
    private int _correctIndex = -1;
    private int _points;
    private int _selectedIndex = -1;
    private bool _isAnswered;
    private string _feedback = string.Empty;
    private bool _isLeaderboardVisible;
    private int _questionNumber;
    private int _questionCount;
    private int _roomScore;
    private GamePlayer? _awardedTo;

    /// <summary>
    /// Prépare une partie. <paramref name="playerNames"/> peut être vide :
    /// le quiz fonctionne alors comme un quiz de salle (score global).
    /// </summary>
    public GameSession(IEnumerable<string>? playerNames = null)
    {
        Answers = new ObservableCollection<GameAnswer>();
        Players = new ObservableCollection<GamePlayer>();
        Leaderboard = new ObservableCollection<GamePlayer>();

        SetPlayers(playerNames);
    }

    /// <summary>Joueurs enregistrés, dans l'ordre de saisie.</summary>
    public ObservableCollection<GamePlayer> Players { get; }

    /// <summary>Même joueurs, triés par score décroissant (classement affiché).</summary>
    public ObservableCollection<GamePlayer> Leaderboard { get; }

    /// <summary>Propositions de la question en cours (A, B, C, D).</summary>
    public ObservableCollection<GameAnswer> Answers { get; }

    /// <summary>Vrai si au moins un joueur est enregistré.</summary>
    public bool HasPlayers => Players.Count > 0;

    /// <summary>Vrai si la diapositive courante porte une question jouable.</summary>
    public bool HasQuestion => Answers.Count >= 2;

    /// <summary>Énoncé de la question courante.</summary>
    public string QuestionText => _questionText;

    /// <summary>Vrai dès que la réponse a été enregistrée (révélation).</summary>
    public bool IsAnswered => _isAnswered;

    /// <summary>Vrai si l'on peut encore répondre à cette question.</summary>
    public bool CanAnswer => HasQuestion && !_isAnswered;

    /// <summary>Vrai si la réponse choisie est la bonne.</summary>
    public bool IsCorrect => _isAnswered && _correctIndex >= 0 && _selectedIndex == _correctIndex;

    /// <summary>Message affiché sous la question (bonne réponse, correction...).</summary>
    public string Feedback => _feedback;

    /// <summary>Vrai s'il reste des points à donner à un joueur.</summary>
    public bool CanAward => IsCorrect && HasPlayers && _awardedTo is null;

    /// <summary>Vrai quand le classement est affiché (touche L).</summary>
    public bool IsLeaderboardVisible
    {
        get => _isLeaderboardVisible;
        set => SetProperty(ref _isLeaderboardVisible, value);
    }

    /// <summary>En-tête du panneau : « QUESTION 1 / 2 · 10 POINTS ».</summary>
    public string QuestionHeader
    {
        get
        {
            if (!HasQuestion)
            {
                return string.Empty;
            }

            string total = _questionCount > 0 ? $" {_questionNumber} / {_questionCount}" : $" {_questionNumber}";
            string points = _points == 1 ? "1 POINT" : $"{_points} POINTS";
            return $"QUESTION{total} · {points}";
        }
    }

    /// <summary>Résumé du bas du panneau de jeu.</summary>
    public string ScoreSummary => HasPlayers
        ? $"{Players.Count} player{(Players.Count == 1 ? string.Empty : "s")} · {_questionNumber} question{(_questionNumber == 1 ? string.Empty : "s")} played"
        : $"No players registered · room score {_roomScore}";

    // ------------------------------------------------------------------
    // Joueurs
    // ------------------------------------------------------------------

    /// <summary>
    /// Remplace la liste des joueurs : noms vides ignorés, doublons retirés
    /// (sans tenir compte des majuscules).
    /// </summary>
    public void SetPlayers(IEnumerable<string>? names)
    {
        Players.Clear();

        foreach (string name in CleanNames(names))
        {
            Players.Add(new GamePlayer(name));
        }

        RefreshLeaderboard();
        OnPropertyChanged(nameof(HasPlayers));
        OnPropertyChanged(nameof(ScoreSummary));
        OnPropertyChanged(nameof(CanAward));
    }

    private static IEnumerable<string> CleanNames(IEnumerable<string>? names)
    {
        if (names is null)
        {
            return Array.Empty<string>();
        }

        var seen = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);
        var result = new List<string>();

        foreach (string name in names)
        {
            string trimmed = (name ?? string.Empty).Trim();

            if (trimmed.Length == 0 || !seen.Add(trimmed))
            {
                continue;
            }

            result.Add(trimmed);
        }

        return result;
    }

    // ------------------------------------------------------------------
    // Question en cours
    // ------------------------------------------------------------------

    /// <summary>
    /// Charge la question de la diapositive affichée. Appelée à chaque
    /// changement de diapositive ; une diapositive sans quiz vide le panneau.
    /// </summary>
    /// <param name="questionNumber">Numéro de la question dans la présentation.</param>
    /// <param name="questionCount">Nombre total de questions de la présentation.</param>
    public void LoadQuestion(QuizQuestion? question, int questionNumber, int questionCount)
    {
        Answers.Clear();
        _selectedIndex = -1;
        _isAnswered = false;
        _awardedTo = null;
        _correctIndex = -1;
        _questionText = string.Empty;
        _points = 0;
        _feedback = string.Empty;
        _questionNumber = Math.Max(0, questionNumber);
        _questionCount = Math.Max(0, questionCount);

        // Le classement n'est PAS refermé ici : un présentateur qui l'a
        // ouvert (touche L) veut le garder à l'écran d'une diapositive à
        // l'autre. Seule la touche L le referme.

        if (question is not null)
        {
            _questionText = string.IsNullOrWhiteSpace(question.Text) ? "Question" : question.Text.Trim();
            _points = Math.Max(0, question.Points);

            // On ignore les propositions vides et on recalcule l'index de la
            // bonne réponse : un quiz enregistré avec un trou reste jouable.
            int compactIndex = -1;

            for (int i = 0; i < question.Answers.Count; i++)
            {
                string text = (question.Answers[i] ?? string.Empty).Trim();

                if (text.Length == 0)
                {
                    continue;
                }

                compactIndex++;

                if (i == question.CorrectAnswerIndex)
                {
                    _correctIndex = compactIndex;
                }

                Answers.Add(new GameAnswer(compactIndex, text));
            }
        }

        NotifyQuestionChanged();
    }

    /// <summary>
    /// Enregistre la réponse choisie par la salle et révèle la bonne réponse.
    /// Renvoie false si la question est déjà répondue ou l'index invalide.
    /// </summary>
    public bool SelectAnswer(int index)
    {
        if (!CanAnswer || index < 0 || index >= Answers.Count)
        {
            return false;
        }

        _selectedIndex = index;
        _isAnswered = true;

        foreach (GameAnswer answer in Answers)
        {
            answer.IsRight = answer.Index == _correctIndex;
            answer.IsWrong = answer.Index == index && answer.Index != _correctIndex;
        }

        if (IsCorrect)
        {
            _feedback = _points > 0
                ? $"Correct! +{_points} point{(_points == 1 ? string.Empty : "s")}"
                : "Correct!";

            // Sans joueur enregistré, les points vont à la salle.
            if (!HasPlayers)
            {
                _roomScore += _points;
            }
        }
        else if (_correctIndex >= 0)
        {
            _feedback = $"Not quite. The right answer is {Answers[_correctIndex].Letter}.";
        }
        else
        {
            _feedback = "Not quite. No answer is marked as correct for this question.";
        }

        NotifyQuestionChanged();
        return true;
    }

    /// <summary>
    /// Donne les points de la question à un joueur. Un seul joueur est
    /// crédité par question, sinon un clic malheureux doublerait le score.
    /// </summary>
    public bool Award(GamePlayer? player)
    {
        if (!CanAward || player is null || !Players.Contains(player))
        {
            return false;
        }

        player.AddPoints(_points);
        _awardedTo = player;
        _feedback = _points > 0
            ? $"{player.Name} scores {_points} point{(_points == 1 ? string.Empty : "s")}!"
            : $"{player.Name} scores!";

        RefreshLeaderboard();
        NotifyQuestionChanged();
        return true;
    }

    /// <summary>Ouvre ou ferme le classement (touche L).</summary>
    public void ToggleLeaderboard()
    {
        IsLeaderboardVisible = !IsLeaderboardVisible;
    }

    /// <summary>Reconstruit le classement trié (score décroissant, puis nom).</summary>
    public void RefreshLeaderboard()
    {
        Leaderboard.Clear();

        foreach (GamePlayer player in Players
                     .OrderByDescending(item => item.Score)
                     .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            Leaderboard.Add(player);
        }
    }

    /// <summary>Prévient l'interface que tout ce qui dépend de la question a changé.</summary>
    private void NotifyQuestionChanged()
    {
        OnPropertyChanged(nameof(HasQuestion));
        OnPropertyChanged(nameof(QuestionText));
        OnPropertyChanged(nameof(IsAnswered));
        OnPropertyChanged(nameof(CanAnswer));
        OnPropertyChanged(nameof(IsCorrect));
        OnPropertyChanged(nameof(Feedback));
        OnPropertyChanged(nameof(CanAward));
        OnPropertyChanged(nameof(QuestionHeader));
        OnPropertyChanged(nameof(ScoreSummary));
    }
}
