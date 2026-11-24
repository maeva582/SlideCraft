using SlideCraft.ViewModels;

namespace SlideCraft.Models;

/// <summary>
/// Une proposition de réponse telle qu'elle est affichée pendant le jeu.
///
/// Elle n'existe que pendant la projection : ce n'est pas le contenu du
/// document, seulement l'état de la question en cours.
/// </summary>
public class GameAnswer : ObservableObject
{
    private bool _isRight;
    private bool _isWrong;

    public GameAnswer(int index, string text)
    {
        Index = index;
        Text = text;
    }

    /// <summary>Position de la réponse (0 = première). Sert aussi de paramètre de commande.</summary>
    public int Index { get; }

    /// <summary>Texte affiché.</summary>
    public string Text { get; }

    /// <summary>Lettre affichée devant le texte : A, B, C, D.</summary>
    public string Letter => ((char)('A' + Index)).ToString();

    /// <summary>
    /// Vrai si c'est LA bonne réponse, mais seulement après la révélation :
    /// avant, toutes les propositions restent grises (sinon le public
    /// verrait la solution).
    /// </summary>
    public bool IsRight
    {
        get => _isRight;
        set => SetProperty(ref _isRight, value);
    }

    /// <summary>Vrai si c'est la réponse choisie et qu'elle est fausse.</summary>
    public bool IsWrong
    {
        get => _isWrong;
        set => SetProperty(ref _isWrong, value);
    }
}
