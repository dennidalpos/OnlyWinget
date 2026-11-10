param(
    [Parameter(Mandatory)]
    [string]$TagName,
    [string]$RepositoryRoot = (Split-Path $PSScriptRoot -Parent),
    [string]$ExpectedCommit,
    [switch]$CheckRemoteTag,
    [switch]$CheckAssets
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ($TagName -notmatch '^v(?<version>[0-9]+\.[0-9]+\.[0-9]+)$') {
    throw 'Release requires an explicit vMAJOR.MINOR.PATCH tag; branch dispatch without a tag is unsupported.'
}
$version = $Matches.version
$tagCommit = git -C $RepositoryRoot rev-parse --verify "refs/tags/$TagName^{commit}" 2>$null
if ($LASTEXITCODE -ne 0) { throw "Release tag does not exist: $TagName" }
$headCommit = git -C $RepositoryRoot rev-parse --verify HEAD
if ($LASTEXITCODE -ne 0) { throw 'Cannot resolve release checkout HEAD.' }
if ($headCommit -ne $tagCommit) { throw 'Release checkout HEAD does not match the tag commit.' }
if ($ExpectedCommit -and $tagCommit -ne $ExpectedCommit) { throw 'Release commit changed after verification.' }

[xml]$project = Get-Content -LiteralPath (Join-Path $RepositoryRoot 'src/OnlyWinget/OnlyWinget.csproj') -Raw
foreach ($property in @('Version', 'InformationalVersion', 'AssemblyVersion', 'FileVersion')) {
    $value = @($project.SelectNodes("/Project/PropertyGroup/$property"))
    $expectedVersion = if ($property -in @('AssemblyVersion', 'FileVersion')) { "$version.0" } else { $version }
    if ($value.Count -ne 1 -or $value[0].InnerText -ne $expectedVersion) {
        throw "Release tag/version mismatch: $property must be $expectedVersion."
    }
}

if ($CheckRemoteTag) {
    $remoteRefs = @(git -C $RepositoryRoot ls-remote --tags origin "refs/tags/$TagName" "refs/tags/$TagName^{}")
    if ($LASTEXITCODE -ne 0) { throw 'Cannot verify the remote release tag.' }
    $peeledRefs = @($remoteRefs | Where-Object { $_.EndsWith("refs/tags/$TagName^{}", [StringComparison]::Ordinal) })
    $commitRefs = @(if ($peeledRefs.Count -eq 1) { $peeledRefs } else { $remoteRefs })
    if ($commitRefs.Count -ne 1 -or ($commitRefs[0] -split '\s+')[0] -ne $tagCommit) {
        throw 'Remote release tag is missing or no longer points to the verified commit.'
    }
}

$distPath = Join-Path $RepositoryRoot 'artifacts/dist/OnlyWinget/Release'
$setupPath = Join-Path $distPath "OnlyWinget-$version-setup.exe"
$portablePath = Join-Path $distPath "OnlyWinget-$version-portable-x64.zip"
if ($CheckAssets) {
    foreach ($assetPath in @($setupPath, $portablePath)) {
        if (-not (Test-Path -LiteralPath $assetPath -PathType Leaf) -or (Get-Item -LiteralPath $assetPath).Length -eq 0) {
            throw "Required release asset is missing or empty: $assetPath"
        }
    }
}

[pscustomobject]@{
    TagName = $TagName
    Version = $version
    CommitSha = $tagCommit
    SetupPath = $setupPath
    PortablePath = $portablePath
}
