
function Import-AppData {
    
    param(
        [Parameter(Mandatory=$true)]
        [string]$JsonPath
    )

    $tabs = @{}
    $tabNames = New-Object System.Collections.ArrayList

    if (-not (Test-Path $JsonPath)) {
        $list = New-Object System.Collections.ArrayList
        $tabs['Default'] = $list
        [void]$tabNames.Add('Default')
        return @{Tabs = $tabs; TabNames = $tabNames}
    }

    try {
        $rawText = Get-Content $JsonPath -Raw
        if ([string]::IsNullOrWhiteSpace($rawText)) {
            $list = New-Object System.Collections.ArrayList
            $tabs['Default'] = $list
            [void]$tabNames.Add('Default')
            return @{Tabs = $tabs; TabNames = $tabNames}
        }

        $trimmed = $rawText.TrimStart()

        if ($trimmed.StartsWith("{")) {
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
        $list = New-Object System.Collections.ArrayList
        $tabs['Default'] = $list
        [void]$tabNames.Add('Default')
    }

    if ($tabs.Count -eq 0) {
        $list = New-Object System.Collections.ArrayList
        $tabs['Default'] = $list
        [void]$tabNames.Add('Default')
    }

    return @{Tabs = $tabs; TabNames = $tabNames}
}

function Export-AppData {
    
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

    foreach ($existingApp in $list) {
        if ($existingApp.Id -eq $App.Id) {
            return $false
        }
    }

    [void]$list.Add($App)
    return $true
}

function Remove-AppFromTab {
    
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
    
    param(
        [Parameter(Mandatory=$true)]
        [hashtable]$Tabs,

        [Parameter(Mandatory=$true)]
        [System.Collections.ArrayList]$TabNames,

        [Parameter(Mandatory=$true)]
        [string]$Name
    )

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
