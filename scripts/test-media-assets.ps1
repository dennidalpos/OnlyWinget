$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repositoryRoot = Split-Path $PSScriptRoot -Parent
$fixtureRoot = Join-Path $repositoryRoot ('artifacts/media-tests/' + [Guid]::NewGuid().ToString('N'))
$files = @('assets/logos/logo.png', 'assets/logos/logo.ico', 'landing/assets/logo.png',
    'src/OnlyWinget/Assets/OnlyWinget-icon.png', 'src/OnlyWinget/Assets/OnlyWinget.ico',
    'src/OnlyWinget.Setup/Assets/HeaderBanner.bmp', 'src/OnlyWinget.Setup/Assets/WelcomeDialog.bmp')
try {
    & (Join-Path $PSScriptRoot 'update-media-assets.ps1') -OutputRoot $fixtureRoot
    foreach ($asset in @(@{Name='HeaderBanner.bmp';Width=150;Height=57}, @{Name='WelcomeDialog.bmp';Width=164;Height=314})) {
        $bitmap = [Drawing.Bitmap]::FromFile((Join-Path $fixtureRoot ('src/OnlyWinget.Setup/Assets/' + $asset.Name)))
        try {
            if ($bitmap.Width -ne $asset.Width -or $bitmap.Height -ne $asset.Height -or
                $bitmap.PixelFormat -ne [Drawing.Imaging.PixelFormat]::Format24bppRgb) { throw 'Incorrect NSIS bitmap dimensions/depth.' }
        } finally { $bitmap.Dispose() }
    }
    $icon = [Drawing.Icon]::new((Join-Path $fixtureRoot 'assets/logos/logo.ico'))
    $icon.Dispose()
    if (@(Get-ChildItem -LiteralPath $fixtureRoot -Recurse -File).Count -ne $files.Count) { throw 'Unexpected media output.' }
    foreach ($file in $files) {
        if ((Get-Item -LiteralPath (Join-Path $fixtureRoot $file)).Length -eq 0) { throw "Empty media output: $file" }
    }
    Write-Host 'PASS: isolated NSIS dimensions/depth, readable ICO and exactly seven consumed media outputs.'
}
finally {
    $resolvedRoot = [IO.Path]::GetFullPath($fixtureRoot)
    if (-not $resolvedRoot.StartsWith((Join-Path $repositoryRoot 'artifacts/media-tests/'), [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Unexpected media cleanup path.'
    }
    foreach ($file in $files) {
        $ownedPath = Join-Path $resolvedRoot $file
        if (Test-Path -LiteralPath $ownedPath) { Remove-Item -LiteralPath $ownedPath -Force }
    }
    foreach ($relativeDirectory in @('assets/logos', 'landing/assets', 'src/OnlyWinget/Assets', 'src/OnlyWinget.Setup/Assets',
        'src/OnlyWinget', 'src/OnlyWinget.Setup', 'assets', 'landing', 'src', '')) {
        $directory = if ($relativeDirectory) { Join-Path $resolvedRoot $relativeDirectory } else { $resolvedRoot }
        if (Test-Path -LiteralPath $directory) { Remove-Item -LiteralPath $directory }
    }
}
