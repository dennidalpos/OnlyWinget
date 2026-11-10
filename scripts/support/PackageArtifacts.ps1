function Write-PackageJournal {
    param([string]$Path, [object]$State)
    $bytes = [Text.UTF8Encoding]::new($false).GetBytes(($State | ConvertTo-Json -Depth 4))
    $stream = [IO.File]::Open($Path, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
    try { $stream.Write($bytes, 0, $bytes.Length); $stream.Flush($true) }
    finally { $stream.Dispose() }
}

function Remove-PackageRecovery {
    param([string]$Directory)
    # Delete only transaction-owned files, then the empty directory.
    foreach ($name in @('setup.previous', 'portable.previous', 'setup.restore', 'portable.restore', 'state.pending', 'state.json', 'committed.json')) {
        $path = Join-Path $Directory $name
        if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path -Force -ErrorAction Stop }
    }
    Remove-Item -LiteralPath $Directory -ErrorAction Stop
}

function Repair-PackagePromotion {
    param([Parameter(Mandatory)][string]$OutputDirectory)
    $directory = Join-Path $OutputDirectory '.package-recovery'
    if (-not (Test-Path -LiteralPath $directory)) { return }
    $items = @(Get-Item -LiteralPath $directory) + @(Get-ChildItem -LiteralPath $directory -Force)
    if (@($items | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint -or ($_.PSIsContainer -and $_.FullName -ne $directory) }).Count -gt 0) {
        throw 'Package recovery must not contain reparse points or nested directories.'
    }
    $statePath = Join-Path $directory 'state.json'
    if (Test-Path -LiteralPath $statePath) {
        $state = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
        if ($state.Version -ne 1 -or $state.Files.Count -ne 2 -or
            $state.Files[0].Name -notmatch '^OnlyWinget-(1\.0\.\d+)-setup\.exe$' -or
            $state.Files[1].Name -ne ('OnlyWinget-' + $Matches[1] + '-portable-x64.zip')) {
            throw 'Invalid package recovery journal; preserved for inspection.'
        }
        $slots = @('setup', 'portable')
        # Validate every backup before restoring either output.
        for ($i = 0; $i -lt 2; $i++) {
            $entry = $state.Files[$i]
            if ($entry.Previous -isnot [bool]) { throw 'Invalid package recovery ownership.' }
            if ($entry.Previous) {
                $backup = Join-Path $directory ($slots[$i] + '.previous')
                if (-not (Test-Path -LiteralPath $backup) -or (Get-FileHash -LiteralPath $backup).Hash -ne $entry.Hash) {
                    throw 'Package recovery backup missing or modified; preserved for inspection.'
                }
            }
        }
        for ($i = 0; $i -lt 2; $i++) {
            $entry = $state.Files[$i]
            $destination = Join-Path $OutputDirectory $entry.Name
            if ($entry.Previous) {
                if ((Test-Path -LiteralPath $destination) -and (Get-FileHash -LiteralPath $destination).Hash -eq $entry.Hash) { continue }
                $restore = Join-Path $directory ($slots[$i] + '.restore')
                [IO.File]::Copy((Join-Path $directory ($slots[$i] + '.previous')), $restore, $true)
                if (Test-Path -LiteralPath $destination) { [IO.File]::Replace($restore, $destination, [NullString]::Value) }
                else { [IO.File]::Move($restore, $destination) }
            } elseif (Test-Path -LiteralPath $destination) {
                Remove-Item -LiteralPath $destination -Force -ErrorAction Stop
            }
        }
        [IO.File]::Move($statePath, (Join-Path $directory 'committed.json'))
        Write-Warning 'Recovered the previous package artifacts after an incomplete promotion.'
    }
    Remove-PackageRecovery -Directory $directory
}

function Initialize-PackagePromotion {
    param([string]$OutputDirectory, [string[]]$Names)
    Repair-PackagePromotion -OutputDirectory $OutputDirectory
    $directory = Join-Path $OutputDirectory '.package-recovery'
    New-Item -ItemType Directory -Path $directory | Out-Null
    $slots = @('setup', 'portable')
    $entries = for ($i = 0; $i -lt 2; $i++) {
        $destination = Join-Path $OutputDirectory $Names[$i]
        $previous = Test-Path -LiteralPath $destination
        $hash = $null
        if ($previous) {
            $backup = Join-Path $directory ($slots[$i] + '.previous')
            [IO.File]::Copy($destination, $backup)
            $stream = [IO.File]::Open($backup, [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
            try { $stream.Flush($true) } finally { $stream.Dispose() }
            $hash = (Get-FileHash -LiteralPath $backup).Hash
        }
        [pscustomobject]@{ Name = $Names[$i]; Previous = $previous; Hash = $hash }
    }
    $pending = Join-Path $directory 'state.pending'
    Write-PackageJournal -Path $pending -State @{ Version = 1; Files = @($entries) }
    [IO.File]::Move($pending, (Join-Path $directory 'state.json'))
}

function Publish-PackageArtifactPair {
    param([string]$OutputDirectory, [string[]]$StagedPaths)
    $names = @($StagedPaths | ForEach-Object { [IO.Path]::GetFileName($_) })
    if ($names.Count -ne 2 -or $names[0] -notmatch '^OnlyWinget-(1\.0\.\d+)-setup\.exe$' -or
        $names[1] -ne ('OnlyWinget-' + $Matches[1] + '-portable-x64.zip')) { throw 'Unexpected package artifact names.' }
    $directory = Join-Path $OutputDirectory '.package-recovery'
    try {
        Initialize-PackagePromotion -OutputDirectory $OutputDirectory -Names $names
        foreach ($source in $StagedPaths) {
            $destination = Join-Path $OutputDirectory ([IO.Path]::GetFileName($source))
            if (Test-Path -LiteralPath $destination) { [IO.File]::Replace($source, $destination, [NullString]::Value) }
            else { [IO.File]::Move($source, $destination) }
        }
        [IO.File]::Move((Join-Path $directory 'state.json'), (Join-Path $directory 'committed.json'))
    } catch {
        Repair-PackagePromotion -OutputDirectory $OutputDirectory
        throw
    }
    Remove-PackageRecovery -Directory $directory
}

function Assert-PackageArtifactPair {
    param([string]$SetupPath, [string]$PortablePath, [string]$PublishDirectory)
    $stream = [IO.File]::OpenRead($SetupPath)
    try {
        if ($stream.Length -lt 64 -or $stream.ReadByte() -ne 0x4D -or $stream.ReadByte() -ne 0x5A) { throw 'Invalid staged setup executable.' }
    } finally { $stream.Dispose() }
    $files = @(Get-ChildItem -LiteralPath $PublishDirectory -Recurse -File -Force)
    $archive = [IO.Compression.ZipFile]::OpenRead($PortablePath)
    $hasher = [Security.Cryptography.SHA256]::Create()
    try {
        if (@($archive.Entries | Where-Object { $_.Name }).Count -ne $files.Count -or $null -eq $archive.GetEntry('OnlyWinget.exe')) {
            throw 'Staged portable archive has an incomplete payload.'
        }
        foreach ($file in $files) {
            $name = [IO.Path]::GetRelativePath($PublishDirectory, $file.FullName).Replace('\', '/')
            $entry = $archive.GetEntry($name)
            if ($null -eq $entry -or $entry.Length -ne $file.Length) { throw "Staged portable entry mismatch: $name" }
            $entryStream = $entry.Open()
            try { $hash = [BitConverter]::ToString($hasher.ComputeHash($entryStream)).Replace('-', '') }
            finally { $entryStream.Dispose() }
            if ($hash -ne (Get-FileHash -LiteralPath $file.FullName).Hash) { throw "Staged portable content mismatch: $name" }
        }
    } finally { $hasher.Dispose(); $archive.Dispose() }
}

function New-PackageArtifactPair {
    param([string]$OutputDirectory, [string]$PublishDirectory, [string]$Version, [scriptblock]$BuildSetup)
    if ($Version -notmatch '^1\.0\.\d+$') { throw 'Unexpected package version.' }
    Repair-PackagePromotion -OutputDirectory $OutputDirectory
    $stage = Join-Path $OutputDirectory ('.package-stage-' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $stage | Out-Null
    $setup = Join-Path $stage "OnlyWinget-$Version-setup.exe"
    $portable = Join-Path $stage "OnlyWinget-$Version-portable-x64.zip"
    try {
        & $BuildSetup $setup
        [IO.Compression.ZipFile]::CreateFromDirectory($PublishDirectory, $portable, [IO.Compression.CompressionLevel]::Optimal, $false)
        Assert-PackageArtifactPair -SetupPath $setup -PortablePath $portable -PublishDirectory $PublishDirectory
        Publish-PackageArtifactPair -OutputDirectory $OutputDirectory -StagedPaths @($setup, $portable)
    } finally {
        foreach ($path in @($setup, $portable)) {
            if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path -Force -ErrorAction Stop }
        }
        Remove-Item -LiteralPath $stage -ErrorAction Stop
    }
}
