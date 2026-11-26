# Notes partie C — Diaporama, quiz, sous-titres, narration

## SubtitleMode (Models/SubtitleMode.cs)
- C'est une `enum` : une liste de choix fixes.
- Les 4 choix : Both (deux langues), PrimaryOnly, SecondaryOnly, Hidden.
- La touche T du diaporama passe de l'un à l'autre.

## ObservableObject (ViewModels/ObservableObject.cs)
- C'est la classe de base de presque tous les objets du projet.
- Elle prévient l'écran quand une valeur change.
- `SetProperty` enregistre la nouvelle valeur, puis prévient l'écran.
- Si la valeur est identique à l'ancienne, elle ne fait rien.


## QuizQuestion (Models/QuizQuestion.cs)
- Une question de quiz : énoncé (`Text`), liste de réponses (`Answers`), index de la bonne réponse (`CorrectAnswerIndex`) et points (`Points`).
- Les index commencent à 0 : la première réponse a l'index 0.
- `Points` est limité entre 0 et 1000 grâce à `Math.Clamp`.
- `QuizQuestion` hérite de `ObservableObject`, donc l'écran se met à jour quand une valeur change.
- `Clone()` fabrique une copie indépendante. L'éditeur de quiz travaille sur la copie : si on annule, l'original n'a pas bougé.


## GameSession (Services/GameSession.cs)
- C'est le « cerveau » du mode jeu pour UNE projection. Il ne connaît ni WPF ni la souris : on peut donc le tester sans Windows.
- Fonctionnement hors ligne : la salle vote à main levée, le présentateur clique la réponse (ou tape 1 à 4), puis clique le nom du joueur qui a répondu.
- `SetPlayers` : retire les noms vides et les doublons (sans tenir compte des majuscules).
- `LoadQuestion` : appelée à chaque changement de diapositive. Elle ignore les propositions vides et recalcule l'index de la bonne réponse. Elle ne referme pas le classement (seule la touche L le fait).
- `SelectAnswer(index)` : enregistre la réponse de la salle et révèle la bonne. Elle renvoie false si la question est déjà répondue ou si l'index est invalide. Sans joueur, les points vont à la salle (`_roomScore`).
- `Award(player)` : donne les points à UN seul joueur par question, pour éviter qu'un clic en trop double le score.
- `RefreshLeaderboard` : classement trié par score décroissant, puis par nom.
- `ToggleLeaderboard` : ouvre ou ferme le classement (touche L).