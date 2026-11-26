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