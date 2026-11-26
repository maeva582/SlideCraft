# SlideCraft

[![Build Windows](https://github.com/maeva582/SlideCraft/actions/workflows/build.yml/badge.svg?branch=feat/qualite-ci)](https://github.com/maeva582/SlideCraft/actions/workflows/build.yml)

**SlideCraft** est un logiciel de présentation de bureau écrit en **C# / WPF** (.NET 11).
Il permet de créer des diapositives, d'y insérer du texte, des images et des
vidéos, de les projeter en plein écran, d'y ajouter des notes de présentateur,
des sous-titres bilingues, un quiz jouable en salle, d'ouvrir un fichier
PowerPoint existant et d'exporter le tout vers PowerPoint.

Le projet est **100 % hors ligne** : aucun serveur, aucune API, aucune IA
externe, aucune télémétrie. Tout est stocké dans des fichiers locaux.

> Ce README décrit le projet et son lancement. Les règles de travail à quatre
> (branches, pull requests, répartition des fichiers) sont dans
> [CONTRIBUTING.md](CONTRIBUTING.md).

---

## Sommaire

- [Fonctionnalités](#fonctionnalités)
- [Prérequis](#prérequis)
- [Lancer le projet](#lancer-le-projet)
- [Structure du projet](#structure-du-projet)
- [Formats de fichiers](#formats-de-fichiers)
- [Raccourcis clavier](#raccourcis-clavier)
- [Limites connues](#limites-connues)

---

## Fonctionnalités

| Domaine                | Ce que l'application sait faire                                                                                                                                                                                                                                                                                                                |
| ---------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Édition                | Ajouter, supprimer, dupliquer et réordonner des diapositives ; choisir un arrière-plan ; zoomer dans l'éditeur ; appliquer des dispositions (titre, titre + contenu, vide…).                                                                                                                                                                   |
| Contenu                | Insérer du texte, des images, des **vidéos**, des formes et des tableaux ; déplacer, redimensionner, mettre en forme ; copier/coller un élément ; annuler et rétablir (Undo/Redo). Une vidéo joue en boucle et sans son dans l'éditeur, avec le son pendant le diaporama ; elle est **recopiée dans un dossier `nom_media` à côté du projet**. |
| Fichiers               | Enregistrer et ouvrir une présentation au format `.scraft` (XML lisible) ; exporter une copie en `.pptx` (textes, images, vidéos, formes, tableaux) ; **importer un fichier `.pptx`** (textes, images, vidéos, tableaux, fonds, notes).                                                                                                        |
| Diaporama              | Projection plein écran, navigation au clavier, affichage des sous-titres en bas de l'écran.                                                                                                                                                                                                                                                    |
| Transitions            | Effet d'entrée par diapositive (**None, Fade, Push, Wipe**), réellement joué pendant le diaporama ; « Apply to All » pour toutes les diapositives ; enregistré dans le `.scraft` et exporté en `.pptx`.                                                                                                                                        |
| Onglets Review et View | Review : rapport SmartSlide, résumé de revue (nombre de mots, temps de lecture estimé, notes manquantes, historique des modifications avec Undo/Redo). View : zoom réel (100 %, +, −) et panneaux miniatures / propriétés / notes réellement masquables.                                                                                       |
| SmartSlide             | Analyse automatique des diapositives (texte trop long, chevauchements, contraste…) avec des conseils.                                                                                                                                                                                                                                          |
| Narration              | Synthèse vocale hors ligne via les voix Windows (`System.Speech`) : lecture d'un texte de narration écrit par l'utilisateur. La voix se choisit dans **Slide Show → Narration Voice**, avec sa langue et une phrase d'essai.                                                                                                                   |
| Aide                   | Onglet **Help → Quick Guide** : un guide court en anglais simple qui explique à quoi sert chaque onglet, chaque fonction et chaque raccourci.                                                                                                                                                                                                  |
| Quiz et jeu            | Création de questions à choix multiples par diapositive, mode « jeu » avec joueurs et votes **sur le PC du présentateur**, résultats calculés localement.                                                                                                                                                                                      |
| Sous-titres            | Deux langues saisies à la main (pas de traduction automatique), affichées pendant la projection.                                                                                                                                                                                                                                               |
| Notes                  | Notes personnelles, conseil du coach, texte de narration et durée cible par diapositive.                                                                                                                                                                                                                                                       |
| Export texte           | Après chaque export `.pptx`, un fichier `nom-notes.txt` est écrit à côté : notes, conseil, narration et sous-titres de toutes les diapositives.                                                                                                                                                                                                |

L'interface est **entièrement en anglais** ; les commentaires du code et cette
documentation sont en français.

---

## Prérequis

- **Windows 10 ou 11** — WPF ne s'exécute que sous Windows.
- **SDK .NET 11** (11.0.100 ou version ultérieure, preview acceptée) :
  <https://dotnet.microsoft.com/download/dotnet/11.0>
- Un éditeur :
  - **Visual Studio** avec la charge de travail « .NET desktop development », ou
  - **VS Code** avec l'extension **C# Dev Kit**.

Vérifier l'installation :

```powershell
dotnet --version
```

Les dépendances NuGet sont restaurées automatiquement :

- `DocumentFormat.OpenXml` 3.3.0 — lecture/écriture des fichiers `.pptx` ;
- `System.Speech` 9.0.0 — synthèse vocale hors ligne.

---

## Lancer le projet

Depuis la racine du dépôt (le dossier qui contient `SlideCraft.csproj`) :

```powershell
# 1. Restaurer les paquets
dotnet restore SlideCraft.csproj

# 2. Compiler
dotnet build SlideCraft.csproj

# 3. Lancer
dotnet run --project SlideCraft.csproj
```

Pour créer un exécutable distribuable :

```powershell
dotnet publish SlideCraft.csproj -c Release -r win-x64 --self-contained false
```

Le résultat se trouve dans `bin/Release/net11.0-windows/win-x64/publish/`.

### Vérification sous Linux / macOS

Le code peut être **compilé** (pas exécuté) sans Windows :

```bash
dotnet build SlideCraft.csproj -p:EnableWindowsTargeting=true
```

Cela valide la syntaxe et les types, mais ne remplace pas un test réel sous
Windows : l'interface ne peut pas s'afficher.

---

## Structure du projet

```
SlideCraft/
├── App.xaml / App.xaml.cs        Point d'entrée WPF (StartupUri → MainWindow)
├── SlideCraft.csproj             Cible net11.0-windows + paquets NuGet
├── Models/                       Données pures, sans interface
│   ├── Presentation.cs           Titre + collection de diapositives
│   ├── Slide.cs                  Fond, éléments, notes, quiz, sous-titres
│   ├── SlideElement.cs           Classe de base (position, taille, nom)
│   ├── TextElement.cs            Texte (contenu, police, taille, couleur)
│   ├── ImageElement.cs           Image (chemin local, dimensions)
│   ├── VideoElement.cs           Vidéo (chemin local, jouée dans le diaporama)
│   ├── ShapeElement.cs           Forme (rectangle, ellipse, couleur, coins)
│   ├── TableElement.cs / TableCell.cs   Tableau et ses cellules
│   ├── PresenterNotes.cs         Notes, conseil, narration, durée cible
│   ├── Quiz.cs / QuizQuestion.cs / GamePlayer.cs / GameAnswer.cs
│   ├── SlideSubtitles.cs         Deux langues de sous-titres
│   ├── SlideLayout.cs            Dispositions prédéfinies
│   ├── SlideTransition.cs        Transitions (None, Fade, Push, Wipe)
│   ├── SlideAnalysis.cs / SlideIssue.cs   Résultats de SmartSlide
│   └── SubtitleMode.cs           Mode d'affichage des sous-titres
├── ViewModels/                   Logique de présentation (MVVM)
│   ├── ObservableObject.cs       Base INotifyPropertyChanged
│   ├── MainViewModel.cs          Éditeur : diapositives, éléments, fichiers
│   ├── SlideShowViewModel.cs     Projection : navigation, minuteur, sous-titres
│   ├── QuizEditorViewModel.cs    Édition du quiz
│   ├── NarrationVoiceViewModel.cs Choix de la voix et de sa langue
│   ├── SubtitleEditorViewModel.cs Édition des deux langues
│   ├── SmartSlideViewModel.cs    Analyse et liste des problèmes
│   └── ReviewViewModel.cs        Résumé de revue (mots, temps, historique)
├── Views/                        XAML uniquement (pas de logique métier)
│   ├── MainWindow.xaml(.cs)      Fenêtre principale, rubans, Backstage
│   ├── SlideShowWindow.xaml(.cs) Fenêtre de projection
│   ├── QuizEditorWindow.xaml(.cs)
│   ├── SubtitleEditorWindow.xaml(.cs)
│   ├── SmartSlideWindow.xaml(.cs)
│   ├── NarrationVoiceWindow.xaml(.cs) Choix de la voix de narration
│   ├── ReviewWindow.xaml(.cs)    Résumé de revue (onglet Review)
│   ├── HelpWindow.xaml(.cs)      Guide rapide (onglet Help)
│   └── GameSetupWindow.xaml(.cs) Configuration des joueurs
├── Services/                     Tout ce qui n'est ni donnée ni interface
│   ├── PresentationFileService.cs  Lecture/écriture `.scraft`
│   ├── MediaLibrary.cs             Copie des vidéos dans `nom_media`, chemins relatifs
│   ├── PowerPointExporter.cs       Génération `.pptx` (Open XML)
│   ├── PowerPointImporter.cs       Lecture d'un `.pptx` → diapositives SlideCraft
│   ├── NotesExporter.cs            Génération `nom-notes.txt`
│   ├── PresentationFactory.cs      Présentation vide et démo de 3 diapositives
│   ├── PresentationSnapshot.cs     Copie pour l'Undo/Redo
│   ├── UndoRedoService.cs          Piles d'annulation
│   ├── SmartSlideAnalyzer.cs       Règles d'analyse des diapositives
│   ├── NarrationTextBuilder.cs     Texte de narration par défaut
│   ├── NarrationService.cs         Synthèse vocale Windows (liste des voix, choix)
│   ├── NarrationVoiceStore.cs      Mémorisation de la voix choisie
│   ├── NarrationDemoText.cs        Phrase d'essai dans la langue de la voix
│   ├── SpeechVoice.cs              Une voix installée (nom, langue, genre)
│   ├── GameSession.cs              Déroulé d'une partie de quiz
│   ├── ReviewService.cs            Ouverture du résumé de revue
│   └── I*Service.cs                Interfaces (dialogue, quiz, sous-titres…)
├── Commands/
│   └── RelayCommand.cs           ICommand minimal pour les boutons XAML
└── Resources/
    ├── Theme.xaml                Styles et couleurs de l'application
    └── Converters.cs             Convertisseurs XAML (bool → visibilité…)
```

Règle d'architecture : **aucune logique métier dans les `*.xaml.cs`**. Les
fichiers code-behind ne font que le strict nécessaire (glisser-déposer,
fermeture de fenêtre, focus), le reste est dans les ViewModels et les Services.

---

## Formats de fichiers

### `.scraft` — format de travail

Fichier **XML** lisible, enregistré par `File → Save` / `Save as…`. Il contient
le titre, l'arrière-plan, les éléments, les notes, le quiz et les sous-titres.
C'est le seul format à utiliser pendant le développement : il est rouvert sans
perte.

### `.pptx` — export PowerPoint

`File → Export` crée une **copie** `.pptx` ; le `.scraft` d'origine n'est pas
modifié. L'export reproduit les textes, images, formes et tableaux, ainsi que
les transitions Fade / Push / Wipe. Il ne gère pas les animations d'objets.

Les **vidéos** sont réellement intégrées à l'archive `.pptx` (pas seulement
un lien) : le fichier est copié dans `media/` et la diapositive contient une
forme `p:pic` avec `a:videoFile` (relation `video`) et `p14:media` (relation
`media`), exactement comme un fichier produit par PowerPoint. Une image
d'attente sombre est affichée avant la lecture.

### Importer un `.pptx` existant

`Insert → PowerPoint` (ou `File → Import PowerPoint`) lit un fichier créé dans
PowerPoint et le convertit en diapositives SlideCraft : textes, images,
vidéos, tableaux, couleurs de fond et notes du présentateur. Le résultat n'a
pas de fichier `.scraft` : il est marqué comme modifié, et `Ctrl+S` propose de
l'enregistrer.

Les vidéos intégrées au `.pptx` sont extraites et deviennent de vraies vidéos
SlideCraft (elles jouent dans le diaporama) ; l'affiche de la vidéo n'est pas
importée en double comme image.

Détails de la conversion :

- le **titre interne** de la diapositive est le vrai titre PowerPoint (zone
  `title` / `ctrTitle`), et seulement à défaut le premier texte de la page ;
- les **groupes d'objets** sont parcourus récursivement, avec conversion de
  leurs coordonnées et de leur échelle (un groupe mal formé ne fait plus
  disparaître ses enfants) ;
- les **sauts de ligne** internes à un paragraphe sont conservés (deux mots
  ne se collent plus) ;
- les transitions **Fade, Push et Wipe** sont reprises ; les autres retombent
  sur « aucune transition ».

Ce qui n'est **pas** repris : animations d'objets, graphiques et SmartArt. La
diapositive est alors convertie sans ces effets, plutôt que refusée. Les
images et les vidéos sont recopiées dans
`%LocalAppData%\SlideCraft\imported` pour que la présentation reste complète
même si le `.pptx` d'origine est déplacé ; au premier enregistrement en
`.scraft`, les vidéos sont ensuite copiées dans le dossier `nom_media`.

### `nom-notes.txt` — notes et sous-titres

Écrit **automatiquement** après un export `.pptx` réussi, dans le même dossier
et avec le même nom de base (`demo.pptx` → `demo-notes.txt`), en UTF-8 avec BOM
et fins de ligne Windows. Exemple de bloc :

```
============================================================
Slide 2: Why offline matters
============================================================
Time limit: 60 s
Presenter notes:
  Insist on the privacy argument.
Coach tip:
  Slow down on this slide.
Narration:
  (none)
Subtitles (English):
  Your data never leaves this computer.
Subtitles (French):
  Vos données ne quittent jamais cet ordinateur.
```

Si un champ est vide, le fichier l'indique explicitement (`(none)`,
`No presenter notes on this slide.`). Le quiz n'est pas encore inclus dans ce
fichier texte.

---

## Raccourcis clavier

| Raccourci               | Action                          |
| ----------------------- | ------------------------------- |
| `Ctrl + N`              | Nouvelle présentation           |
| `Ctrl + O`              | Ouvrir un `.scraft`             |
| `Ctrl + S`              | Enregistrer                     |
| `Ctrl + Z` / `Ctrl + Y` | Annuler / Rétablir              |
| `Suppr`                 | Supprimer l'élément sélectionné |
| `F5`                    | Démarrer le diaporama           |
| `Échap`                 | Quitter le diaporama            |

---

## Limites connues

- **Windows uniquement** : WPF n'a pas d'équivalent fonctionnel sous Linux.
- **Pas de tests automatisés dans le dépôt** : la validation se fait par une
  campagne manuelle sous Windows (voir CONTRIBUTING.md).
- **Pas de test visuel en CI** : une machine Linux peut compiler, pas afficher.
- Le `.txt` de notes est **réécrit** à chaque export (pas de fusion).
- Le quiz n'est pas exporté dans le `.pptx` ni dans le `.txt`.
- Seules les transitions **Fade, Push et Wipe** existent (aucune autre
  transition PowerPoint) ; les animations d'objets et les thèmes PowerPoint ne
  sont ni exportés ni importés.
- L'import `.pptx` reprend textes, images, vidéos, tableaux, fonds, notes,
  titres, groupes et ces trois transitions ; il ignore graphiques, SmartArt et
  formes sans texte.
- Une vidéo n'est **pas** stockée dans le `.scraft` (le XML deviendrait
  énorme). Elle est copiée dans un dossier voisin `nom_media`, et le `.scraft`
  retient un chemin relatif : déplacez le `.scraft` **avec** son dossier
  `_media` et tout continue de fonctionner. Si vous ne déplacez que le
  `.scraft`, la vidéo n'est plus trouvée (les autres éléments restent).
- PowerPoint n'a pas pu être lancé pour valider le `.pptx` exporté : la
  structure est conforme (archive, relations, content-types) et se relit avec
  le SDK Open XML, mais l'ouverture réelle dans PowerPoint reste à tester.
- Le choix de la voix de narration est enregistré par machine, pas dans la
  présentation : un nom de voix n'existe que sur l'ordinateur qui l'a installée.
