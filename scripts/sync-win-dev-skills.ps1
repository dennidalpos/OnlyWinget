# scripts/sync-win-dev-skills.ps1
# Clones and synchronizes WinUI 3 developer skills from the microsoft/win-dev-skills repository.

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoUrl = "https://github.com/microsoft/win-dev-skills.git"
$tempDir = Join-Path $env:TEMP "win-dev-skills-sync"
$rootSkillsDir = Join-Path $PSScriptRoot "../skills"
$agentsSkillsDir = Join-Path $PSScriptRoot "../.agents/skills"

# Create destination folders if not exist
foreach ($dir in @($rootSkillsDir, $agentsSkillsDir)) {
    if (-not (Test-Path $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }
}

# Remove existing temp dir if present
if (Test-Path $tempDir) {
    Remove-Item -Path $tempDir -Recurse -Force | Out-Null
}

Write-Host "Clonazione in corso di $repoUrl..." -ForegroundColor Cyan
git clone --depth 1 $repoUrl $tempDir

$skillsSource = Join-Path $tempDir "plugins/winui/skills"
if (Test-Path $skillsSource) {
    $skills = Get-ChildItem -Path $skillsSource -Directory
    foreach ($skill in $skills) {
        Write-Host "Sincronizzazione della skill: $($skill.Name)..." -ForegroundColor Green
        foreach ($targetDir in @($rootSkillsDir, $agentsSkillsDir)) {
            $destPath = Join-Path $targetDir $skill.Name
            if (Test-Path $destPath) {
                Remove-Item -Path $destPath -Recurse -Force | Out-Null
            }
            Copy-Item -Path $skill.FullName -Destination $targetDir -Recurse -Force
        }
    }
    Write-Host "Tutte le skill sincronizzate con successo in $rootSkillsDir e $agentsSkillsDir" -ForegroundColor Green
} else {
    throw "Impossibile trovare la cartella delle skill nel repository clonato."
}

# Cleanup
if (Test-Path $tempDir) {
    Remove-Item -Path $tempDir -Recurse -Force | Out-Null
}
