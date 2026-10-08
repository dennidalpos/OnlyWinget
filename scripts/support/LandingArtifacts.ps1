function Copy-CurrentLandingArtifactPair {
    param([string]$RepositoryRoot, [string]$Configuration = 'Release')
    [xml]$project = Get-Content -LiteralPath (Join-Path $RepositoryRoot 'src/OnlyWinget/OnlyWinget.csproj')
    $version = [string]($project.Project.PropertyGroup | Where-Object Version | Select-Object -ExpandProperty Version -First 1)
    if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'Project release version is missing or invalid.' }
    $names = @("OnlyWinget-$version-setup.exe", "OnlyWinget-$version-portable-x64.zip")
    $dist = Join-Path $RepositoryRoot "artifacts/dist/OnlyWinget/$Configuration"
    foreach ($name in $names) {
        $source = Join-Path $dist $name
        if (-not (Test-Path -LiteralPath $source -PathType Leaf) -or (Get-Item -LiteralPath $source).Length -eq 0) {
            throw "Current release artifact is missing or empty: $source"
        }
    }
    $htmlPath = Join-Path $RepositoryRoot 'landing/index.html'
    $html = [IO.File]::ReadAllText($htmlPath)
    $patterns = @('href="build/(?:OnlyWinget-[\d.]+-setup|setup)\.exe"',
        'href="build/(?:OnlyWinget-[\d.]+-portable-x64|portable)\.zip"')
    for ($index = 0; $index -lt $names.Count; $index++) {
        if ([regex]::Matches($html, $patterns[$index]).Count -ne 1) { throw 'Expected one supported download link per release artifact.' }
        $html = [regex]::Replace($html, $patterns[$index], ('href="build/' + $names[$index] + '"'))
    }
    $build = Join-Path $RepositoryRoot 'landing/build'
    New-Item -ItemType Directory -Path $build -Force | Out-Null
    foreach ($name in $names) { Copy-Item -LiteralPath (Join-Path $dist $name) -Destination (Join-Path $build $name) -Force }
    [IO.File]::WriteAllText($htmlPath, $html, [Text.UTF8Encoding]::new($false))
    Write-Host "PASS: landing links and both prepared downloads match $version."
}
