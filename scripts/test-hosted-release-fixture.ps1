param(
    [Parameter(Mandatory)]
    [string]$OutputDirectory
)

# AUDIT-12 branch-only fixture. Do not merge its release.yml into production.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repoRoot = Split-Path $PSScriptRoot -Parent
$baseCommit = '52ddde918ffb92e5afb675700198e00b399d2a1e'
$workflowPath = Join-Path $repoRoot '.github/workflows/release.yml'
$workflow = [IO.File]::ReadAllText($workflowPath).Replace("`r`n", "`n")
$originalLines = & git -C $repoRoot show "${baseCommit}:.github/workflows/release.yml"
if ($LASTEXITCODE -ne 0) { throw 'Cannot read the qualified base workflow.' }
$original = $originalLines -join "`n"

function Get-WorkflowStep {
    param([string]$Text, [string]$Name)
    $pattern = '(?ms)^      - name: ' + [regex]::Escape($Name) + '\n.*?(?=^      - name: |\z)'
    $stepMatches = [regex]::Matches($Text, $pattern)
    if ($stepMatches.Count -ne 1) { throw "Expected one workflow step: $Name" }
    return $stepMatches[0].Value.TrimEnd()
}

function Get-WorkflowRun {
    param([string]$Text, [string]$Name)
    $lines = (Get-WorkflowStep -Text $Text -Name $Name) -split "`n"
    $start = [Array]::IndexOf($lines, '        run: |')
    if ($start -lt 0) { throw "No inline command in workflow step: $Name" }
    $body = [Collections.Generic.List[string]]::new()
    for ($index = $start + 1; $index -lt $lines.Count; $index++) {
        if ($lines[$index].Length -eq 0) { $body.Add(''); continue }
        if (-not $lines[$index].StartsWith('          ', [StringComparison]::Ordinal)) { break }
        $body.Add($lines[$index].Substring(10))
    }
    return ($body -join "`n").TrimEnd()
}

if ($workflow -notmatch '(?m)^permissions:\n  contents: read$' -or $workflow -match '(?m)contents: write|continue-on-error|^  push:') {
    throw 'Qualification must be manual-only and read-only, with normal failure propagation.'
}
if (-not $workflow.Contains('ref: ${{ github.sha }}') -or -not $workflow.Contains('persist-credentials: false')) {
    throw 'Qualification must check out the dispatch SHA without persisting credentials.'
}
foreach ($step in @('Validate Release Tag and Version', 'Verify and Package Release Commit', 'Validate Verified Commit and Required Assets')) {
    if ((Get-WorkflowRun -Text $workflow -Name $step) -cne (Get-WorkflowRun -Text $original -Name $step)) {
        throw "Production release command changed in the fixture: $step"
    }
}
if ((Get-WorkflowStep -Text $workflow -Name 'Create GitHub Release') -cne (Get-WorkflowStep -Text $original -Name 'Create GitHub Release')) {
    throw 'The publisher/default success condition must remain identical to production.'
}
$lastPosition = -1
foreach ($step in @('Prepare runner-only negative release identity', 'Validate Release Tag and Version', 'Setup .NET SDK', 'Install NSIS', 'Inject runner-only locked restore failure', 'Verify and Package Release Commit', 'Validate Verified Commit and Required Assets', 'Create GitHub Release', 'Upload verification diagnostics')) {
    $position = $workflow.IndexOf('      - name: ' + $step, [StringComparison]::Ordinal)
    if ($position -le $lastPosition) { throw "Release guard ordering changed: $step" }
    $lastPosition = $position
}

$outputRoot = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $outputRoot) { throw 'Use a new evidence directory; retained qualification evidence is preserved.' }
New-Item -ItemType Directory -Path $outputRoot | Out-Null
$prepare = [scriptblock]::Create((Get-WorkflowRun -Text $workflow -Name 'Prepare runner-only negative release identity'))
$inject = [scriptblock]::Create((Get-WorkflowRun -Text $workflow -Name 'Inject runner-only locked restore failure'))
$environmentNames = @('ONLYWINGET_RELEASE_QUALIFICATION', 'QUALIFICATION_CASE', 'RUNNER_TEMP', 'GITHUB_OUTPUT')
$savedEnvironment = @{}
foreach ($name in $environmentNames) { $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process') }
$results = [Collections.Generic.List[object]]::new()

function Invoke-CapturedScript {
    param([string]$Path, [string[]]$Arguments, [string]$WorkingDirectory, [string]$LogPath)
    $start = [Diagnostics.ProcessStartInfo]::new((Get-Command pwsh -ErrorAction Stop).Source)
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.WorkingDirectory = $WorkingDirectory
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    foreach ($argument in @('-NoProfile', '-File', $Path) + $Arguments) { $start.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::Start($start)
    try {
        $output = $process.StandardOutput.ReadToEndAsync()
        $errors = $process.StandardError.ReadToEndAsync()
        $process.WaitForExit()
        $text = $output.Result + $errors.Result
        [IO.File]::WriteAllText($LogPath, $text)
        return [pscustomobject]@{ ExitCode=$process.ExitCode; Text=$text }
    }
    finally { $process.Dispose() }
}

try {
    foreach ($case in @('tag-mismatch', 'gate-failure')) {
        $caseRoot = Join-Path $outputRoot $case
        $checkout = Join-Path $caseRoot 'checkout'
        New-Item -ItemType Directory -Path $caseRoot | Out-Null
        $cloneOutput = & git clone --shared --no-tags --no-checkout $repoRoot $checkout 2>&1
        if ($LASTEXITCODE -ne 0) { throw "Fixture clone failed: $cloneOutput" }
        $checkoutOutput = & git -C $checkout checkout --detach $baseCommit 2>&1
        if ($LASTEXITCODE -ne 0) { throw "Fixture checkout failed: $checkoutOutput" }
        $env:ONLYWINGET_RELEASE_QUALIFICATION = 'true'
        $env:QUALIFICATION_CASE = $case
        $env:RUNNER_TEMP = Join-Path $caseRoot 'runner-temp'
        $env:GITHUB_OUTPUT = Join-Path $caseRoot 'github-output.txt'
        Push-Location $checkout
        try { & $prepare }
        finally { Pop-Location }
        $identityPath = Join-Path $env:RUNNER_TEMP 'onlywinget-audit12/identity.json'
        $identity = Get-Content -LiteralPath $identityPath -Raw | ConvertFrom-Json
        $validation = Invoke-CapturedScript -Path (Join-Path $checkout 'scripts/validate-release.ps1') -Arguments @('-TagName', $identity.LocalTag) -WorkingDirectory $checkout -LogPath (Join-Path $caseRoot 'validate-release.log')
        if ($case -eq 'tag-mismatch') {
            if ($validation.ExitCode -eq 0 -or $validation.Text -notmatch 'Release tag/version mismatch') {
                throw 'The explicit existing-tag/version mismatch did not fail in the production validator.'
            }
            $results.Add([pscustomobject]@{ Case=$case; Passed=$true; ExpectedStep='Validate Release Tag and Version'; ExitCode=$validation.ExitCode; Hosted=$false })
            continue
        }
        if ($validation.ExitCode -ne 0) { throw "Valid runner-only tag was rejected: $($validation.Text)" }
        Push-Location $checkout
        try { & $inject }
        finally { Pop-Location }
        $gate = Invoke-CapturedScript -Path (Join-Path $checkout 'scripts/run.ps1') -Arguments @('-Task', 'Check', '-Configuration', 'Release', '-NonInteractive') -WorkingDirectory $checkout -LogPath (Join-Path $caseRoot 'check.log')
        $report = Get-Content -LiteralPath (Join-Path $checkout 'artifacts/build-report.txt') -Raw
        $restore = Get-Content -LiteralPath (Join-Path $checkout 'artifacts/check/restore.log') -Raw
        if ($gate.ExitCode -eq 0 -or $report -notmatch 'FailedStep: restore' -or $restore -notmatch 'NU1004') {
            throw 'The unchanged full Check entrypoint did not reject the injected real locked restore failure.'
        }
        $results.Add([pscustomobject]@{ Case=$case; Passed=$true; ExpectedStep='Verify and Package Release Commit / restore'; ExitCode=$gate.ExitCode; NativeFailure='NU1004'; Hosted=$false })
    }
    $results | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $outputRoot 'results.json') -Encoding utf8
    Write-Output 'PASS: production release commands/publisher retained, read-only manual fixture verified, both real negative cases rejected locally. Hosted step skipping remains unqualified.'
}
finally {
    foreach ($name in $environmentNames) { [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name], 'Process') }
}
