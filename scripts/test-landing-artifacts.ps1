$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'support/LandingArtifacts.ps1')
$repositoryRoot = Split-Path $PSScriptRoot -Parent
$fixtureRoot = Join-Path $repositoryRoot ('artifacts/landing-tests/' + [Guid]::NewGuid().ToString('N'))
$directories = @('src/OnlyWinget', 'artifacts/dist/OnlyWinget/Release', 'landing/build')
$files = @('src/OnlyWinget/OnlyWinget.csproj', 'landing/index.html', 'landing/build/user.txt',
    'artifacts/dist/OnlyWinget/Release/OnlyWinget-1.0.1-setup.exe', 'artifacts/dist/OnlyWinget/Release/OnlyWinget-1.0.1-portable-x64.zip',
    'artifacts/dist/OnlyWinget/Release/OnlyWinget-1.0.2-setup.exe', 'artifacts/dist/OnlyWinget/Release/OnlyWinget-1.0.2-portable-x64.zip',
    'landing/build/OnlyWinget-1.0.2-setup.exe', 'landing/build/OnlyWinget-1.0.2-portable-x64.zip')
try {
    foreach ($directory in $directories) { New-Item -ItemType Directory -Path (Join-Path $fixtureRoot $directory) -Force | Out-Null }
    [IO.File]::WriteAllText((Join-Path $fixtureRoot $files[0]), '<Project><PropertyGroup><Version>1.0.2</Version></PropertyGroup></Project>')
    $htmlPath = Join-Path $fixtureRoot $files[1]
    $html = '<a href="build/OnlyWinget-1.0.1-setup.exe">Setup</a><a href="build/OnlyWinget-1.0.1-portable-x64.zip">Portable</a>'
    [IO.File]::WriteAllText($htmlPath, $html)
    [IO.File]::WriteAllText((Join-Path $fixtureRoot $files[2]), 'preserve user file')
    # Sentinel artifacts test exact version selection; real package validity is verified separately.
    foreach ($index in @(3, 4)) { [IO.File]::WriteAllText((Join-Path $fixtureRoot $files[$index]), 'old release') }
    $failed = $false
    try { Copy-CurrentLandingArtifactPair -RepositoryRoot $fixtureRoot } catch {
        if ($_.Exception.Message -notmatch 'missing or empty') { throw }
        $failed = $true
    }
    if (-not $failed -or [IO.File]::ReadAllText($htmlPath) -cne $html) { throw 'Missing current artifacts were not rejected before HTML mutation.' }
    foreach ($index in @(5, 6)) { [IO.File]::WriteAllText((Join-Path $fixtureRoot $files[$index]), 'current release') }
    Copy-CurrentLandingArtifactPair -RepositoryRoot $fixtureRoot
    if ([IO.File]::ReadAllText($htmlPath) -match '1.0.1' -or @(Get-ChildItem -LiteralPath (Join-Path $fixtureRoot 'landing/build') -File).Count -ne 3) {
        throw 'Prepared landing selected an old release or deleted unrelated files.'
    }
    foreach ($index in @(7, 8)) {
        if ([IO.File]::ReadAllText((Join-Path $fixtureRoot $files[$index])) -ne 'current release') { throw 'Wrong artifact content copied.' }
    }
    if ([IO.File]::ReadAllText((Join-Path $fixtureRoot $files[2])) -ne 'preserve user file') { throw 'Unrelated landing file changed.' }
    Write-Host 'PASS: exact current-version landing artifacts/links; missing current pair fails; unrelated files preserved.'
}
finally {
    $resolvedRoot = [IO.Path]::GetFullPath($fixtureRoot)
    if (-not $resolvedRoot.StartsWith((Join-Path $repositoryRoot 'artifacts/landing-tests/'), [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Unexpected landing cleanup path.'
    }
    foreach ($file in $files) {
        $ownedPath = Join-Path $resolvedRoot $file
        if (Test-Path -LiteralPath $ownedPath) { Remove-Item -LiteralPath $ownedPath -Force }
    }
    foreach ($directory in @('src/OnlyWinget', 'artifacts/dist/OnlyWinget/Release', 'artifacts/dist/OnlyWinget', 'artifacts/dist',
        'landing/build', 'src', 'artifacts', 'landing', '')) {
        $ownedPath = if ($directory) { Join-Path $resolvedRoot $directory } else { $resolvedRoot }
        if (Test-Path -LiteralPath $ownedPath) { Remove-Item -LiteralPath $ownedPath }
    }
}
