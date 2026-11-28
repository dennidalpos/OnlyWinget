function Write-InstallerFileInclude {
    param(
        [Parameter(Mandatory)]
        [string]$PublishDirectory,
        [Parameter(Mandatory)]
        [string]$OutputPath
    )

    $publishRoot = (Resolve-Path -LiteralPath $PublishDirectory -ErrorAction Stop).Path
    $entries = @(Get-ChildItem -LiteralPath $publishRoot -Recurse -Force -ErrorAction Stop)
    if (@($entries | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }).Count -gt 0) {
        throw 'Installer publish directory must not contain reparse points.'
    }

    $files = @($entries | Where-Object { -not $_.PSIsContainer } | Sort-Object FullName)
    if ($files.Count -eq 0) {
        throw 'Installer publish directory contains no files.'
    }

    $lines = [Collections.Generic.List[string]]::new()
    $lines.Add('; Generated from the publish directory. Do not edit.')
    $lines.Add('!macro OnlyWingetInstallFiles')
    foreach ($file in $files) {
        $relativePath = [IO.Path]::GetRelativePath($publishRoot, $file.FullName)
        $relativeDirectory = [IO.Path]::GetDirectoryName($relativePath)
        $escapedDirectory = $relativeDirectory.Replace('$', '$$')
        $escapedFileName = $file.Name.Replace('$', '$$')
        $outputDirectory = if ([string]::IsNullOrEmpty($escapedDirectory)) { '$INSTDIR' } else { '$INSTDIR\' + $escapedDirectory }
        $lines.Add('  SetOutPath "' + $outputDirectory + '"')
        $lines.Add('  File "/oname=' + $escapedFileName + '" "${PUBLISH_DIR}\' + $relativePath + '"')
    }
    $lines.Add('!macroend')
    $lines.Add('!macro OnlyWingetUninstallFiles')
    foreach ($file in $files) {
        $escapedPath = [IO.Path]::GetRelativePath($publishRoot, $file.FullName).Replace('$', '$$')
        $lines.Add('  Delete "$INSTDIR\' + $escapedPath + '"')
    }
    $directories = @($entries | Where-Object { $_.PSIsContainer } | Sort-Object { $_.FullName.Length } -Descending)
    foreach ($directory in $directories) {
        $escapedPath = [IO.Path]::GetRelativePath($publishRoot, $directory.FullName).Replace('$', '$$')
        $lines.Add('  RMDir "$INSTDIR\' + $escapedPath + '"')
    }
    $lines.Add('!macroend')
    $lines | Set-Content -LiteralPath $OutputPath -Encoding utf8 -ErrorAction Stop
}
