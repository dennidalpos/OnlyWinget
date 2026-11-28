function Copy-ThirdPartyNotice {
    param(
        [string]$PublishDirectory,
        [string]$AssetsPath,
        [string]$NoticeSource
    )

    $manifest = Get-Content -LiteralPath (Join-Path $NoticeSource 'packages.json') -Raw | ConvertFrom-Json -AsHashtable
    $assets = Get-Content -LiteralPath $AssetsPath -Raw | ConvertFrom-Json -AsHashtable
    $deps = Get-Content -LiteralPath (Join-Path $PublishDirectory 'OnlyWinget.deps.json') -Raw | ConvertFrom-Json -AsHashtable
    $packages = @($deps.libraries.Keys | Where-Object { $deps.libraries[$_].type -in 'package', 'runtimepack' } |
        ForEach-Object { $_ -replace '^runtimepack\.', '' })
    # These native build inputs can ship resources without appearing in the published deps file.
    $packages += @($assets.libraries.Keys | Where-Object { $_ -match '^Microsoft\.WindowsAppSDK\.(Base|DWrite|Runtime)/' })
    $drift = @(Compare-Object -ReferenceObject @($manifest.Keys) -DifferenceObject @($packages | Sort-Object -Unique))
    if ($drift.Count -gt 0) {
        throw "Review third-party notices before packaging this dependency graph: $($drift.InputObject -join ', ')."
    }

    $destination = Join-Path $PublishDirectory 'ThirdPartyNotices'
    New-Item -ItemType Directory -Path $destination -ErrorAction Stop | Out-Null
    Copy-Item -LiteralPath (Join-Path $NoticeSource 'README.txt') -Destination $destination -ErrorAction Stop
    foreach ($package in $manifest.Keys) {
        if ($package -notmatch '^[A-Za-z0-9][A-Za-z0-9._-]*/[0-9][A-Za-z0-9.+-]*$' -or @($manifest[$package]).Count -eq 0) {
            throw "Invalid third-party notice manifest entry: $package."
        }
        $packagePath = $package.ToLowerInvariant()
        $packageRoot = $assets.packageFolders.Keys | ForEach-Object { Join-Path $_ $packagePath } |
            Where-Object { Test-Path -LiteralPath $_ -PathType Container } | Select-Object -First 1
        if (!$packageRoot) { throw "Restored package is missing for third-party notices: $package." }

        $packageDestination = Join-Path $destination $package
        New-Item -ItemType Directory -Path $packageDestination -Force -ErrorAction Stop | Out-Null
        foreach ($entry in $manifest[$package]) {
            if ($entry -notmatch '^@?[A-Za-z0-9][A-Za-z0-9._-]*$') {
                throw "Invalid notice filename for ${package}: $entry."
            }
            $sourceRoot = if ($entry.StartsWith('@')) { $NoticeSource } else { $packageRoot }
            $source = Join-Path $sourceRoot $entry.TrimStart('@')
            if (!(Test-Path -LiteralPath $source -PathType Leaf) -or (Get-Item -LiteralPath $source).Length -eq 0) {
                throw "Missing or empty third-party notice for ${package}: $entry."
            }
            Copy-Item -LiteralPath $source -Destination $packageDestination -ErrorAction Stop
        }
    }
}

function Assert-NsisNoticeSource {
    param([string]$Compiler, [string]$NoticeDirectory)

    $version = (& $Compiler /VERSION | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or $version -cne 'v3.13') {
        throw "Review the bundled NSIS source and licenses before packaging with compiler '$version'. Expected v3.13."
    }
    $archive = Join-Path $NoticeDirectory 'nsis-3.13-src.tar.bz2'
    if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256 -ErrorAction Stop).Hash -ne
        'A8FFE024602D46B6D766F9E1CE30C324AD2A24DAEACD3EFC2642D436A0C157AC') {
        throw 'The NSIS 3.13 source archive does not match the published SHA-256.'
    }
    foreach ($name in @('COPYING.txt', 'README.txt')) {
        if ((Get-Item -LiteralPath (Join-Path $NoticeDirectory $name) -ErrorAction Stop).Length -eq 0) {
            throw "The NSIS notice is empty: $name."
        }
    }
}
