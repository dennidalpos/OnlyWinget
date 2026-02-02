# WingetUtils.ps1
# Modulo per l'interazione con Windows Package Manager (winget)

function Test-WingetAvailable {
    <#
    .SYNOPSIS
        Verifica se winget e' disponibile nel sistema.
    .OUTPUTS
        Boolean. True se winget e' disponibile, False altrimenti.
    #>
    try {
        $null = & winget --version 2>&1
        return $LASTEXITCODE -eq 0
    } catch {
        return $false
    }
}

function Invoke-Winget {
    <#
    .SYNOPSIS
        Esegue un comando winget con i parametri specificati.
    .PARAMETER Command
        Il comando winget da eseguire (install, uninstall, upgrade, search, show).
    .PARAMETER Params
        Hashtable con i parametri da passare al comando.
    .OUTPUTS
        Hashtable con ExitCode e Output del comando.
    #>
    param(
        [Parameter(Mandatory=$true)]
        [string]$Command,

        [hashtable]$Params = @{}
    )

    $argList = @($Command)
    foreach ($key in $Params.Keys) {
        $argList += $key
        if ($null -ne $Params[$key] -and $Params[$key] -ne '') {
            $argList += $Params[$key]
        }
    }

    try {
        $output = & winget $argList 2>&1 | Out-String
        return @{ExitCode = $LASTEXITCODE; Output = $output}
    } catch {
        return @{ExitCode = 9999; Output = $_.Exception.Message}
    }
}

function Test-AppExists {
    <#
    .SYNOPSIS
        Verifica se un'applicazione esiste nello store Winget.
    .PARAMETER Id
        L'ID Winget dell'applicazione da verificare.
    .OUTPUTS
        Boolean. True se l'app esiste, False altrimenti.
    #>
    param(
        [Parameter(Mandatory=$true)]
        [string]$Id
    )

    $result = Invoke-Winget -Command "show" -Params @{
        "--id" = $Id
        "--source" = "winget"
        "--exact" = $null
        "--accept-source-agreements" = $null
    }
    return ($result.ExitCode -eq 0)
}

function Get-WingetErrorMessage {
    <#
    .SYNOPSIS
        Converte un codice di uscita winget in un messaggio leggibile.
    .PARAMETER ExitCode
        Il codice di uscita restituito da winget.
    .OUTPUTS
        String. Messaggio di errore localizzato.
    #>
    param(
        [Parameter(Mandatory=$true)]
        [int]$ExitCode
    )

    switch ($ExitCode) {
        0            { return "OK" }
        -1978335231  { return "Errore interno" }
        -1978335230  { return "Argomenti non validi" }
        -1978335229  { return "Comando fallito" }
        -1978335228  { return "Apertura manifest fallita" }
        -1978335227  { return "Annullato" }
        -1978335226  { return "ShellExecute fallito" }
        -1978335225  { return "Versione manifest non supportata" }
        -1978335224  { return "Download fallito" }
        -1978335222  { return "Indice corrotto" }
        -1978335221  { return "Origini non valide" }
        -1978335220  { return "Nome origine gia' esistente" }
        -1978335219  { return "Tipo origine non valido" }
        -1978335217  { return "Dati origine mancanti" }
        -1978335216  { return "Nessun installer applicabile" }
        -1978335215  { return "Hash non corrisponde" }
        -1978335214  { return "Nome origine non esiste" }
        -1978335212  { return "App non trovata" }
        -1978335211  { return "Nessuna origine configurata" }
        -1978335210  { return "Piu' app trovate" }
        -1978335209  { return "Manifest non trovato" }
        -1978335207  { return "Richiesti privilegi admin" }
        -1978335205  { return "MS Store bloccato da policy" }
        -1978335204  { return "App MS Store bloccata da policy" }
        -1978335203  { return "Funzione sperimentale disabilitata" }
        -1978335202  { return "Installazione MS Store fallita" }
        -1978335191  { return "Validazione manifest fallita" }
        -1978335190  { return "Manifest non valido" }
        -1978335189  { return "Nessun aggiornamento" }
        -1978335188  { return "Upgrade --all con errori" }
        -1978335187  { return "Controllo sicurezza fallito" }
        -1978335186  { return "Dimensione download errata" }
        -1978335185  { return "Info disinstallazione mancanti" }
        -1978335184  { return "Disinstallazione fallita" }
        -1978335180  { return "Import installazione fallito" }
        -1978335179  { return "Non tutti i pacchetti trovati" }
        -1978335174  { return "Bloccato da policy" }
        -1978335173  { return "Errore REST API" }
        -1978335163  { return "Apertura origine fallita" }
        -1978335157  { return "Apertura origini fallita" }
        -1978335153  { return "Versione upgrade non piu' recente" }
        -1978335150  { return "Installazione portable fallita" }
        -1978335147  { return "Portable gia' esistente" }
        -1978335146  { return "Installer proibisce elevazione" }
        -1978335145  { return "Disinstallazione portable fallita" }
        -1978335141  { return "Nested installer non trovato" }
        -1978335140  { return "Estrazione archivio fallita" }
        -1978335137  { return "Percorso installazione richiesto" }
        -1978335136  { return "Scansione malware fallita" }
        -1978335135  { return "Gia' installata" }
        -1978335131  { return "Una o piu' installazioni fallite" }
        -1978335130  { return "Una o piu' disinstallazioni fallite" }
        -1978335128  { return "Bloccato da pin" }
        -1978335127  { return "Pacchetto stub" }
        -1978335125  { return "Download dipendenze fallito" }
        -1978335123  { return "Servizio non disponibile" }
        -1978335115  { return "Autenticazione fallita" }
        -1978335111  { return "Info riparazione mancanti" }
        -1978335109  { return "Riparazione fallita" }
        -1978335108  { return "Riparazione non supportata" }
        -1978335098  { return "Installer zero byte" }
        -1978334975  { return "App in uso" }
        -1978334974  { return "Installazione in corso" }
        -1978334973  { return "File in uso" }
        -1978334972  { return "Dipendenza mancante" }
        -1978334971  { return "Disco pieno" }
        -1978334970  { return "Memoria insufficiente" }
        -1978334969  { return "Rete richiesta" }
        -1978334968  { return "Contattare supporto" }
        -1978334967  { return "Riavvio per completare" }
        -1978334966  { return "Riavvio per installare" }
        -1978334965  { return "Riavvio avviato" }
        -1978334964  { return "Annullato dall'utente" }
        -1978334963  { return "Altra versione installata" }
        -1978334962  { return "Versione superiore presente" }
        -1978334961  { return "Bloccato da policy" }
        -1978334960  { return "Dipendenze fallite" }
        -1978334959  { return "App usata da altra applicazione" }
        -1978334958  { return "Parametro non valido" }
        -1978334957  { return "Sistema non supportato" }
        -1978334956  { return "Upgrade non supportato" }
        -1978334955  { return "Errore installer personalizzato" }
        -2145844844  { return "Errore installer" }
        9999         { return "Errore esecuzione" }
        default      { return "Errore ($ExitCode)" }
    }
}

function ConvertFrom-WingetSearchOutput {
    <#
    .SYNOPSIS
        Converte l'output testuale di winget search in oggetti strutturati.
    .PARAMETER Output
        L'output testuale del comando winget search.
    .OUTPUTS
        Array di oggetti con proprieta' Name, Id, Version.
    #>
    param(
        [Parameter(Mandatory=$true)]
        [string]$Output
    )

    $results = @()
    $lines = $Output -split "`r?`n"
    $headerFound = $false
    $nameStart = 0
    $idStart = 0
    $versionStart = 0

    foreach ($line in $lines) {
        if (-not $headerFound) {
            if ($line -match '^Nome\s+ID\s+Versione' -or $line -match '^Name\s+Id\s+Version') {
                $nameStart = 0
                $idStart = $line.IndexOf('ID')
                if ($idStart -lt 0) { $idStart = $line.IndexOf('Id') }
                $versionStart = $line.IndexOf('Versione')
                if ($versionStart -lt 0) { $versionStart = $line.IndexOf('Version') }
                if ($idStart -lt 0 -or $versionStart -lt 0) { continue }
                $headerFound = $true
            }
            continue
        }
        if ($line -match '^-+$' -or [string]::IsNullOrWhiteSpace($line)) { continue }
        if ($line.Length -lt $versionStart) { continue }
        if ($idStart -le $nameStart) { continue }

        $name = $line.Substring($nameStart, [Math]::Min($idStart - $nameStart, $line.Length)).Trim()
        $idEnd = if ($versionStart -gt $idStart) { $versionStart - $idStart } else { $line.Length - $idStart }
        $id = $line.Substring($idStart, [Math]::Min($idEnd, $line.Length - $idStart)).Trim()
        $version = if ($line.Length -gt $versionStart) { $line.Substring($versionStart).Trim().Split()[0] } else { "" }

        if (-not [string]::IsNullOrWhiteSpace($id)) {
            $results += [pscustomobject]@{Name = $name; Id = $id; Version = $version}
        }
    }
    return , @($results)
}

function Search-WingetApp {
    <#
    .SYNOPSIS
        Cerca applicazioni nello store Winget.
    .PARAMETER Query
        Il termine di ricerca.
    .OUTPUTS
        Array di risultati di ricerca.
    #>
    param(
        [Parameter(Mandatory=$true)]
        [string]$Query
    )

    $result = Invoke-Winget -Command "search" -Params @{
        "--query" = $Query
        "--source" = "winget"
        "--accept-source-agreements" = $null
    }

    return ConvertFrom-WingetSearchOutput $result.Output
}

function Install-WingetApp {
    <#
    .SYNOPSIS
        Installa un'applicazione tramite winget.
    .PARAMETER Id
        L'ID Winget dell'applicazione.
    .OUTPUTS
        Hashtable con ExitCode e Output.
    #>
    param(
        [Parameter(Mandatory=$true)]
        [string]$Id
    )

    return Invoke-Winget -Command "install" -Params @{
        "--id" = $Id
        "--exact" = $null
        "--accept-package-agreements" = $null
        "--accept-source-agreements" = $null
        "--disable-interactivity" = $null
    }
}

function Update-WingetApp {
    <#
    .SYNOPSIS
        Aggiorna un'applicazione tramite winget.
    .PARAMETER Id
        L'ID Winget dell'applicazione.
    .OUTPUTS
        Hashtable con ExitCode e Output.
    #>
    param(
        [Parameter(Mandatory=$true)]
        [string]$Id
    )

    return Invoke-Winget -Command "upgrade" -Params @{
        "--id" = $Id
        "--exact" = $null
        "--accept-package-agreements" = $null
        "--accept-source-agreements" = $null
        "--disable-interactivity" = $null
    }
}

function Uninstall-WingetApp {
    <#
    .SYNOPSIS
        Disinstalla un'applicazione tramite winget.
    .PARAMETER Id
        L'ID Winget dell'applicazione.
    .OUTPUTS
        Hashtable con ExitCode e Output.
    #>
    param(
        [Parameter(Mandatory=$true)]
        [string]$Id
    )

    return Invoke-Winget -Command "uninstall" -Params @{
        "--id" = $Id
        "--exact" = $null
        "--accept-source-agreements" = $null
        "--disable-interactivity" = $null
    }
}

# Codici di uscita che indicano "nessun aggiornamento necessario"
$script:NoUpgradeNeededCodes = @(
    -1978335189,  # Nessun aggiornamento
    -1978335135,  # Gia' installata
    -1978334963,  # Altra versione installata
    -1978334962   # Versione superiore presente
)

# Codici di uscita che indicano "gia' installata"
$script:AlreadyInstalledCodes = @(
    -1978335135,  # Gia' installata
    -1978334963   # Altra versione installata
)

function Test-NoUpgradeNeeded {
    <#
    .SYNOPSIS
        Verifica se il codice di uscita indica che non serve aggiornamento.
    #>
    param([int]$ExitCode)
    return $script:NoUpgradeNeededCodes -contains $ExitCode
}

function Test-AlreadyInstalled {
    <#
    .SYNOPSIS
        Verifica se il codice di uscita indica che l'app e' gia' installata.
    #>
    param([int]$ExitCode)
    return $script:AlreadyInstalledCodes -contains $ExitCode
}

# Esporta le funzioni
Export-ModuleMember -Function @(
    'Test-WingetAvailable',
    'Invoke-Winget',
    'Test-AppExists',
    'Get-WingetErrorMessage',
    'ConvertFrom-WingetSearchOutput',
    'Search-WingetApp',
    'Install-WingetApp',
    'Update-WingetApp',
    'Uninstall-WingetApp',
    'Test-NoUpgradeNeeded',
    'Test-AlreadyInstalled'
)
