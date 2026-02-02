# DataManager.ps1
# Modulo per la gestione della persistenza dati (JSON)

function Import-AppData {
    <#
    .SYNOPSIS
        Carica i dati delle applicazioni dal file JSON.
    .PARAMETER JsonPath
        Percorso del file JSON da caricare.
    .OUTPUTS
        Hashtable con Tabs (hashtable nome->lista) e TabNames (ArrayList ordinato).
    #>
    param(
        [Parameter(Mandatory=$true)]
        [string]$JsonPath
    )

    $tabs = @{}
    $tabNames = New-Object System.Collections.ArrayList

    if (-not (Test-Path $JsonPath)) {
        # File non esiste, restituisci struttura vuota con tab default
        $list = New-Object System.Collections.ArrayList
        $tabs['Default'] = $list
        [void]$tabNames.Add('Default')
        return @{Tabs = $tabs; TabNames = $tabNames}
    }

    try {
        $rawText = Get-Content $JsonPath -Raw
        if ([string]::IsNullOrWhiteSpace($rawText)) {
            # File vuoto
            $list = New-Object System.Collections.ArrayList
            $tabs['Default'] = $list
            [void]$tabNames.Add('Default')
            return @{Tabs = $tabs; TabNames = $tabNames}
        }

        $trimmed = $rawText.TrimStart()

        if ($trimmed.StartsWith("{")) {
            # Formato nuovo con schede
            $raw = $rawText | ConvertFrom-Json
            if ($raw.Tabs) {
                foreach ($tab in $raw.Tabs) {
                    $list = New-Object System.Collections.ArrayList
                    foreach ($app in $tab.Apps) {
                        $action = Get-NormalizedAction $app.Action
                        [void]$list.Add([pscustomobject]@{
                            Name = $app.Name
                            Id = $app.Id
                            Action = $action
                            Status = ""
                        })
                    }
                    $tabs[$tab.Name] = $list
                    [void]$tabNames.Add($tab.Name)
                }
            }
        } else {
            # Formato legacy (array semplice)
            $raw = $rawText | ConvertFrom-Json
            $list = New-Object System.Collections.ArrayList
            foreach ($app in $raw) {
                $action = Get-NormalizedAction $app.Action
                [void]$list.Add([pscustomobject]@{
                    Name = $app.Name
                    Id = $app.Id
                    Action = $action
                    Status = ""
                })
            }
            $tabs['Default'] = $list
            [void]$tabNames.Add('Default')
        }
    } catch {
        # Errore parsing, restituisci struttura vuota
        $list = New-Object System.Collections.ArrayList
        $tabs['Default'] = $list
        [void]$tabNames.Add('Default')
    }

    # Se non ci sono tab, crea uno default
    if ($tabs.Count -eq 0) {
        $list = New-Object System.Collections.ArrayList
        $tabs['Default'] = $list
        [void]$tabNames.Add('Default')
    }

    return @{Tabs = $tabs; TabNames = $tabNames}
}

function Export-AppData {
    <#
    .SYNOPSIS
        Salva i dati delle applicazioni nel file JSON.
    .PARAMETER JsonPath
        Percorso del file JSON.
    .PARAMETER Tabs
        Hashtable con le schede (nome->lista app).
    .PARAMETER TabNames
        ArrayList con i nomi delle schede in ordine.
    .OUTPUTS
        Boolean. True se salvataggio riuscito.
    #>
    param(
        [Parameter(Mandatory=$true)]
        [string]$JsonPath,

        [Parameter(Mandatory=$true)]
        [hashtable]$Tabs,

        [Parameter(Mandatory=$true)]
        [System.Collections.ArrayList]$TabNames
    )

    try {
        $tabsForJson = @()
        foreach ($tabName in $TabNames) {
            $appsForJson = @()
            foreach ($app in $Tabs[$tabName]) {
                $appsForJson += [pscustomobject]@{
                    Name = $app.Name
                    Id = $app.Id
                    Action = $app.Action
                }
            }
            $tabsForJson += [pscustomobject]@{
                Name = $tabName
                Apps = $appsForJson
            }
        }
        $root = [pscustomobject]@{Tabs = $tabsForJson}
        $root | ConvertTo-Json -Depth 5 | Set-Content -Path $JsonPath -Encoding UTF8
        return $true
    } catch {
        return $false
    }
}

function New-AppObject {
    <#
    .SYNOPSIS
        Crea un nuovo oggetto applicazione.
    .PARAMETER Name
        Nome visualizzato dell'applicazione.
    .PARAMETER Id
        ID Winget dell'applicazione.
    .PARAMETER Action
        Azione da eseguire (Install, Uninstall, Pause).
    .OUTPUTS
        PSCustomObject con le proprieta' dell'app.
    #>
    param(
        [Parameter(Mandatory=$true)]
        [string]$Name,

        [Parameter(Mandatory=$true)]
        [string]$Id,

        [string]$Action = "Install"
    )

    return [pscustomobject]@{
        Name = $Name
        Id = $Id
        Action = Get-NormalizedAction $Action
        Status = ""
    }
}

function Get-NormalizedAction {
    <#
    .SYNOPSIS
        Normalizza l'azione di un'applicazione.
    .PARAMETER Action
        Valore dell'azione da normalizzare.
    .OUTPUTS
        String. Action valida (Install, Uninstall, Pause).
    #>
    param([string]$Action)

    if ([string]::IsNullOrWhiteSpace($Action)) {
        return "Install"
    }

    switch ($Action) {
        "Install" { return "Install" }
        "Uninstall" { return "Uninstall" }
        "Pause" { return "Pause" }
        default { return "Install" }
    }
}

function Add-AppToTab {
    <#
    .SYNOPSIS
        Aggiunge un'applicazione a una scheda.
    .PARAMETER Tabs
        Hashtable delle schede.
    .PARAMETER TabName
        Nome della scheda.
    .PARAMETER App
        Oggetto applicazione da aggiungere.
    .OUTPUTS
        Boolean. True se aggiunta riuscita, False se ID duplicato.
    #>
    param(
        [Parameter(Mandatory=$true)]
        [hashtable]$Tabs,

        [Parameter(Mandatory=$true)]
        [string]$TabName,

        [Parameter(Mandatory=$true)]
        [pscustomobject]$App
    )

    if (-not $Tabs.ContainsKey($TabName)) {
        return $false
    }

    $list = $Tabs[$TabName]

    # Verifica duplicati
    foreach ($existingApp in $list) {
        if ($existingApp.Id -eq $App.Id) {
            return $false
        }
    }

    [void]$list.Add($App)
    return $true
}

function Remove-AppFromTab {
    <#
    .SYNOPSIS
        Rimuove un'applicazione da una scheda.
    .PARAMETER Tabs
        Hashtable delle schede.
    .PARAMETER TabName
        Nome della scheda.
    .PARAMETER App
        Oggetto applicazione da rimuovere.
    .OUTPUTS
        Boolean. True se rimozione riuscita.
    #>
    param(
        [Parameter(Mandatory=$true)]
        [hashtable]$Tabs,

        [Parameter(Mandatory=$true)]
        [string]$TabName,

        [Parameter(Mandatory=$true)]
        [pscustomobject]$App
    )

    if (-not $Tabs.ContainsKey($TabName)) {
        return $false
    }

    [void]$Tabs[$TabName].Remove($App)
    return $true
}

function New-Tab {
    <#
    .SYNOPSIS
        Crea una nuova scheda.
    .PARAMETER Tabs
        Hashtable delle schede.
    .PARAMETER TabNames
        ArrayList dei nomi schede.
    .PARAMETER Name
        Nome della nuova scheda.
    .OUTPUTS
        Boolean. True se creazione riuscita, False se nome duplicato.
    #>
    param(
        [Parameter(Mandatory=$true)]
        [hashtable]$Tabs,

        [Parameter(Mandatory=$true)]
        [System.Collections.ArrayList]$TabNames,

        [Parameter(Mandatory=$true)]
        [string]$Name
    )

    if ($Tabs.ContainsKey($Name)) {
        return $false
    }

    $list = New-Object System.Collections.ArrayList
    $Tabs[$Name] = $list
    [void]$TabNames.Add($Name)
    return $true
}

function Rename-Tab {
    <#
    .SYNOPSIS
        Rinomina una scheda esistente.
    .PARAMETER Tabs
        Hashtable delle schede.
    .PARAMETER TabNames
        ArrayList dei nomi schede.
    .PARAMETER OldName
        Nome attuale della scheda.
    .PARAMETER NewName
        Nuovo nome della scheda.
    .OUTPUTS
        Boolean. True se rinomina riuscita.
    #>
    param(
        [Parameter(Mandatory=$true)]
        [hashtable]$Tabs,

        [Parameter(Mandatory=$true)]
        [System.Collections.ArrayList]$TabNames,

        [Parameter(Mandatory=$true)]
        [string]$OldName,

        [Parameter(Mandatory=$true)]
        [string]$NewName
    )

    if (-not $Tabs.ContainsKey($OldName)) {
        return $false
    }

    if ($Tabs.ContainsKey($NewName)) {
        return $false
    }

    $tabList = $Tabs[$OldName]
    [void]$Tabs.Remove($OldName)
    $Tabs[$NewName] = $tabList

    $index = $TabNames.IndexOf($OldName)
    if ($index -ge 0) {
        $TabNames[$index] = $NewName
    }

    return $true
}

function Remove-Tab {
    <#
    .SYNOPSIS
        Rimuove una scheda.
    .PARAMETER Tabs
        Hashtable delle schede.
    .PARAMETER TabNames
        ArrayList dei nomi schede.
    .PARAMETER Name
        Nome della scheda da rimuovere.
    .OUTPUTS
        Boolean. True se rimozione riuscita, False se e' l'unica scheda.
    #>
    param(
        [Parameter(Mandatory=$true)]
        [hashtable]$Tabs,

        [Parameter(Mandatory=$true)]
        [System.Collections.ArrayList]$TabNames,

        [Parameter(Mandatory=$true)]
        [string]$Name
    )

    # Non permettere di eliminare l'unica scheda
    if ($Tabs.Count -le 1) {
        return $false
    }

    if (-not $Tabs.ContainsKey($Name)) {
        return $false
    }

    [void]$Tabs.Remove($Name)
    [void]$TabNames.Remove($Name)
    return $true
}

function Test-DuplicateAppId {
    <#
    .SYNOPSIS
        Verifica se un ID app e' gia' presente nella lista.
    .PARAMETER AppsList
        Lista delle applicazioni.
    .PARAMETER Id
        ID da verificare.
    .OUTPUTS
        Boolean. True se duplicato.
    #>
    param(
        [Parameter(Mandatory=$true)]
        $AppsList,

        [Parameter(Mandatory=$true)]
        [string]$Id
    )

    foreach ($app in $AppsList) {
        if ($app.Id -eq $Id) {
            return $true
        }
    }
    return $false
}

# Esporta le funzioni
Export-ModuleMember -Function @(
    'Import-AppData',
    'Export-AppData',
    'New-AppObject',
    'Add-AppToTab',
    'Remove-AppFromTab',
    'New-Tab',
    'Rename-Tab',
    'Remove-Tab',
    'Test-DuplicateAppId'
)
