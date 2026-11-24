# Contribuer à SlideCraft — guide d'équipe (4 personnes)

Ce fichier contient **les règles Git/GitHub** et **la répartition du travail**.
Le fonctionnement de l'application est décrit dans [README.md](README.md).

Objectif de la première milestone : **`v1.0 — validation Windows`**, c'est-à-dire
une application qui compile sans avertissement et dont chaque fonctionnalité a
été testée à la main sur un PC Windows.

---

## 1. Mise en place du dépôt (une seule fois)

À faire par **une seule personne** (le responsable du dépôt), puis les autres
rejoignent. Si votre dépôt et vos collaborateurs sont **déjà créés**, allez
directement au §1.e.

> Le backlog complet, prêt à coller dans GitHub, est dans
> [GITHUB-ISSUES.md](GITHUB-ISSUES.md) (16 issues, labels et milestone).

### a. Créer le dépôt distant

1. Sur GitHub : **New repository** → nom `slidecraft` → **Private** →
   ne pas cocher « Add a README » (il existe déjà).
2. **Settings → Collaborators → Add people** : inviter les 3 autres comptes.
3. Chacun accepte l'invitation par e-mail.

### b. Premier envoi depuis le dossier du projet

```bash
git add .
git commit -m "chore: import initial du projet SlideCraft"
git branch -M main
git remote add origin https://github.com/<compte>/slidecraft.git
git push -u origin main
```

### c. Récupérer le projet (les 3 autres)

```bash
git clone https://github.com/<compte>/slidecraft.git
cd slidecraft
dotnet restore SlideCraft.csproj
```

### d. Protéger `main`

**Settings → Branches → Add branch protection rule** pour `main` :

- ☑ Require a pull request before merging
- ☑ Require approvals : **1**
- ☑ Dismiss stale approvals when new commits are pushed

Ainsi personne ne pousse directement sur `main`, et tout passe par une PR relue.

> Le fichier `.gitignore` exclut déjà `bin/`, `obj/`, `.vs/`, les fichiers
> `*.user` et l'archive `SlideCraft-source.zip`. **Ne jamais** commiter un
> exécutable, un `.pptx` de test ou l'archive : ils alourdissent le dépôt.

### e. Votre cas : dépôt créé et collaborateurs déjà ajoutés

Il ne reste qu'à **envoyer les fichiers** pour que les trois autres puissent
cloner. Le zip `SlideCraft-source.zip` ne contient **pas** de dossier `.git` :
on part donc d'un dépôt local propre.

**1. Sur le PC Windows du responsable**

```powershell
# Dézipper l'archive, puis ouvrir un terminal DANS le dossier SlideCraft
# (celui qui contient SlideCraft.csproj et README.md)

dotnet --version                 # doit afficher 11.x

git init
git config user.name "Prénom Nom"
git config user.email "adresse@exemple.com"
```

**2. Vérifier ce qui va être envoyé**

```powershell
git add .
git status --short
```

Vous devez voir uniquement les sources, les `.xaml`, `Resources/`, les trois
`.md` et `.gitignore`. **Si `bin/` ou `obj/` apparaissent**, arrêtez et
vérifiez que `.gitignore` est bien présent à la racine.

**3. Premier commit et envoi**

```powershell
git commit -m "chore: import initial du projet SlideCraft"
git branch -M main
git remote add origin https://github.com/<compte>/slidecraft.git
git remote -v                    # vérifie l'adresse
git push -u origin main
```

> **Si GitHub a créé un README ou une licence** lors de la création du dépôt,
> le premier `push` sera refusé. Le plus simple : supprimer ce README sur
> GitHub (bouton corbeille) puis relancer `git push -u origin main`. Sinon :
> `git pull --rebase origin main --allow-unrelated-histories`, garder **votre**
> `README.md` en cas de conflit, puis pousser.

**4. Vérifier sur GitHub**

- La page du dépôt affiche le README avec sa mise en forme.
- Le nombre de fichiers correspond (**84**) et il n'y a **aucun** `bin/`.
- Chaque collaborateur voit le bouton **Code → Clone**.

**5. Protéger `main`** : voir §1.d.

**6. Créer labels, milestone et issues en une commande**

Installer GitHub CLI, puis se connecter (le jeton reste dans le coffre Windows,
jamais dans le projet) :

```powershell
winget install --id GitHub.cli
# rouvrir le terminal, puis :
gh auth login          # GitHub.com > HTTPS > connexion par navigateur
```

Depuis la racine du dépôt :

```powershell
powershell -ExecutionPolicy Bypass -File tools/setup-github.ps1 `
    -Repo <compte>/slidecraft `
    -DevA <compteA> -DevB <compteB> -DevC <compteC> -DevD <compteD>
```

Le script : fait le premier commit/push s'il manque, crée les **10 labels**, la
**milestone**, les **16 issues** (sans doublon, assignées par domaine) et
protège `main`. Il affiche un bilan et fonctionne aussi en simulation :

```powershell
powershell -ExecutionPolicy Bypass -File tools/setup-github.ps1 -DryRun -Repo <compte>/slidecraft
```

Options : `-SkipPush` (code déjà en ligne), `-SkipProtection` (pas de droits
admin), `-RemoteUrl <url>` (ajouter `origin` manuellement). Sans `-Repo`, le
script déduit le dépôt depuis `git remote`.

Si vous préférez tout faire à la main, suivez [GITHUB-ISSUES.md](GITHUB-ISSUES.md).

**7. Les trois autres récupèrent le projet**

```powershell
git clone https://github.com/<compte>/slidecraft.git
cd slidecraft
dotnet restore SlideCraft.csproj
dotnet build SlideCraft.csproj
```

Ils doivent obtenir **0 erreur, 0 avertissement** avant de commencer.

**8. Premier cycle de travail (à répéter pour chaque issue)**

```powershell
git switch main
git pull
git switch -c test/campagne-editeur   # nom lié à l'issue (A1, B2, C3…)
# ... travail et tests ...
git add .
git commit -m "test: campagne Windows de l'éditeur (A1)"
git push -u origin test/campagne-editeur
```

Puis sur GitHub : **Compare & pull request** → cocher la Definition of Done
(§6) → demander la relecture du binôme → **Squash and merge** → supprimer la
branche.

---

## 2. Règles Git communes

### Branches

Une branche par tâche, nommée par domaine :

| Préfixe | Usage | Exemple |
|---|---|---|
| `feat/` | nouvelle fonctionnalité | `feat/export-quiz-texte` |
| `fix/` | correction de bug | `fix/sous-titres-coupes` |
| `docs/` | documentation seule | `docs/guide-tests` |
| `chore/` | outillage, CI, dépendances | `chore/ci-windows` |

```bash
git switch main
git pull
git switch -c feat/ma-tache
```

### Commits

Messages courts, à l'impératif, avec un préfixe :

```
feat: ajoute le quiz au fichier de notes
fix: corrige le nom du fichier -notes.txt
docs: détaille les tests du diaporama
```

Un commit = un changement compréhensible. Éviter `update`, `modifs`, `test`.

### Avant d'ouvrir une PR

```bash
git fetch origin
git rebase origin/main          # ou : git merge origin/main
dotnet build SlideCraft.csproj  # doit afficher 0 erreur, 0 avertissement
git push -u origin feat/ma-tache
```

### Pull request

- Titre clair + description en 3 points : **quoi**, **pourquoi**, **comment tester**.
- Cocher la checklist : build OK, tests manuels effectués, pas de fichier indésirable.
- **1 relecteur minimum**, pris dans l'autre binôme (voir §3).
- Merge en **Squash and merge** pour garder un historique lisible.
- Supprimer la branche après merge.

### Règle anti-conflit

Chaque fichier a **un seul propriétaire** (voir §3). Si un changement doit
toucher un fichier d'un autre domaine (surtout `Views/MainWindow.xaml`,
`ViewModels/MainViewModel.cs` et `SlideCraft.csproj`), **prévenir la personne
concernée avant de commencer** et faire relire la PR par elle.

---

## 3. Répartition du travail

Quatre domaines, équilibrés et **sans recouvrement de fichiers**. Chaque
binôme se relit mutuellement : **A ↔ B**, **C ↔ D**. Les tâches détaillées,
avec cases à cocher et critères d'acceptation, sont dans
[GITHUB-ISSUES.md](GITHUB-ISSUES.md) (issues `A1` à `D4`).

### Dev A — Éditeur et modèle de données

**Fichiers possédés**

- `Models/` (sauf `Quiz*.cs`, `Game*.cs`, `SlideSubtitles.cs`, `SubtitleMode.cs`,
  `SlideAnalysis.cs`, `SlideIssue.cs`) — dont `Models/VideoElement.cs`
- `ViewModels/MainViewModel.cs`, `ViewModels/ObservableObject.cs`
- `Commands/RelayCommand.cs`
- `Views/MainWindow.xaml` et `.xaml.cs` — **parties Home / Insert / Design et aperçu des diapositives**
- `Services/UndoRedoService.cs`, `Services/PresentationSnapshot.cs`

**Missions**

1. Campagne de test Windows de l'éditeur (§5, scénarios 1 à 5 et 16) et
   ouverture d'issues pour chaque défaut trouvé.
2. Vérifier Undo/Redo sur **chaque** action : texte, image, forme, tableau,
   fond, disposition, déplacement, redimensionnement, suppression.
3. Vérifier le zoom et les dispositions sur une petite fenêtre (redimensionnement).
4. Après validation : découper `MainViewModel.cs` (2 178 lignes) en classes
   `partial` — `MainViewModel.Editing.cs`, `.Files.cs`, `.Show.cs` — **sans
   changer le comportement**, pour réduire les conflits futurs.

**Critère de fin** : les scénarios 1 à 5 passent, aucun bouton inactif, build propre.

---

### Dev B — Fichiers, export et sauvegarde

**Fichiers possédés**

- `Services/PresentationFileService.cs`, `Services/PresentationFactory.cs`
- `Services/PowerPointExporter.cs`, `Services/PowerPointImporter.cs`,
  `Services/NotesExporter.cs`
- `Services/FileDialogService.cs`, `Services/IFileDialogService.cs`
- Partie **File / Backstage** de `Views/MainWindow.xaml` (bandeau, page Export)

**Missions**

1. Test aller-retour `.scraft` : enregistrer puis rouvrir une présentation
   contenant texte, image, forme, tableau, notes, quiz et sous-titres ;
   vérifier que **tout** revient à l'identique.
2. Ouvrir le `.pptx` exporté dans PowerPoint **et** LibreOffice Impress ;
   vérifier textes, images, formes, tableaux et positions.
3. Vérifier `nom-notes.txt` : accents (UTF-8), retours à la ligne Windows,
   champs vides explicites, sous-titres multilignes alignés.
4. Tester les cas d'erreur : dossier en lecture seule, fichier `.pptx` déjà
   ouvert dans PowerPoint, disque plein → message clair, pas de crash.
5. Vérifier que le `.scraft` n'est **jamais** modifié par un export.
6. Tester l'import d'un `.pptx` réel : texte, images, tableaux, fonds, notes ;
   fichier corrompu ou renommé en `.pptx` → message clair, pas de crash.

**Critère de fin** : scénarios 6 à 8 et 17 passent, aucun fichier de test commité.

---

### Dev C — Diaporama, quiz, sous-titres et narration

**Fichiers possédés**

- `Views/SlideShowWindow.*`, `Views/QuizEditorWindow.*`,
  `Views/SubtitleEditorWindow.*`, `Views/GameSetupWindow.*`,
  `Views/NarrationVoiceWindow.*`
- `ViewModels/SlideShowViewModel.cs`, `ViewModels/QuizEditorViewModel.cs`,
  `ViewModels/SubtitleEditorViewModel.cs`
- `Services/SlideShowService.cs`, `Services/QuizEditorService.cs`,
  `Services/SubtitleEditorService.cs`, `Services/GameSession.cs`,
  `Services/GameSetupService.cs`, `Services/NarrationService.cs`,
  `Services/NarrationTextBuilder.cs`, `Services/NarrationVoiceStore.cs`,
  `Services/NarrationDemoText.cs`, `Services/SpeechVoice.cs`
- `ViewModels/NarrationVoiceViewModel.cs`
- `Models/Quiz.cs`, `Models/QuizQuestion.cs`, `Models/GamePlayer.cs`,
  `Models/GameAnswer.cs`, `Models/SlideSubtitles.cs`, `Models/SubtitleMode.cs`

**Missions**

1. Tester le diaporama : `F5`, flèches, `Échap`, dernier diapositive, une
   seule diapositive, changement d'écran pendant la projection.
2. Tester les sous-titres : une seule langue, deux langues, texte long,
   multiligne, mode d'affichage ; vérifier qu'ils ne masquent pas le contenu.
3. Tester un quiz complet à 3 joueurs : création des questions, votes sur le
   PC du présentateur, calcul des scores, égalité, retour à l'éditeur.
4. Tester la narration : voix absente ou désactivée → message explicite ;
   lecture, arrêt, changement de diapositive pendant la lecture.
5. Tester le choix de voix : plusieurs voix installées, phrase d'essai dans la
   bonne langue, choix conservé après redémarrage.
6. Vérifier qu'aucune fenêtre ne reste bloquée (modal, focus, fermeture).

**Critère de fin** : scénarios 9 à 12 et 18 passent, aucune exception non gérée.

---

### Dev D — Qualité, SmartSlide, thème et CI

**Fichiers possédés**

- `Services/SmartSlideAnalyzer.cs`, `Services/SmartSlideService.cs`,
  `Views/SmartSlideWindow.*`, `ViewModels/SmartSlideViewModel.cs`
- `Models/SlideAnalysis.cs`, `Models/SlideIssue.cs`
- `Resources/Theme.xaml`, `Resources/Converters.cs`
- `Views/HelpWindow.*` (guide rapide)
- `README.md`, `CONTRIBUTING.md`, `GITHUB-ISSUES.md`, `.github/` (workflow CI,
  modèles d'issues), `tools/setup-github.ps1`

**Missions**

1. Mettre en place la CI GitHub Actions : build sur `windows-latest` à chaque
   PR (voir extrait ci-dessous).
2. Tester SmartSlide sur des cas limites : texte très long, 0 élément,
   éléments hors cadre, contraste faible ; corriger les faux positifs.
3. Vérifier le thème : lisibilité, contraste, tailles de police, rendu à
   100 %/125 %/150 % de mise à l'échelle Windows.
4. Tenir le README à jour et préparer la **checklist de release** (version,
   date, tests passés, limites connues).
5. Créer les labels et le projet GitHub : `area:editor`, `area:files`,
   `area:show`, `area:quality`, `bug`, `docs`, `blocked`.

**Extrait de workflow (`.github/workflows/build.yml`)**

```yaml
name: build
on:
  pull_request:
  push:
    branches: [main]
jobs:
  build:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 11.0.x
      - run: dotnet build SlideCraft.csproj -warnaserror
```

**Critère de fin** : CI verte sur `main`, SmartSlide sans faux positif évident,
README exact.

---

## 4. Répartition en un tableau

| | Dev A | Dev B | Dev C | Dev D |
|---|---|---|---|---|
| Domaine | Éditeur, modèle | Fichiers, export | Diaporama, quiz | Qualité, CI, thème |
| Dossier principal | `Models/`, `ViewModels/MainViewModel.cs` | `Services/` fichiers | `Views/` projection | `Resources/`, `.github/` |
| Relecteur | Dev B | Dev A | Dev D | Dev C |
| Scénarios de test | 1 à 5 | 6 à 8 | 9 à 12 | 13 à 15 |

---

## 5. Check-list de validation manuelle (Windows)

À dérouler **avant** de dire qu'une version est bonne. Une ligne = un test,
un résultat attendu, une case à cocher.

| # | Test | Résultat attendu | Qui |
|---|---|---|---|
| 1 | Lancer l'application | La démo de 3 diapositives s'affiche, aucune exception | A |
| 2 | Ajouter / dupliquer / supprimer / déplacer une diapositive | Miniatures et sélection cohérentes | A |
| 3 | Insérer texte, image, forme, tableau ; déplacer, redimensionner | Rendu correct, sélection visible | A |
| 4 | `Ctrl+Z` / `Ctrl+Y` sur 10 actions | Chaque action est annulée puis rétablie | A |
| 5 | Changer le zoom et la taille de la fenêtre | Aucun élément ne disparaît | A |
| 6 | Enregistrer puis rouvrir un `.scraft` complet | Contenu identique (texte, image, tableau, notes, quiz, sous-titres) | B |
| 7 | Exporter en `.pptx`, ouvrir dans PowerPoint et LibreOffice | Diapositives fidèles, fichier lisible | B |
| 8 | Vérifier `nom-notes.txt` | Notes, conseil, narration et 2 langues présents, accents corrects | B |
| 9 | `F5`, navigation, `Échap` | Projection plein écran fluide, retour à l'éditeur | C |
| 10 | Sous-titres une langue / deux langues / texte long | Lisibles, non tronqués, ne masquent pas le contenu | C |
| 11 | Quiz à 3 joueurs, votes, scores, égalité | Scores justes, aucune fenêtre bloquée | C |
| 12 | Narration : lecture, arrêt, voix absente | Message clair si aucune voix, arrêt immédiat | C |
| 13 | SmartSlide sur une diapositive surchargée | Problèmes pertinents, pas de faux positif | D |
| 14 | Mise à l'échelle Windows 150 % | Interface lisible, aucun texte coupé | D |
| 15 | Build CI sur la PR | Vert, 0 avertissement | D |
| 16 | Insérer une vidéo (mp4) puis lancer le diaporama | Muette et en boucle dans l'éditeur, avec le son pendant la projection, coupée au changement de diapositive | A |
| 17 | Importer un `.pptx` (texte, image, tableau, notes) | Diapositives converties, fonds et notes conservés, message clair si le fichier est invalide | B |
| 18 | `Slide Show → Narration Voice` | Liste des voix avec leur langue, phrase d'essai audible, choix conservé après redémarrage | C |
| 19 | Onglet `Transitions` : Fade, Push, Wipe sur deux diapositives, puis `Apply to All`, puis `Preview` | L'effet est réellement joué au changement de diapositive ; `Apply to All` se défait d'un seul `Ctrl+Z` ; le choix survit à un enregistrement / réouverture `.scraft` | C |
| 20 | Onglet `Review` : `Check Slides` puis `Review Summary` | Le rapport SmartSlide s'ouvre ; le résumé donne des nombres justes (mots, temps de lecture) et la liste des modifications ; `Undo` dans la fenêtre annule bien la dernière action | D |
| 21 | Onglet `View` : masquer puis réafficher `Slides`, `Properties`, `Notes` | La colonne ou la zone disparaît vraiment (pas de vide à la place) et revient à l'identique | A |
| 22 | Importer un `.pptx` avec un titre, un groupe de formes et des retours à la ligne dans un paragraphe | Titre repris dans la liste des miniatures, groupe à la bonne position et à la bonne échelle, mots non collés | B |
| 23 | Enregistrer un projet contenant une vidéo, puis déplacer le `.scraft` **avec** son dossier `nom_media` dans un autre dossier | Un dossier `nom_media` est créé à côté du `.scraft`, la vidéo y est copiée, et elle joue toujours après le déplacement | A |
| 24 | Exporter un projet avec vidéo en `.pptx`, l'ouvrir dans PowerPoint, puis le réimporter dans SlideCraft | La vidéo est intégrée au `.pptx` (pas un simple lien), elle y joue, et le réimport retrouve une vidéo jouable (pas une image figée) | B |

---

## 6. Definition of Done (pour toute PR)

Une PR n'est fusionnable que si :

- [ ] `dotnet build SlideCraft.csproj` → **0 erreur, 0 avertissement** ;
- [ ] les tests manuels concernés sont cochés dans la description ;
- [ ] aucun fichier `bin/`, `obj/`, `.vs/`, `.zip`, `.pptx` ou secret n'est ajouté ;
- [ ] une personne de l'autre binôme a relu et approuvé ;
- [ ] le README ou ce guide est mis à jour si le comportement change ;
- [ ] la CI est verte.

---

## 7. Pistes pour la suite (non planifiées)

À discuter ensemble avant de coder — chacune doit rester **hors ligne** :

- inclure le quiz et les réponses dans `nom-notes.txt` ;
- export PDF (via une impression XAML) ;
- tests unitaires `xUnit` sur `Models/` et `Services/` (le code est déjà
  découplé pour ça) ;
- thème sombre ;
- mode présentateur sur second écran avec notes visibles.
