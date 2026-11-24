<#
.SYNOPSIS
    Publie SlideCraft sur GitHub et crée labels, milestone et issues.

.DESCRIPTION
    À lancer UNE fois depuis un PC Windows, après « gh auth login ».
    Le script :
      1. vérifie que GitHub CLI (gh) est installé et connecté ;
      2. fait le premier commit et le premier push sur la branche main ;
      3. crée les 10 labels du backlog ;
      4. crée la milestone « v1.0 — validation Windows » ;
      5. lit GITHUB-ISSUES.md et crée les 16 issues (sans doublon) ;
      6. protège main (PR obligatoire + 1 approbation).

    Aucun identifiant n'est stocké dans le projet : gh garde le jeton dans
    son propre coffre (Windows Credential Manager).

.PARAMETER Repo
    Dépôt cible, au format compte/slidecraft. Déduit de « git remote » si absent.

.PARAMETER RemoteUrl
    URL du dépôt distant à ajouter si aucun « origin » n'existe.

.PARAMETER DevA / DevB / DevC / DevD
    Comptes GitHub des quatre développeurs, pour assigner les issues.
    Facultatif : sans eux, les issues ne sont pas assignées.

.PARAMETER SkipPush
    Ne fait ni commit ni push (utile si le code est déjà en ligne).

.PARAMETER SkipProtection
    Ne touche pas à la protection de la branche main.

.PARAMETER DryRun
    Affiche tout ce qui serait fait, sans rien modifier.

.EXAMPLE
    gh auth login
    powershell -ExecutionPolicy Bypass -File tools/setup-github.ps1 `
        -Repo moncompte/slidecraft -DevA alice -DevB bruno -DevC chloe -DevD david

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File tools/setup-github.ps1 -DryRun
#>
[CmdletBinding()]
param(
    [string]$Repo = "",
    [string]$RemoteUrl = "",
    [string]$Milestone = "v1.0 — validation Windows",
    [string]$DevA = "",
    [string]$DevB = "",
    [string]$DevC = "",
    [string]$DevD = "",
    [switch]$SkipPush,
    [switch]$SkipProtection,
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'

# La racine du dépôt est le dossier parent de tools/.
$root = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $root

# ---------------------------------------------------------------- utilitaires

function Invoke-Gh {
    param([string[]]$Arguments)

    if ($DryRun) {
        Write-Host ("  [dry-run] gh " + ($Arguments -join ' ')) -ForegroundColor DarkGray
        return ""
    }

    $output = & gh @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw ("gh " + ($Arguments -join ' ') + " a échoué :`n" + ($output -join "`n"))
    }
    return ($output -join "`n")
}

function Write-Step {
    param([string]$Text)
    Write-Host ""
    Write-Host "==> $Text" -ForegroundColor Cyan
}

# ------------------------------------------------- lecture du backlog (md)

function Read-Backlog {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Fichier introuvable : $Path"
    }

    $lines = Get-Content -LiteralPath $Path -Encoding UTF8
    $issues = New-Object System.Collections.ArrayList
    $current = $null

    foreach ($line in $lines) {
        if ($line -match '^### ([ABCD]\d) — ') {
            if ($null -ne $current) { [void]$issues.Add($current) }
            $current = [pscustomobject]@{
                Key    = $Matches[1]
                Title  = $null
                Labels = @()
                Body   = New-Object System.Collections.ArrayList
            }
            continue
        }

        if ($null -eq $current) { continue }

        if ($line -match '^\*\*Titre :\*\* `([^`]+)`') {
            $current.Title = $Matches[1]
            continue
        }

        if ($line -match '^\*\*Labels :\*\* ') {
            $found = [regex]::Matches($line, '`([^`]+)`')
            $current.Labels = @($found | ForEach-Object { $_.Groups[1].Value })
            continue
        }

        if ($line -match '^---\s*$' -or $line -match '^## ') {
            [void]$issues.Add($current)
            $current = $null
            continue
        }

        if ($current.Body.Count -eq 0 -and [string]::IsNullOrWhiteSpace($line)) { continue }
        [void]$current.Body.Add($line)
    }

    if ($null -ne $current) { [void]$issues.Add($current) }

    # Contrôle de forme : mieux vaut s'arrêter que créer des issues vides.
    foreach ($issue in $issues) {
        if ([string]::IsNullOrWhiteSpace($issue.Title)) {
            throw "Issue $($issue.Key) sans ligne « **Titre :** » dans le backlog."
        }
        if ($issue.Labels.Count -eq 0) {
            throw "Issue $($issue.Key) sans labels dans le backlog."
        }
        $issue.Body = (($issue.Body -join "`n").Trim())
    }

    return $issues
}

# ------------------------------------------------------------------- labels

$labelDefinitions = @(
    @{ Name = 'area:editor';  Color = '6366F1'; Description = 'Éditeur, modèle, Undo/Redo' },
    @{ Name = 'area:files';   Color = '0EA5E9'; Description = 'Fichiers .scraft, .pptx, -notes.txt' },
    @{ Name = 'area:show';    Color = 'F59E0B'; Description = 'Diaporama, quiz, sous-titres, narration' },
    @{ Name = 'area:quality'; Color = '10B981'; Description = 'SmartSlide, thème, CI, documentation' },
    @{ Name = 'bug';          Color = 'DC2626'; Description = 'Comportement incorrect à corriger' },
    @{ Name = 'tests';        Color = 'A855F7'; Description = 'Campagne de test manuelle' },
    @{ Name = 'enhancement';  Color = '8B5CF6'; Description = 'Amélioration ou refactorisation' },
    @{ Name = 'docs';         Color = '64748B'; Description = 'Documentation seule' },
    @{ Name = 'ci';           Color = '0891B2'; Description = 'Intégration continue' },
    @{ Name = 'blocked';      Color = 'B91C1C'; Description = 'En attente d''une autre issue' }
)

# --------------------------------------------------------------------- début

$issues = Read-Backlog -Path (Join-Path $root 'GITHUB-ISSUES.md')
Write-Host "Backlog lu : $($issues.Count) issues." -ForegroundColor Green

if ($issues.Count -ne 16) {
    Write-Warning "Le backlog ne contient pas 16 issues : vérifiez GITHUB-ISSUES.md."
}

if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    throw "GitHub CLI absent. Installez-le : winget install --id GitHub.cli"
}

if (-not $DryRun) {
    & gh auth status *> $null
    if ($LASTEXITCODE -ne 0) {
        throw "GitHub CLI n'est pas connecté. Lancez d'abord : gh auth login"
    }
}

# Déduction du dépôt depuis le remote existant.
if ([string]::IsNullOrWhiteSpace($Repo)) {
    $existingUrl = & git remote get-url origin 2>$null
    if ($LASTEXITCODE -eq 0 -and $existingUrl) {
        $Repo = ($existingUrl -replace '^.*github\.com[:/]', '' -replace '\.git$', '')
    }
}

if ([string]::IsNullOrWhiteSpace($Repo)) {
    throw "Dépôt inconnu. Relancez avec : -Repo compte/slidecraft"
}

Write-Host "Dépôt cible : $Repo" -ForegroundColor Green

# ------------------------------------------------------------- push initial

if (-not $SkipPush -and -not $DryRun) {
    Write-Step "Premier commit et push"

    if (-not (Test-Path -LiteralPath (Join-Path $root '.git'))) {
        & git init | Out-Null
    }

    & git add -A
    $pending = & git status --porcelain
    if ($pending) {
        & git commit -m "chore: import initial du projet SlideCraft" | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "Le commit a échoué (identité git configurée ?)." }
    }

    & git branch -M main

    $remotes = & git remote
    if (-not ($remotes -contains 'origin')) {
        $url = if ($RemoteUrl) { $RemoteUrl } else { "https://github.com/$Repo.git" }
        & git remote add origin $url
    }

    & git push -u origin main
    if ($LASTEXITCODE -ne 0) {
        throw "Le push a échoué. Si GitHub a créé un README, supprimez-le puis relancez."
    }
}

# ------------------------------------------------------------------- labels

Write-Step "Labels"
foreach ($label in $labelDefinitions) {
    Invoke-Gh @(
        'label', 'create', $label.Name,
        '--color', $label.Color,
        '--description', $label.Description,
        '--force'
    ) | Out-Null
    Write-Host "  $($label.Name)"
}

# ---------------------------------------------------------------- milestone

Write-Step "Milestone « $Milestone »"
$milestoneExists = $false

if (-not $DryRun) {
    $raw = & gh api "repos/$Repo/milestones?state=all&per_page=100" 2>$null
    if ($LASTEXITCODE -eq 0 -and $raw) {
        $milestoneExists = @($raw | ConvertFrom-Json | Where-Object { $_.title -eq $Milestone }).Count -gt 0
    }
}

if ($milestoneExists) {
    Write-Host "  déjà créée"
} else {
    Invoke-Gh @('api', '-X', 'POST', "repos/$Repo/milestones", '-f', "title=$Milestone") | Out-Null
    Write-Host "  créée"
}

# ------------------------------------------------------------------- issues

Write-Step "Issues"

$existingTitles = @()
if (-not $DryRun) {
    $raw = & gh issue list --repo $Repo --state all --limit 500 --json title 2>$null
    if ($LASTEXITCODE -eq 0 -and $raw) {
        $existingTitles = @($raw | ConvertFrom-Json | ForEach-Object { $_.title })
    }
}

$created = 0
$skipped = 0

foreach ($issue in $issues) {
    if ($existingTitles -contains $issue.Title) {
        Write-Host "  déjà présente : $($issue.Title)" -ForegroundColor DarkGray
        $skipped++
        continue
    }

    $assignee = switch ($issue.Key.Substring(0, 1)) {
        'A' { $DevA }
        'B' { $DevB }
        'C' { $DevC }
        'D' { $DevD }
    }

    $bodyFile = Join-Path ([System.IO.Path]::GetTempPath()) "slidecraft-$($issue.Key).md"
    [System.IO.File]::WriteAllText($bodyFile, $issue.Body, (New-Object System.Text.UTF8Encoding($false)))

    $arguments = @(
        'issue', 'create',
        '--repo', $Repo,
        '--title', $issue.Title,
        '--body-file', $bodyFile,
        '--milestone', $Milestone
    )
    foreach ($label in $issue.Labels) {
        $arguments += @('--label', $label)
    }
    if (-not [string]::IsNullOrWhiteSpace($assignee)) {
        $arguments += @('--assignee', $assignee)
    }

    Invoke-Gh $arguments | Out-Null
    Remove-Item -LiteralPath $bodyFile -ErrorAction SilentlyContinue
    Write-Host "  $($issue.Key) : $($issue.Title)"
    $created++
}

# -------------------------------------------------------- protection de main

if (-not $SkipProtection) {
    Write-Step "Protection de la branche main"

    $protectionFile = Join-Path ([System.IO.Path]::GetTempPath()) 'slidecraft-protection.json'
    $protection = @{
        required_status_checks        = $null
        enforce_admins                = $false
        required_pull_request_reviews = @{ required_approving_review_count = 1 }
        restrictions                  = $null
        allow_force_pushes            = $false
        allow_deletions               = $false
    } | ConvertTo-Json -Depth 5
    [System.IO.File]::WriteAllText($protectionFile, $protection, (New-Object System.Text.UTF8Encoding($false)))

    try {
        Invoke-Gh @(
            'api', '-X', 'PUT', "repos/$Repo/branches/main/protection",
            '-H', 'Accept: application/vnd.github+json',
            '--input', $protectionFile
        ) | Out-Null
        Write-Host "  main protégée (PR + 1 approbation)"
    } catch {
        Write-Warning "Protection non appliquée (droits admin requis). À faire à la main : Settings > Branches."
        Write-Warning $_.Exception.Message
    } finally {
        Remove-Item -LiteralPath $protectionFile -ErrorAction SilentlyContinue
    }
}

# -------------------------------------------------------------------- résumé

Write-Host ""
if ($DryRun) {
    Write-Host "Simulation terminée : aucune modification effectuée." -ForegroundColor Yellow
} else {
    Write-Host "Terminé : $created issue(s) créée(s), $skipped déjà présente(s)." -ForegroundColor Green
    Write-Host "Vérifiez : https://github.com/$Repo/issues" -ForegroundColor Green
}
