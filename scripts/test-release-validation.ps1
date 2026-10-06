$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'support/ScriptHelpers.ps1')

$repoRoot = Split-Path $PSScriptRoot -Parent
$fixtureRoot = Join-Path $repoRoot "tmp/release-regression-$([Guid]::NewGuid().ToString('N'))"
$fixtureRepo = Join-Path $fixtureRoot 'checkout'
$fixtureRemote = Join-Path $fixtureRoot 'remote'
$validator = Join-Path $PSScriptRoot 'validate-release.ps1'

function Assert-ReleaseRejected {
    param([scriptblock]$Action, [string]$Message)
    $rejected = $false
    try { & $Action | Out-Null }
    catch {
        if ($_.Exception.Message -notlike "*$Message*") { throw }
        $rejected = $true
    }
    if (-not $rejected) { throw "Release validation accepted invalid state: $Message" }
}

function Invoke-FixtureGit {
    param([string[]]$Arguments)
    $output = & git @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) { throw "Fixture git failed: $output" }
    return $output
}

try {
    New-Item -ItemType Directory -Path $fixtureRoot -Force | Out-Null
    Invoke-FixtureGit @('clone', '--no-tags', '--shared', '--no-checkout', $repoRoot, $fixtureRepo) | Out-Null
    Invoke-FixtureGit @('-C', $fixtureRepo, 'checkout', '--detach', 'HEAD') | Out-Null
    $projectPath = Join-Path $fixtureRepo 'src/OnlyWinget/OnlyWinget.csproj'
    [xml]$fixtureProject = Get-Content -LiteralPath $projectPath -Raw
    $fixtureVersion = $fixtureProject.SelectSingleNode('/Project/PropertyGroup/Version').InnerText
    $tagName = "v$fixtureVersion"
    $tagSha = Invoke-FixtureGit @('-C', $fixtureRepo, 'rev-parse', 'HEAD')
    Invoke-FixtureGit @('-C', $fixtureRepo, 'tag', $tagName, $tagSha) | Out-Null
    Invoke-FixtureGit @('-C', $fixtureRepo, 'remote', 'set-url', 'origin', $fixtureRepo) | Out-Null

    $release = & $validator -TagName $tagName -RepositoryRoot $fixtureRepo -CheckRemoteTag
    if ($release.CommitSha -ne $tagSha -or $release.Version -ne $fixtureVersion) { throw 'Wrong release identity.' }

    Assert-ReleaseRejected { & $validator -TagName 'main' -RepositoryRoot $fixtureRepo } 'explicit vMAJOR.MINOR.PATCH tag'
    Assert-ReleaseRejected { & $validator -TagName 'v1.0.999999' -RepositoryRoot $fixtureRepo } 'tag does not exist'
    Assert-ReleaseRejected { & $validator -TagName $tagName -RepositoryRoot $fixtureRepo -ExpectedCommit ('0' * 40) } 'commit changed'

    # Reuse existing commits in isolated clones; never commit or modify the user's Git history.
    Invoke-FixtureGit @('-C', $fixtureRepo, 'checkout', '--detach', 'HEAD^') | Out-Null
    Assert-ReleaseRejected { & $validator -TagName $tagName -RepositoryRoot $fixtureRepo } 'HEAD does not match'
    Invoke-FixtureGit @('-C', $fixtureRepo, 'checkout', '--detach', $tagSha) | Out-Null

    $projectText = Get-Content -LiteralPath $projectPath -Raw
    [System.IO.File]::WriteAllText($projectPath, $projectText.Replace("<Version>$fixtureVersion</Version>", '<Version>99.0.987650</Version>'))
    Assert-ReleaseRejected { & $validator -TagName $tagName -RepositoryRoot $fixtureRepo } 'tag/version mismatch'
    [System.IO.File]::WriteAllText($projectPath, $projectText)

    Assert-ReleaseRejected { & $validator -TagName $tagName -RepositoryRoot $fixtureRepo -CheckAssets } 'asset is missing or empty'
    New-Item -ItemType Directory -Path (Split-Path $release.SetupPath -Parent) -Force | Out-Null
    Set-Content -LiteralPath $release.SetupPath -Value 'isolated setup sentinel' -Encoding utf8
    Assert-ReleaseRejected { & $validator -TagName $tagName -RepositoryRoot $fixtureRepo -CheckAssets } 'asset is missing or empty'
    [System.IO.File]::WriteAllBytes($release.PortablePath, [byte[]]@())
    Assert-ReleaseRejected { & $validator -TagName $tagName -RepositoryRoot $fixtureRepo -CheckAssets } 'asset is missing or empty'
    Set-Content -LiteralPath $release.PortablePath -Value 'isolated portable sentinel' -Encoding utf8
    & $validator -TagName $tagName -RepositoryRoot $fixtureRepo -ExpectedCommit $tagSha -CheckRemoteTag -CheckAssets | Out-Null

    Invoke-FixtureGit @('clone', '--no-tags', '--shared', '--no-checkout', $repoRoot, $fixtureRemote) | Out-Null
    $otherSha = Invoke-FixtureGit @('-C', $fixtureRemote, 'rev-parse', 'HEAD^')
    Invoke-FixtureGit @('-C', $fixtureRemote, '-c', 'user.name=Release Fixture', '-c', 'user.email=fixture@example.invalid', 'tag', '-a', $tagName, $otherSha, '-m', 'Isolated tag fixture') | Out-Null
    Invoke-FixtureGit @('-C', $fixtureRepo, 'remote', 'set-url', 'origin', $fixtureRemote) | Out-Null
    Assert-ReleaseRejected { & $validator -TagName $tagName -RepositoryRoot $fixtureRepo -CheckRemoteTag } 'Remote release tag'
    Invoke-FixtureGit @('-C', $fixtureRepo, '-c', 'user.name=Release Fixture', '-c', 'user.email=fixture@example.invalid', 'tag', '-a', 'v1.0.999998', $tagSha, '-m', 'Isolated annotated tag') | Out-Null
    Invoke-FixtureGit @('-C', $fixtureRepo, 'remote', 'set-url', 'origin', $fixtureRepo) | Out-Null
    [System.IO.File]::WriteAllText($projectPath, $projectText.Replace($fixtureVersion, '1.0.999998'))
    & $validator -TagName 'v1.0.999998' -RepositoryRoot $fixtureRepo -CheckRemoteTag | Out-Null
    $global:LASTEXITCODE = 0
    Write-Host 'PASS: Release rejects wrong refs/commits/versions/assets and remote tag drift; lightweight and annotated tags pass.' -ForegroundColor Green
}
finally {
    $cleanupPath = Assert-RepositoryPathInAllowedRoot -Path $fixtureRoot -RepositoryRoot $repoRoot -AllowedRoots @((Join-Path $repoRoot 'tmp'))
    if (Test-Path -LiteralPath $cleanupPath) { Remove-Item -LiteralPath $cleanupPath -Recurse -Force }
}
