$ErrorActionPreference = 'Stop'

$solutionPath = Join-Path $PSScriptRoot 'OnlyWinget.sln'

dotnet build $solutionPath -c Release
