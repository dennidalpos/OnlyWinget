[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'High')]
param(
    [string]$ProjectName = 'OnlyWinget',
    [string]$InstallRoot,
    [string]$RepositoryRoot
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Get-OptionalPropertyValue {
    param(
        [object]$InputObject,
        [string]$PropertyName
    )

    if ($null -eq $InputObject) {
        return $null
    }

    $property = $InputObject.PSObject.Properties[$PropertyName]
    if ($null -eq $property) {
        return $null
    }

    return $property.Value
}

function Test-IsAdministrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Add-MatchTerm {
    param(
        [System.Collections.Generic.HashSet[string]]$Terms,
        [string]$Value
    )

    if ([string]::IsNullOrWhiteSpace($Value)) {
        return
    }

    $normalized = $Value.Trim().Trim('"').ToLowerInvariant()
    if (-not [string]::IsNullOrWhiteSpace($normalized)) {
        $null = $Terms.Add($normalized)
    }
}

function Test-ContainsMatchTerm {
    param(
        [string[]]$Candidates,
        [System.Collections.Generic.HashSet[string]]$Terms
    )

    foreach ($candidate in $Candidates) {
        if ([string]::IsNullOrWhiteSpace($candidate)) {
            continue
        }

        $normalizedCandidate = $candidate.Trim().ToLowerInvariant()
        foreach ($term in $Terms) {
            if ($normalizedCandidate.Contains($term)) {
                return $true
            }
        }
    }

    return $false
}

function Get-ProjectService {
    param(
        [System.Collections.Generic.HashSet[string]]$Terms
    )

    $serviceRoot = 'HKLM:\SYSTEM\CurrentControlSet\Services'

    foreach ($serviceKey in Get-ChildItem -Path $serviceRoot -ErrorAction SilentlyContinue) {
        $serviceProps = Get-ItemProperty -Path $serviceKey.PSPath -ErrorAction SilentlyContinue
        $parametersPath = Join-Path $serviceKey.PSPath 'Parameters'
        $parameterProps = if (Test-Path $parametersPath) {
            Get-ItemProperty -Path $parametersPath -ErrorAction SilentlyContinue
        }
        else {
            $null
        }

        $candidates = @(
            $serviceKey.PSChildName
            (Get-OptionalPropertyValue -InputObject $serviceProps -PropertyName 'DisplayName')
            (Get-OptionalPropertyValue -InputObject $serviceProps -PropertyName 'ImagePath')
            (Get-OptionalPropertyValue -InputObject $parameterProps -PropertyName 'Application')
            (Get-OptionalPropertyValue -InputObject $parameterProps -PropertyName 'AppDirectory')
            (Get-OptionalPropertyValue -InputObject $parameterProps -PropertyName 'AppParameters')
        )

        if (-not (Test-ContainsMatchTerm -Candidates $candidates -Terms $Terms)) {
            continue
        }

        $serviceState = Get-Service -Name $serviceKey.PSChildName -ErrorAction SilentlyContinue

        [PSCustomObject]@{
            Name        = $serviceKey.PSChildName
            DisplayName = Get-OptionalPropertyValue -InputObject $serviceProps -PropertyName 'DisplayName'
            Status      = $serviceState.Status
            ImagePath   = Get-OptionalPropertyValue -InputObject $serviceProps -PropertyName 'ImagePath'
            Application = Get-OptionalPropertyValue -InputObject $parameterProps -PropertyName 'Application'
            AppDirectory = Get-OptionalPropertyValue -InputObject $parameterProps -PropertyName 'AppDirectory'
        }
    }
}

$matchTerms = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
Add-MatchTerm -Terms $matchTerms -Value $ProjectName
Add-MatchTerm -Terms $matchTerms -Value $InstallRoot
Add-MatchTerm -Terms $matchTerms -Value $RepositoryRoot

$services = @(Get-ProjectService -Terms $matchTerms | Sort-Object Name -Unique)

if ($services.Count -eq 0) {
    Write-Host "Nessun servizio associato a '$ProjectName' trovato." -ForegroundColor Green
    return
}

if (-not (Test-IsAdministrator)) {
    throw "Trovati $($services.Count) servizi associati a '$ProjectName', ma per rimuoverli servono privilegi amministrativi."
}

foreach ($service in $services) {
    if (-not $PSCmdlet.ShouldProcess($service.Name, 'Stop and delete Windows service')) {
        continue
    }

    Write-Host "Rimozione servizio '$($service.Name)'..." -ForegroundColor Yellow

    $serviceInstance = Get-Service -Name $service.Name -ErrorAction SilentlyContinue
    if ($null -ne $serviceInstance -and $serviceInstance.Status -ne [System.ServiceProcess.ServiceControllerStatus]::Stopped) {
        try {
            Stop-Service -Name $service.Name -Force -ErrorAction Stop
            $serviceInstance.WaitForStatus([System.ServiceProcess.ServiceControllerStatus]::Stopped, [TimeSpan]::FromSeconds(20))
        }
        catch {
            & sc.exe stop $service.Name | Out-Null
        }
    }

    & sc.exe delete $service.Name | Out-Null

    $serviceRegistryPath = "HKLM:\SYSTEM\CurrentControlSet\Services\$($service.Name)"
    $deadline = (Get-Date).AddSeconds(20)
    while ((Get-Date) -lt $deadline -and (Test-Path $serviceRegistryPath)) {
        Start-Sleep -Milliseconds 300
    }

    if (Test-Path $serviceRegistryPath) {
        throw "Il servizio '$($service.Name)' risulta ancora registrato dopo il tentativo di rimozione."
    }
}

Write-Host "Servizi rimossi: $($services.Count)." -ForegroundColor Green
