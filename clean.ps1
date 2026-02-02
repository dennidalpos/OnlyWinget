$ErrorActionPreference = 'Stop'

$solutionPath = Join-Path $PSScriptRoot 'OnlyWinget.sln'

dotnet clean $solutionPath -c Release
