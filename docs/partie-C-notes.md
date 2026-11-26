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