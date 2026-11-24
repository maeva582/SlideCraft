# Backlog GitHub — SlideCraft

Ce fichier contient **toutes les issues de la milestone `v1.0 — validation Windows`**,
prêtes à copier-coller dans GitHub. Une issue = une branche = une pull request.

- Pour chaque issue : un **titre**, des **labels**, un **assigné**, une description
  avec cases à cocher et des **critères d'acceptation**.
- Remplacez « Dev A / B / C / D » par les comptes GitHub réels.
- Les fichiers possédés par chacun sont rappelés dans
  [CONTRIBUTING.md](CONTRIBUTING.md) §3 : ne modifiez pas le fichier d'un autre
  domaine sans le prévenir.

> **Automatisation** : `tools/setup-github.ps1` crée les labels, la milestone,
> les 16 issues et la protection de `main` à partir de ce fichier (voir
> [CONTRIBUTING.md](CONTRIBUTING.md) §1.e). Le mode `-DryRun` montre tout sans
> rien modifier.

## Ordre conseillé

1. **D4a** (labels + milestone) puis **D1** (CI) : tout le reste en profite.
2. **A1, B1, C1** en parallèle : ce sont les campagnes de test principales.
3. Les corrections trouvées deviennent des issues `bug` séparées, assignées au
   propriétaire du fichier.
4. **A4** (découpage du ViewModel) seulement **après** la validation, pour ne pas
   mélanger refactorisation et corrections.

---

## Labels à créer

**Issues → Labels → New label**

| Label | Couleur | Description |
|---|---|---|
| `area:editor` | `#6366F1` | Éditeur, modèle, Undo/Redo |
| `area:files` | `#0EA5E9` | `.scraft`, `.pptx`, `-notes.txt` |
| `area:show` | `#F59E0B` | Diaporama, quiz, sous-titres, narration |
| `area:quality` | `#10B981` | SmartSlide, thème, CI, documentation |
| `bug` | `#DC2626` | Comportement incorrect à corriger |
| `tests` | `#A855F7` | Campagne de test manuelle |
| `enhancement` | `#8B5CF6` | Amélioration ou refactorisation |
| `docs` | `#64748B` | Documentation seule |
| `ci` | `#0891B2` | Intégration continue |
| `blocked` | `#B91C1C` | En attente d'une autre issue |

## Milestone

**Issues → Milestones → New milestone**

- Titre : `v1.0 — validation Windows`
- Échéance suggérée : deux semaines après le premier push.

---

# Issues

## Dev A — Éditeur et modèle

### A1 — Campagne de test Windows de l'éditeur

**Titre :** `test: campagne Windows de l'éditeur et du modèle`
**Labels :** `area:editor`, `tests` · **Assigné :** Dev A

**Contexte**
Aucun test automatisé n'existe et WPF ne s'exécute que sous Windows. Cette issue
valide tout le cœur d'édition avant de considérer la v1.0 comme bonne.

**À faire**
- [ ] Lancer l'application : la démo de 3 diapositives et ses miniatures s'affichent.
- [ ] `Ctrl+N` : nouvelle présentation vide, une seule diapositive.
- [ ] Ajouter 5 diapositives, puis dupliquer, supprimer, monter et descendre chacune.
- [ ] Vérifier que la sélection reste cohérente après chaque opération.
- [ ] Insérer un texte, une image (PNG et JPG), une forme et un tableau ; déplacer
      et redimensionner les quatre types.
- [ ] Changer l'arrière-plan et appliquer chaque disposition disponible.
- [ ] Copier un élément d'une diapositive et le coller sur une autre.
- [ ] Redimensionner la fenêtre et utiliser le zoom avant/arrière.
- [ ] Noter chaque défaut dans une **nouvelle issue** `bug` + `area:editor`
      (étapes de reproduction, attendu, obtenu, capture).

**Critères d'acceptation**
- Tous les points ci-dessus passés sur Windows 10 **et** 11 si possible.
- Aucune exception non gérée, aucun bouton sans effet.
- La liste des défauts est vide ou chaque défaut a son issue.

**Fichiers concernés :** `Models/`, `ViewModels/MainViewModel.cs`,
`Views/MainWindow.xaml`, `Commands/RelayCommand.cs`.

---

### A2 — Vérifier Undo/Redo sur toutes les actions

**Titre :** `test: vérifier Undo/Redo sur toutes les actions de l'éditeur`
**Labels :** `area:editor`, `tests` · **Assigné :** Dev A

**Contexte**
`UndoRedoService` et `PresentationSnapshot` doivent couvrir chaque mutation.
Un oubli laisse l'utilisateur avec des états incohérents.

**À faire**
Pour chacune de ces actions : l'exécuter, `Ctrl+Z`, vérifier le retour exact,
`Ctrl+Y`, vérifier le rétablissement.
- [ ] Ajouter une diapositive
- [ ] Supprimer une diapositive
- [ ] Déplacer une diapositive
- [ ] Insérer un texte / une image / une forme / un tableau
- [ ] Déplacer un élément
- [ ] Redimensionner un élément
- [ ] Changer la couleur de texte ou de remplissage
- [ ] Changer l'arrière-plan
- [ ] Coller un élément
- [ ] Modifier le titre d'une diapositive
- [ ] Vérifier qu'une nouvelle action après un `Ctrl+Z` vide bien la pile de redo.

**Critères d'acceptation**
- Chaque action est annulée puis rétablie **à l'identique**.
- Aucun `Ctrl+Z` ne provoque d'exception ni d'état vide (0 diapositive).

**Fichiers :** `Services/UndoRedoService.cs`, `Services/PresentationSnapshot.cs`,
`ViewModels/MainViewModel.cs`.

---

### A3 — Zoom, dispositions et redimensionnement

**Titre :** `test: zoom, dispositions et redimensionnement de la fenêtre`
**Labels :** `area:editor`, `tests` · **Assigné :** Dev A

**À faire**
- [ ] Zoom 50 %, 100 %, 200 % : le contenu reste centré et cliquable.
- [ ] Chaque disposition produit un résultat différent et correct.
- [ ] Fenêtre réduite à 1024 × 700 : les rubans et la colonne de miniatures
      restent utilisables, rien n'est coupé.
- [ ] Fenêtre agrandie : pas de zone blanche inattendue.

**Critères d'acceptation**
- Aucun élément inatteignable, aucun texte tronqué dans l'interface.
- Aucune exception au redimensionnement.

**Fichiers :** `Views/MainWindow.xaml`, `Models/SlideLayout.cs`,
`ViewModels/MainViewModel.cs`.

---

### A4 — Découper `MainViewModel.cs` en classes `partial`

**Titre :** `refactor: découper MainViewModel en classes partial`
**Labels :** `area:editor`, `enhancement` · **Assigné :** Dev A · **À faire après A1–A3**

**Contexte**
`ViewModels/MainViewModel.cs` fait 2 026 lignes : à quatre, chaque PR y crée des
conflits. Le découpage est **purement structurel** : aucun comportement ne change.

**À faire**
- [ ] Créer `MainViewModel.Editing.cs` (diapositives, éléments, presse-papiers).
- [ ] Créer `MainViewModel.Files.cs` (nouveau, ouvrir, enregistrer, exporter).
- [ ] Créer `MainViewModel.Show.cs` (diaporama, SmartSlide, quiz, sous-titres, narration).
- [ ] Garder `MainViewModel.cs` pour l'état partagé et le constructeur.
- [ ] Marquer chaque fichier `public partial class MainViewModel`.
- [ ] Ne renommer aucun membre public.

**Critères d'acceptation**
- Build 0 erreur / 0 avertissement, aucune modification de logique.
- L'application se comporte exactement comme avant (tests A1–A3 rejoués).
- Relecture par Dev B.

---

## Dev B — Fichiers, export et sauvegarde

### B1 — Aller-retour `.scraft` sans perte

**Titre :** `test: aller-retour .scraft avec tous les types de contenu`
**Labels :** `area:files`, `tests` · **Assigné :** Dev B

**À faire**
- [ ] Créer une présentation contenant : texte, image, forme, tableau, notes,
      conseil du coach, narration, durée cible, quiz et sous-titres bilingues.
- [ ] Enregistrer, fermer l'application, la relancer, rouvrir le fichier.
- [ ] Comparer **champ par champ** : tout doit être identique (positions, tailles,
      couleurs, textes, langues, questions et réponses).
- [ ] Vérifier que le fichier est bien du XML lisible.
- [ ] Enregistrer sous un autre nom (`Save as…`) puis rouvrir les deux fichiers.

**Critères d'acceptation**
- Aucune perte de donnée, aucune exception à l'ouverture.
- Les accents et caractères spéciaux (`é`, `€`, `—`) sont conservés.

**Fichiers :** `Services/PresentationFileService.cs`,
`Services/PresentationFactory.cs`, `Models/`.

---

### B2 — Export `.pptx` vérifié dans deux logiciels

**Titre :** `test: vérifier l'export .pptx dans PowerPoint et LibreOffice`
**Labels :** `area:files`, `tests` · **Assigné :** Dev B

**À faire**
- [ ] Exporter une présentation de 3 diapositives riches (texte, image, forme, tableau).
- [ ] Ouvrir le fichier dans **PowerPoint** : aucune réparation demandée.
- [ ] Ouvrir le même fichier dans **LibreOffice Impress** : rendu comparable.
- [ ] Vérifier que les images sont bien **incorporées** (déplacer le `.pptx` seul
      sur le bureau et le rouvrir).
- [ ] Vérifier qu'aucune diapositive n'est vide et que les positions sont proches.
- [ ] Confirmer que le `.scraft` d'origine est inchangé (date de modification).

**Critères d'acceptation**
- Le `.pptx` s'ouvre sans avertissement dans les deux logiciels.
- Aucune image manquante, aucun texte illisible.

**Fichiers :** `Services/PowerPointExporter.cs`.

---

### B3 — Vérifier `nom-notes.txt`

**Titre :** `test: vérifier nom-notes.txt (encodage, accents, sous-titres)`
**Labels :** `area:files`, `tests` · **Assigné :** Dev B

**À faire**
- [ ] Exporter et vérifier la présence de `demo-notes.txt` à côté de `demo.pptx`.
- [ ] Ouvrir le `.txt` dans le Bloc-notes **et** dans VS Code : accents corrects,
      pas de `Ã©`.
- [ ] Vérifier les fins de ligne Windows (CRLF).
- [ ] Vérifier les cas vides : `(none)`, `No presenter notes on this slide.`,
      `No subtitles on this slide.`.
- [ ] Vérifier qu'un sous-titre multiligne reste aligné sous son étiquette.
- [ ] Vérifier une présentation sans aucune note ni sous-titre.

**Critères d'acceptation**
- Le fichier est lisible tel quel par un professeur, sans retouche.
- Chaque diapositive a son bloc, dans l'ordre.

**Fichiers :** `Services/NotesExporter.cs`, `ViewModels/MainViewModel.cs`
(partie export uniquement, en accord avec Dev A).

---

### B4 — Erreurs d'écriture gérées proprement

**Titre :** `fix: messages clairs en cas d'échec d'écriture`
**Labels :** `area:files`, `bug` · **Assigné :** Dev B

**À faire**
- [ ] Exporter vers un dossier en lecture seule → message clair, aucun crash.
- [ ] Exporter un `.pptx` déjà ouvert dans PowerPoint → message clair.
- [ ] Enregistrer un `.scraft` sur un support plein ou déconnecté → message clair.
- [ ] Vérifier qu'un échec du `.txt` seul n'empêche pas le `.pptx` et affiche
      `Exported to "x.pptx" (notes file failed)`.
- [ ] Vérifier qu'aucun fichier `.tmp` ne reste après un échec.

**Critères d'acceptation**
- Chaque cas affiche un message compréhensible et laisse l'application utilisable.
- Aucune perte du travail en cours.

**Fichiers :** `Services/PresentationFileService.cs`,
`Services/NotesExporter.cs`, `Services/FileDialogService.cs`.

---

## Dev C — Diaporama, quiz, sous-titres, narration

### C1 — Diaporama complet

**Titre :** `test: diaporama (F5, navigation, Échap, cas limites)`
**Labels :** `area:show`, `tests` · **Assigné :** Dev C

**À faire**
- [ ] `F5` depuis la première diapositive, navigation avant/arrière au clavier.
- [ ] `Échap` : retour immédiat à l'éditeur, aucune fenêtre résiduelle.
- [ ] Avancer au-delà de la dernière diapositive : comportement défini, sans crash.
- [ ] Présentation à **une seule** diapositive.
- [ ] `Alt+Tab` pendant la projection, puis retour.
- [ ] Si un second écran est disponible : projection dessus, éditeur sur le premier.
- [ ] Vérifier le minuteur et l'indicateur de progression.

**Critères d'acceptation**
- Projection fluide, aucune exception, focus rendu correctement à l'éditeur.

**Fichiers :** `Views/SlideShowWindow.*`, `ViewModels/SlideShowViewModel.cs`,
`Services/SlideShowService.cs`.

---

### C2 — Sous-titres bilingues

**Titre :** `test: sous-titres bilingues (une langue, deux langues, texte long)`
**Labels :** `area:show`, `tests` · **Assigné :** Dev C

**À faire**
- [ ] Diapositive avec anglais seul, français seul, puis les deux.
- [ ] Texte de 3 lignes par langue : alignement et lisibilité.
- [ ] Texte très long : pas de débordement hors de l'écran.
- [ ] Chaque mode d'affichage (`SubtitleMode`) testé.
- [ ] Vérifier que les sous-titres ne masquent pas le contenu important.
- [ ] Enregistrer puis rouvrir : les deux langues sont conservées.

**Critères d'acceptation**
- Les deux langues restent identifiables et lisibles en projection.
- Aucun texte tronqué sans indication.

**Fichiers :** `Views/SubtitleEditorWindow.*`,
`ViewModels/SubtitleEditorViewModel.cs`, `Models/SlideSubtitles.cs`,
`Views/SlideShowWindow.xaml`.

---

### C3 — Quiz à 3 joueurs

**Titre :** `test: quiz complet à 3 joueurs (votes, scores, égalité)`
**Labels :** `area:show`, `tests` · **Assigné :** Dev C

**À faire**
- [ ] Créer un quiz de 4 questions à choix multiples sur une diapositive.
- [ ] Configurer 3 joueurs, voter sur le PC du présentateur.
- [ ] Vérifier que le score est juste pour chaque joueur.
- [ ] Provoquer une égalité et vérifier l'affichage du résultat.
- [ ] Vérifier une question sans bonne réponse ou avec une seule réponse.
- [ ] Fermer la fenêtre de jeu en cours de partie : aucun blocage.
- [ ] Enregistrer, rouvrir : questions et réponses intactes.

**Critères d'acceptation**
- Scores exacts, aucune fenêtre modale bloquée, retour propre à l'éditeur.
- Aucun accès réseau (tout est local).

**Fichiers :** `Views/QuizEditorWindow.*`, `Views/GameSetupWindow.*`,
`ViewModels/QuizEditorViewModel.cs`, `Services/GameSession.cs`,
`Services/GameSetupService.cs`, `Models/Quiz*.cs`, `Models/Game*.cs`.

---

### C4 — Narration et voix absente

**Titre :** `test: narration (lecture, arrêt, aucune voix installée)`
**Labels :** `area:show`, `tests` · **Assigné :** Dev C

**À faire**
- [ ] Lire la narration d'une diapositive, vérifier l'arrêt en cours de lecture.
- [ ] Changer de diapositive pendant la lecture : le texte lu change proprement.
- [ ] Tester sur une machine sans voix française installée → message explicite.
- [ ] Tester avec une narration vide → aucun crash, message ou inaction claire.
- [ ] Quitter l'application pendant une lecture → aucun processus résiduel.

**Critères d'acceptation**
- Aucune exception, message compréhensible si la synthèse vocale est indisponible.

**Fichiers :** `Services/NarrationService.cs`,
`Services/NarrationTextBuilder.cs`, `ViewModels/SlideShowViewModel.cs`.

---

## Dev D — Qualité, SmartSlide, thème, CI

### D1 — CI GitHub Actions

**Titre :** `ci: compiler le projet sur windows-latest à chaque PR`
**Labels :** `area:quality`, `ci` · **Assigné :** Dev D

**À faire**
- [ ] Créer `.github/workflows/build.yml` (extrait dans CONTRIBUTING.md §3).
- [ ] Vérifier que le workflow se déclenche sur `pull_request` et sur `push` vers `main`.
- [ ] Vérifier qu'une PR qui ne compile pas est bien rouge.
- [ ] Ajouter le badge du workflow en haut du README.

**Critères d'acceptation**
- CI verte sur `main`, build en 0 erreur / 0 avertissement (`-warnaserror`).

**Fichiers :** `.github/workflows/build.yml`, `README.md`.

---

### D2 — SmartSlide sur cas limites

**Titre :** `test: SmartSlide sur cas limites et faux positifs`
**Labels :** `area:quality`, `tests` · **Assigné :** Dev D

**À faire**
- [ ] Diapositive vide (0 élément) : analyse sans crash.
- [ ] Texte très long dans un petit cadre : problème signalé une seule fois.
- [ ] Élément partiellement hors cadre : signalé correctement.
- [ ] Contraste faible (gris clair sur blanc) : signalé.
- [ ] Diapositive correcte : **aucun** problème affiché.
- [ ] Rejouer l'analyse après correction : la liste se met à jour.

**Critères d'acceptation**
- Aucun faux positif évident, aucune exception, conseils compréhensibles.

**Fichiers :** `Services/SmartSlideAnalyzer.cs`,
`ViewModels/SmartSlideViewModel.cs`, `Views/SmartSlideWindow.*`,
`Models/SlideAnalysis.cs`, `Models/SlideIssue.cs`.

---

### D3 — Thème et mise à l'échelle Windows

**Titre :** `test: thème et mise à l'échelle 100 / 125 / 150 %`
**Labels :** `area:quality`, `tests` · **Assigné :** Dev D

**À faire**
- [ ] Tester l'application aux trois niveaux de mise à l'échelle Windows.
- [ ] Vérifier les contrastes des textes secondaires et des états désactivés.
- [ ] Vérifier qu'aucun libellé n'est coupé dans les rubans et les fenêtres modales.
- [ ] Vérifier le rendu des icônes et des images.
- [ ] Noter toute incohérence visuelle dans une issue `area:quality`.

**Critères d'acceptation**
- Interface lisible et utilisable aux trois échelles, aucun texte coupé.

**Fichiers :** `Resources/Theme.xaml`, `Resources/Converters.cs`, `Views/*.xaml`.

---

### D4 — Outillage GitHub et checklist de release

**Titre :** `docs: labels, milestone, modèle de PR et checklist de release`
**Labels :** `area:quality`, `docs` · **Assigné :** Dev D

**À faire**
- [ ] Vérifier les deux modèles déjà fournis : `.github/pull_request_template.md`
      (Definition of Done) et `.github/ISSUE_TEMPLATE/bug_report.md`.
- [ ] Lancer `tools/setup-github.ps1` (ou tout créer à la main) : labels,
      milestone, 16 issues assignées, protection de `main`.
- [ ] Vérifier que `main` est protégée (PR obligatoire + 1 approbation).
- [ ] Ajouter en fin de README une section `## Version` (numéro, date, tests passés).

**Critères d'acceptation**
- Une nouvelle PR affiche automatiquement la checklist.
- Toutes les issues de la milestone sont créées et assignées.

**Fichiers :** `.github/`, `README.md`, `CONTRIBUTING.md`.
