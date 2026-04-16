Import-Module PSScriptAnalyzer -ErrorAction Stop
$settingsPath = Join-Path $PSScriptRoot 'PSScriptAnalyzerSettings.psd1'
$scripts = Get-ChildItem $PSScriptRoot -Filter '*.ps1' |
    Where-Object { $_.Name -notlike '_*' }

$anyIssue = $false
foreach ($s in $scripts) {
    $issues = Invoke-ScriptAnalyzer -Path $s.FullName -Settings $settingsPath -Severity Error,Warning
    if ($issues) {
        $anyIssue = $true
        foreach ($i in $issues) {
            Write-Host "$($s.Name):$($i.Line): [$($i.Severity)] $($i.RuleName) - $($i.Message)"
        }
    } else {
        Write-Host "$($s.Name): OK"
    }
}
if (-not $anyIssue) { Write-Host 'All scripts OK' -ForegroundColor Green }
