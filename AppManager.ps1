if ([System.Threading.Thread]::CurrentThread.ApartmentState -ne 'STA') {
  $pwsh = (Get-Process -Id $PID).Path
  $psArgs = @('-NoProfile','-STA','-ExecutionPolicy','Bypass','-File',"`"$($MyInvocation.MyCommand.Path)`"")
  Start-Process -FilePath $pwsh -ArgumentList $psArgs
  exit
}

Add-Type -AssemblyName PresentationFramework,System.Windows.Forms,Microsoft.VisualBasic

$L = @{
  Add='Aggiungi'; Edit='Modifica'; Remove='Rimuovi'; Search='Cerca App'; Apply='Installa/Aggiorna'; Save='Salva'; Title='Gestione Applicazioni'; Name='Nome'; Id='ID Winget'; Action='Azione'; Status='Stato'
  Install='Installa'; Uninstall='Disinstalla'; Pause='Pausa'
  InputNameTitle='Aggiungi App'; InputNamePrompt='Inserisci il nome dell''app:'
  InputIdTitle='ID Winget'; InputIdPrompt='Inserisci ID Winget (es. Microsoft.VisualStudioCode):'
  InvalidIdTitle='Errore ID'; InvalidIdText="ID '{0}' non trovato nello store Winget. Riprova."
  DuplicateIdTitle='ID duplicato'; DuplicateIdText="L'ID '{0}' è già presente in lista."
  SearchTitle='Cerca App'; SearchPrompt='Termine di ricerca:'
  UpdatesTitle='Aggiornamenti disponibili'; Updates='Aggiornamenti'; RefreshUpdates='Aggiorna elenco'; ApplyUpdates='Applica aggiornamenti'
  SaveSuccessTitle='Salvato'; SaveSuccessText='Lista salvata correttamente.'
  Tab='Scheda'; NewTab='Nuova'; RenameTab='Rinomina'; DeleteTab='Rimuovi'
  TabNameTitle='Nuova scheda'; TabNamePrompt='Nome della nuova scheda:'; TabRenameTitle='Rinomina scheda'; TabRenamePrompt='Nuovo nome per la scheda:'
  TabExistsTitle='Nome scheda esistente'; TabExistsText='Esiste già una scheda con questo nome.'
  NoTabToDeleteTitle='Eliminazione scheda'; NoTabToDeleteText='Non puoi eliminare l''unica scheda esistente.'
  RunningText='Operazione in corso...'
  WingetNotFoundTitle='Winget non trovato'; WingetNotFoundText='winget non risulta disponibile. Installa/aggiorna "App Installer" dal Microsoft Store.'
}

if ($PSScriptRoot) { $BasePath = $PSScriptRoot }
elseif ($MyInvocation.MyCommand.Path) { $BasePath = Split-Path -Parent $MyInvocation.MyCommand.Path }
else { $BasePath = (Get-Location).Path }

$jsonPath = Join-Path $BasePath "AppsList.json"

function Test-WingetAvailable {
  try {
    $null = & winget --version 2>&1
    return $LASTEXITCODE -eq 0
  } catch {
    return $false
  }
}

if (-not (Test-WingetAvailable)) {
  [System.Windows.MessageBox]::Show($L.WingetNotFoundText,$L.WingetNotFoundTitle,[System.Windows.MessageBoxButton]::OK,[System.Windows.MessageBoxImage]::Error) | Out-Null
  exit
}

function Invoke-Winget {
  param([string]$Command, [hashtable]$Params = @{})
  
  $argList = @($Command)
  foreach ($key in $Params.Keys) {
    $argList += $key
    if ($null -ne $Params[$key] -and $Params[$key] -ne '') {
      $argList += $Params[$key]
    }
  }
  
  try {
    $output = & winget $argList 2>&1 | Out-String
    return @{ExitCode=$LASTEXITCODE; Output=$output}
  } catch {
    return @{ExitCode=9999; Output=$_.Exception.Message}
  }
}

function Test-AppExists {
  param([string]$Id)
  $result = Invoke-Winget -Command "show" -Params @{
    "--id" = $Id
    "--source" = "winget"
    "--exact" = $null
    "--accept-source-agreements" = $null
  }
  return ($result.ExitCode -eq 0)
}

function Get-WingetErrorMessage {
  param([int]$exitCode)
  switch ($exitCode) {
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
    -1978335220  { return "Nome origine già esistente" }
    -1978335219  { return "Tipo origine non valido" }
    -1978335217  { return "Dati origine mancanti" }
    -1978335216  { return "Nessun installer applicabile" }
    -1978335215  { return "Hash non corrisponde" }
    -1978335214  { return "Nome origine non esiste" }
    -1978335212  { return "App non trovata" }
    -1978335211  { return "Nessuna origine configurata" }
    -1978335210  { return "Più app trovate" }
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
    -1978335153  { return "Versione upgrade non più recente" }
    -1978335150  { return "Installazione portable fallita" }
    -1978335147  { return "Portable già esistente" }
    -1978335146  { return "Installer proibisce elevazione" }
    -1978335145  { return "Disinstallazione portable fallita" }
    -1978335141  { return "Nested installer non trovato" }
    -1978335140  { return "Estrazione archivio fallita" }
    -1978335137  { return "Percorso installazione richiesto" }
    -1978335136  { return "Scansione malware fallita" }
    -1978335135  { return "Già installata" }
    -1978335131  { return "Una o più installazioni fallite" }
    -1978335130  { return "Una o più disinstallazioni fallite" }
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
    default      { return "Errore ($exitCode)" }
  }
}

function ConvertFrom-WingetSearchOutput {
  param([string]$output)
  $results = @()
  $lines = $output -split "`r?`n"
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
      $results += [pscustomobject]@{Name=$name;Id=$id;Version=$version}
    }
  }
  return ,@($results)
}

$xaml = @"
<Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="$($L.Title)" Height="680" Width="980" WindowStartupLocation="CenterScreen"
        Background="#F5F7FB">
  <Window.Resources>
    <SolidColorBrush x:Key="PrimaryBrush" Color="#2563EB"/>
    <SolidColorBrush x:Key="PrimaryHoverBrush" Color="#1D4ED8"/>
    <SolidColorBrush x:Key="PrimaryPressedBrush" Color="#1E40AF"/>
    <SolidColorBrush x:Key="AccentBrush" Color="#10B981"/>
    <SolidColorBrush x:Key="PanelBrush" Color="#FFFFFF"/>
    <SolidColorBrush x:Key="BorderBrush" Color="#D6D8DE"/>

    <Style TargetType="Button">
      <Setter Property="Background" Value="{StaticResource PrimaryBrush}"/>
      <Setter Property="Foreground" Value="White"/>
      <Setter Property="BorderBrush" Value="{StaticResource PrimaryBrush}"/>
      <Setter Property="BorderThickness" Value="1"/>
      <Setter Property="Padding" Value="12,6"/>
      <Setter Property="FontWeight" Value="SemiBold"/>
      <Setter Property="Cursor" Value="Hand"/>
      <Setter Property="Template">
        <Setter.Value>
          <ControlTemplate TargetType="Button">
            <Border Background="{TemplateBinding Background}"
                    BorderBrush="{TemplateBinding BorderBrush}"
                    BorderThickness="{TemplateBinding BorderThickness}"
                    CornerRadius="6">
              <ContentPresenter HorizontalAlignment="Center"
                                VerticalAlignment="Center"
                                Margin="{TemplateBinding Padding}"/>
            </Border>
            <ControlTemplate.Triggers>
              <Trigger Property="IsMouseOver" Value="True">
                <Setter Property="Background" Value="{StaticResource PrimaryHoverBrush}"/>
              </Trigger>
              <Trigger Property="IsPressed" Value="True">
                <Setter Property="Background" Value="{StaticResource PrimaryPressedBrush}"/>
              </Trigger>
              <Trigger Property="IsEnabled" Value="False">
                <Setter Property="Background" Value="#A7B3D6"/>
                <Setter Property="BorderBrush" Value="#A7B3D6"/>
              </Trigger>
            </ControlTemplate.Triggers>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>

    <Style TargetType="TextBox">
      <Setter Property="BorderBrush" Value="{StaticResource BorderBrush}"/>
      <Setter Property="BorderThickness" Value="1"/>
      <Setter Property="Padding" Value="8,4"/>
    </Style>

    <Style TargetType="ComboBox">
      <Setter Property="BorderBrush" Value="{StaticResource BorderBrush}"/>
      <Setter Property="BorderThickness" Value="1"/>
      <Setter Property="Padding" Value="6,2"/>
    </Style>

    <Style TargetType="ListView">
      <Setter Property="BorderBrush" Value="{StaticResource BorderBrush}"/>
      <Setter Property="BorderThickness" Value="1"/>
      <Setter Property="Background" Value="{StaticResource PanelBrush}"/>
    </Style>
  </Window.Resources>
  <Grid Margin="10">
    <Grid.RowDefinitions>
      <RowDefinition Height="Auto"/>
      <RowDefinition Height="Auto"/>
      <RowDefinition Height="*"/>
      <RowDefinition Height="Auto"/>
      <RowDefinition Height="Auto"/>
    </Grid.RowDefinitions>

    <Grid Grid.Row="0" Margin="0,0,0,10">
      <Grid.ColumnDefinitions>
        <ColumnDefinition Width="Auto"/>
        <ColumnDefinition Width="240"/>
        <ColumnDefinition Width="10"/>
        <ColumnDefinition Width="*"/>
      </Grid.ColumnDefinitions>

      <Label Grid.Column="0" Content="$($L.Tab)" VerticalAlignment="Center" Margin="0,0,8,0"/>
      <ComboBox Grid.Column="1" Name="CmbTabs" Height="28"/>

      <UniformGrid Grid.Column="3" Rows="1" Columns="3" HorizontalAlignment="Right">
        <Button Name="BtnNewTab" Content="$($L.NewTab)" MinWidth="120" Height="28" Margin="0,0,8,0"/>
        <Button Name="BtnRenameTab" Content="$($L.RenameTab)" MinWidth="120" Height="28" Margin="0,0,8,0"/>
        <Button Name="BtnDeleteTab" Content="$($L.DeleteTab)" MinWidth="120" Height="28"/>
      </UniformGrid>
    </Grid>

    <UniformGrid Grid.Row="1" Rows="1" Columns="6" Margin="0,0,0,10">
      <Button Name="BtnAdd"    Content="$($L.Add)"    Height="32" Margin="0,0,8,0"/>
      <Button Name="BtnEdit"   Content="$($L.Edit)"   Height="32" Margin="0,0,8,0"/>
      <Button Name="BtnRemove" Content="$($L.Remove)" Height="32" Margin="0,0,8,0"/>
      <Button Name="BtnSearch" Content="$($L.Search)" Height="32" Margin="0,0,8,0"/>
      <Button Name="BtnUpdates" Content="$($L.Updates)" Height="32" Margin="0,0,8,0"/>
      <Button Name="BtnApply"  Content="$($L.Apply)"  Height="32" Background="{StaticResource AccentBrush}" BorderBrush="{StaticResource AccentBrush}"/>
    </UniformGrid>

    <Grid Grid.Row="2">
      <ListView Name="LvApps" SelectionMode="Single">
        <ListView.View>
          <GridView>
            <GridViewColumn Header="$($L.Name)" DisplayMemberBinding="{Binding Name}" Width="270"/>
            <GridViewColumn Header="$($L.Id)"   DisplayMemberBinding="{Binding Id}"   Width="330"/>
            <GridViewColumn Header="$($L.Action)" Width="200">
              <GridViewColumn.CellTemplate>
                <DataTemplate>
                  <ComboBox SelectedValue="{Binding Action, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"
                            SelectedValuePath="Tag"
                            Width="180" Height="26">
                    <ComboBoxItem Content="$($L.Install)"   Tag="Install"/>
                    <ComboBoxItem Content="$($L.Uninstall)" Tag="Uninstall"/>
                    <ComboBoxItem Content="$($L.Pause)"     Tag="Pause"/>
                  </ComboBox>
                </DataTemplate>
              </GridViewColumn.CellTemplate>
            </GridViewColumn>
            <GridViewColumn Header="$($L.Status)" DisplayMemberBinding="{Binding Status}" Width="140"/>
          </GridView>
        </ListView.View>
      </ListView>

      <Border Name="SearchOverlay" Background="#CC000000" Visibility="Collapsed">
        <Grid Margin="40">
          <Border Background="White" CornerRadius="6" Padding="12">
            <Grid>
              <Grid.RowDefinitions>
                <RowDefinition Height="Auto"/>
                <RowDefinition Height="Auto"/>
                <RowDefinition Height="*"/>
                <RowDefinition Height="Auto"/>
              </Grid.RowDefinitions>

              <TextBlock Grid.Row="0" FontWeight="Bold" FontSize="14" Text="$($L.SearchTitle)" Margin="0,0,0,10"/>

              <Grid Grid.Row="1" Margin="0,0,0,10">
                <Grid.ColumnDefinitions>
                  <ColumnDefinition Width="*"/>
                  <ColumnDefinition Width="10"/>
                  <ColumnDefinition Width="Auto"/>
                  <ColumnDefinition Width="10"/>
                  <ColumnDefinition Width="Auto"/>
                </Grid.ColumnDefinitions>
                <TextBox Grid.Column="0" Name="TxtSearchQuery" Height="28"/>
                <Button Grid.Column="2" Name="BtnRunSearch" Content="$($L.Search)" Height="28" MinWidth="120"/>
                <Button Grid.Column="4" Name="BtnCloseSearch" Content="Chiudi" Height="28" MinWidth="120"/>
              </Grid>

              <ListView Grid.Row="2" Name="LvSearchResults" SelectionMode="Single">
                <ListView.View>
                  <GridView>
                    <GridViewColumn Header="Nome" DisplayMemberBinding="{Binding Name}" Width="280"/>
                    <GridViewColumn Header="ID" DisplayMemberBinding="{Binding Id}" Width="300"/>
                    <GridViewColumn Header="Versione" DisplayMemberBinding="{Binding Version}" Width="100"/>
                  </GridView>
                </ListView.View>
              </ListView>

              <Grid Grid.Row="3" Margin="0,10,0,0">
                <Grid.ColumnDefinitions>
                  <ColumnDefinition Width="*"/>
                  <ColumnDefinition Width="10"/>
                  <ColumnDefinition Width="Auto"/>
                </Grid.ColumnDefinitions>
                <TextBox Grid.Column="0" Name="TxtSearchPickId" Height="28" VerticalContentAlignment="Center" />
                <Button Grid.Column="2" Name="BtnUseSearchId" Content="Usa questo ID" Height="28" MinWidth="140"/>
              </Grid>
            </Grid>
          </Border>
        </Grid>
      </Border>

      <Border Name="UpdatesOverlay" Background="#CC000000" Visibility="Collapsed">
        <Grid Margin="40">
          <Border Background="White" CornerRadius="6" Padding="12">
            <Grid>
              <Grid.RowDefinitions>
                <RowDefinition Height="Auto"/>
                <RowDefinition Height="*"/>
                <RowDefinition Height="Auto"/>
              </Grid.RowDefinitions>

              <TextBlock Grid.Row="0" FontWeight="Bold" FontSize="14" Text="$($L.UpdatesTitle)" Margin="0,0,0,10"/>

              <ListView Grid.Row="1" Name="LvUpdates" SelectionMode="Single">
                <ListView.View>
                  <GridView>
                    <GridViewColumn Header="" Width="40">
                      <GridViewColumn.CellTemplate>
                        <DataTemplate>
                          <CheckBox IsChecked="{Binding Selected, Mode=TwoWay}"/>
                        </DataTemplate>
                      </GridViewColumn.CellTemplate>
                    </GridViewColumn>
                    <GridViewColumn Header="$($L.Name)" DisplayMemberBinding="{Binding Name}" Width="260"/>
                    <GridViewColumn Header="ID" DisplayMemberBinding="{Binding Id}" Width="260"/>
                    <GridViewColumn Header="Versione" DisplayMemberBinding="{Binding Version}" Width="110"/>
                    <GridViewColumn Header="Disponibile" DisplayMemberBinding="{Binding Available}" Width="110"/>
                  </GridView>
                </ListView.View>
              </ListView>

              <Grid Grid.Row="2" Margin="0,10,0,0">
                <Grid.ColumnDefinitions>
                  <ColumnDefinition Width="*"/>
                  <ColumnDefinition Width="10"/>
                  <ColumnDefinition Width="Auto"/>
                  <ColumnDefinition Width="10"/>
                  <ColumnDefinition Width="Auto"/>
                  <ColumnDefinition Width="10"/>
                  <ColumnDefinition Width="Auto"/>
                </Grid.ColumnDefinitions>
                <Button Grid.Column="2" Name="BtnRefreshUpdates" Content="$($L.RefreshUpdates)" Height="28" MinWidth="140"/>
                <Button Grid.Column="4" Name="BtnApplyUpdates" Content="$($L.ApplyUpdates)" Height="28" MinWidth="160" Background="{StaticResource AccentBrush}" BorderBrush="{StaticResource AccentBrush}"/>
                <Button Grid.Column="6" Name="BtnCloseUpdates" Content="Chiudi" Height="28" MinWidth="120"/>
              </Grid>
            </Grid>
          </Border>
        </Grid>
      </Border>
    </Grid>

    <TextBox Name="TxtOutput" Grid.Row="3" Height="150" Margin="0,10,0,0"
             TextWrapping="Wrap" VerticalScrollBarVisibility="Auto" HorizontalScrollBarVisibility="Auto"
             IsReadOnly="True" FontFamily="Consolas" FontSize="12"/>

    <Grid Grid.Row="4" Margin="0,10,0,0">
      <Grid.ColumnDefinitions>
        <ColumnDefinition Width="*"/>
        <ColumnDefinition Width="Auto"/>
      </Grid.ColumnDefinitions>
      <TextBlock Grid.Column="0" VerticalAlignment="Center" Foreground="Gray" Name="TxtStatus"/>
      <Button Grid.Column="1" Name="BtnSave" Content="$($L.Save)" Width="160" Height="32" Background="{StaticResource AccentBrush}" BorderBrush="{StaticResource AccentBrush}"/>
    </Grid>
  </Grid>
</Window>
"@

$reader = New-Object System.Xml.XmlNodeReader([xml]$xaml)
$window = [Windows.Markup.XamlReader]::Load($reader)

$lv             = $window.FindName("LvApps")
$btnAdd         = $window.FindName("BtnAdd")
$btnEdit        = $window.FindName("BtnEdit")
$btnRemove      = $window.FindName("BtnRemove")
$btnSearch      = $window.FindName("BtnSearch")
$btnUpdates     = $window.FindName("BtnUpdates")
$btnApply       = $window.FindName("BtnApply")
$btnSave        = $window.FindName("BtnSave")
$cmbTabs        = $window.FindName("CmbTabs")
$btnNewTab      = $window.FindName("BtnNewTab")
$btnRenameTab   = $window.FindName("BtnRenameTab")
$btnDeleteTab   = $window.FindName("BtnDeleteTab")
$txtOutput      = $window.FindName("TxtOutput")
$txtStatus      = $window.FindName("TxtStatus")
$searchOverlay  = $window.FindName("SearchOverlay")
$updatesOverlay = $window.FindName("UpdatesOverlay")
$txtSearchQuery = $window.FindName("TxtSearchQuery")
$btnRunSearch   = $window.FindName("BtnRunSearch")
$btnCloseSearch = $window.FindName("BtnCloseSearch")
$lvSearchResults = $window.FindName("LvSearchResults")
$txtSearchPickId = $window.FindName("TxtSearchPickId")
$btnUseSearchId = $window.FindName("BtnUseSearchId")
$lvUpdates = $window.FindName("LvUpdates")
$btnRefreshUpdates = $window.FindName("BtnRefreshUpdates")
$btnApplyUpdates = $window.FindName("BtnApplyUpdates")
$btnCloseUpdates = $window.FindName("BtnCloseUpdates")

$script:Tabs = @{}
$script:TabNames = New-Object System.Collections.ArrayList
$script:CurrentTabName = $null
$script:AppsList = $null
$script:SearchTimer = $null
$script:ApplyTimer = $null
$script:UpdateResults = @()
$script:UpdateTimer = $null
$script:UpdateSnapshot = @()
$script:UpdateIndex = 0
$script:UpdateJob = $null

function Write-UiOutput {
  param([string]$text)
  if ($txtOutput.Text.Length -gt 0) { $txtOutput.AppendText([Environment]::NewLine) }
  $txtOutput.AppendText($text)
  $txtOutput.ScrollToEnd()
}

function Normalize-LogOutput {
  param([string]$text)
  if ([string]::IsNullOrWhiteSpace($text)) { return "" }
  $lines = $text -split "`r?`n"
  $filtered = foreach ($line in $lines) {
    $trimmed = $line.Trim()
    if ([string]::IsNullOrWhiteSpace($trimmed)) { continue }
    if ($trimmed -match '^[\\/\-\|]+$') { continue }
    $trimmed
  }
  return ($filtered -join [Environment]::NewLine)
}

function Write-UiOutputNormalized {
  param([string]$text)
  $normalized = Normalize-LogOutput $text
  if ([string]::IsNullOrWhiteSpace($normalized)) { return }
  Write-UiOutput $normalized
}

function Update-List {
  $lv.SelectedItem = $null
  $lv.ItemsSource = $null
  $lv.ItemsSource = $script:AppsList
}

function Set-CurrentTab {
  param([string]$name)
  if (-not $script:Tabs.ContainsKey($name)) { return }
  $script:CurrentTabName = $name
  $script:AppsList = $script:Tabs[$name]
  Update-List
}

function Get-NormalizedAction {
  param([string]$action)
  if ([string]::IsNullOrWhiteSpace($action)) { return "Install" }
  switch ($action) {
    "Install" { return "Install" }
    "Uninstall" { return "Uninstall" }
    "Pause" { return "Pause" }
    default { return "Install" }
  }
}

function ConvertFrom-WingetUpgradeOutput {
  param([string]$output)
  $results = @()
  $lines = $output -split "`r?`n"
  $headerFound = $false
  $nameStart = 0
  $idStart = 0
  $versionStart = 0
  $availableStart = 0

  foreach ($line in $lines) {
    if (-not $headerFound) {
      if ($line -match '^Nome\s+ID\s+Versione\s+Disponibile' -or $line -match '^Name\s+Id\s+Version\s+Available') {
        $nameStart = 0
        $idStart = $line.IndexOf('ID')
        if ($idStart -lt 0) { $idStart = $line.IndexOf('Id') }
        $versionStart = $line.IndexOf('Versione')
        if ($versionStart -lt 0) { $versionStart = $line.IndexOf('Version') }
        $availableStart = $line.IndexOf('Disponibile')
        if ($availableStart -lt 0) { $availableStart = $line.IndexOf('Available') }
        if ($idStart -lt 0 -or $versionStart -lt 0 -or $availableStart -lt 0) { continue }
        $headerFound = $true
      }
      continue
    }
    if ($line -match '^-+$' -or [string]::IsNullOrWhiteSpace($line)) { continue }
    if ($line.Length -lt $availableStart) { continue }
    if ($idStart -le $nameStart -or $versionStart -le $idStart -or $availableStart -le $versionStart) { continue }

    $name = $line.Substring($nameStart, [Math]::Min($idStart - $nameStart, $line.Length)).Trim()
    $idEnd = if ($versionStart -gt $idStart) { $versionStart - $idStart } else { $line.Length - $idStart }
    $id = $line.Substring($idStart, [Math]::Min($idEnd, $line.Length - $idStart)).Trim()
    $versionEnd = if ($availableStart -gt $versionStart) { $availableStart - $versionStart } else { $line.Length - $versionStart }
    $version = $line.Substring($versionStart, [Math]::Min($versionEnd, $line.Length - $versionStart)).Trim()
    $available = if ($line.Length -gt $availableStart) { $line.Substring($availableStart).Trim().Split()[0] } else { "" }

    if (-not [string]::IsNullOrWhiteSpace($id) -and $id -notmatch '\s') {
      $results += [pscustomobject]@{
        Name = $name
        Id = $id
        Version = $version
        Available = $available
        Selected = $true
      }
    }
  }
  return ,@($results)
}

function Load-AvailableUpdates {
  $btnRefreshUpdates.IsEnabled = $false
  $btnApplyUpdates.IsEnabled = $false
  $lvUpdates.ItemsSource = $null
  $script:UpdateResults = @()

  $result = Invoke-Winget -Command "upgrade" -Params @{
    "--source" = "winget"
    "--accept-source-agreements" = $null
  }

  $parsed = ConvertFrom-WingetUpgradeOutput $result.Output
  $script:UpdateResults = $parsed
  $lvUpdates.ItemsSource = $script:UpdateResults
  $btnRefreshUpdates.IsEnabled = $true
  $btnApplyUpdates.IsEnabled = $true
}

function Stop-UpdateTimer {
  if ($null -ne $script:UpdateTimer) {
    $script:UpdateTimer.Stop()
    $script:UpdateTimer = $null
  }
}

function Start-UpdatesFlow {
  $script:UpdateSnapshot = @($script:UpdateResults | Where-Object { $_.Selected })
  $script:UpdateIndex = 0
  $script:UpdateJob = $null

  if ($script:UpdateSnapshot.Count -eq 0) { return }

  $btnRefreshUpdates.IsEnabled = $false
  $btnApplyUpdates.IsEnabled = $false
  $btnCloseUpdates.IsEnabled = $false
  $txtStatus.Text = $L.RunningText
  Write-UiOutput ("=== Avvio aggiornamenti ({0}) ===" -f (Get-Date).ToString("yyyy-MM-dd HH:mm:ss"))

  Stop-UpdateTimer
  $script:UpdateTimer = New-Object System.Windows.Threading.DispatcherTimer
  $script:UpdateTimer.Interval = [TimeSpan]::FromMilliseconds(200)
  $script:UpdateTimer.Add_Tick({
    if ($null -ne $script:UpdateJob) {
      if ($script:UpdateJob.State -eq "Running") { return }
      $result = Receive-Job -Job $script:UpdateJob -ErrorAction SilentlyContinue
      if ($null -ne $result) {
        Write-UiOutputNormalized $result.Output
        if ($result.ExitCode -ne 0) {
          Write-UiOutput (Get-WingetErrorMessage $result.ExitCode)
        }
      }
      Remove-Job -Job $script:UpdateJob -Force
      $script:UpdateJob = $null
      return
    }

    if ($script:UpdateIndex -ge $script:UpdateSnapshot.Count) {
      Write-UiOutput ("=== Fine aggiornamenti ({0}) ===" -f (Get-Date).ToString("yyyy-MM-dd HH:mm:ss"))
      $txtStatus.Text = ""
      $btnRefreshUpdates.IsEnabled = $true
      $btnApplyUpdates.IsEnabled = $true
      $btnCloseUpdates.IsEnabled = $true
      Stop-UpdateTimer
      Load-AvailableUpdates
      return
    }

    $update = $script:UpdateSnapshot[$script:UpdateIndex]
    $script:UpdateIndex++
    Write-UiOutput ("--- {0} [{1}] : upgrade ---" -f $update.Name, $update.Id)
    $script:UpdateJob = Start-Job -ArgumentList $update.Id -ScriptBlock {
      param($Id)
      $output = & winget upgrade --id $Id --exact --accept-package-agreements --accept-source-agreements --disable-interactivity 2>&1 | Out-String
      [pscustomobject]@{ExitCode=$LASTEXITCODE; Output=$output}
    }
  })
  $script:UpdateTimer.Start()
}

if (Test-Path $jsonPath) {
  try {
    $rawText = Get-Content $jsonPath -Raw
    if (-not [string]::IsNullOrWhiteSpace($rawText)) {
      $trimmed = $rawText.TrimStart()
      if ($trimmed.StartsWith("{")) {
        $raw = $rawText | ConvertFrom-Json
        if ($raw.Tabs) {
          foreach ($tab in $raw.Tabs) {
            $list = New-Object System.Collections.ArrayList
            foreach ($app in $tab.Apps) {
              $action = Get-NormalizedAction $app.Action
              [void]$list.Add([pscustomobject]@{Name=$app.Name;Id=$app.Id;Action=$action;Status=""})
            }
            $script:Tabs[$tab.Name] = $list
            [void]$script:TabNames.Add($tab.Name)
          }
        }
      } else {
        $raw = $rawText | ConvertFrom-Json
        $list = New-Object System.Collections.ArrayList
        foreach ($app in $raw) {
          $action = Get-NormalizedAction $app.Action
          [void]$list.Add([pscustomobject]@{Name=$app.Name;Id=$app.Id;Action=$action;Status=""})
        }
        $script:Tabs['Default'] = $list
        [void]$script:TabNames.Add('Default')
      }
    }
  } catch {
  }
}

if ($script:Tabs.Count -eq 0) {
  $list = New-Object System.Collections.ArrayList
  $script:Tabs['Default'] = $list
  [void]$script:TabNames.Add('Default')
}

foreach ($name in $script:TabNames) { [void]$cmbTabs.Items.Add($name) }

if ($cmbTabs.Items.Count -gt 0) {
  $cmbTabs.SelectedIndex = 0
  $script:CurrentTabName = $cmbTabs.SelectedItem.ToString()
  $script:AppsList = $script:Tabs[$script:CurrentTabName]
}

Update-List
Write-UiOutput "winget disponibile: OK"

$cmbTabs.Add_SelectionChanged({
  if ($null -ne $cmbTabs.SelectedItem) {
    $name = $cmbTabs.SelectedItem.ToString()
    if ($script:Tabs.ContainsKey($name)) { Set-CurrentTab $name }
  }
})

$btnNewTab.Add_Click({
  $name = [Microsoft.VisualBasic.Interaction]::InputBox($L.TabNamePrompt,$L.TabNameTitle,"Scheda")
  if ([string]::IsNullOrWhiteSpace($name)) { return }
  if ($script:Tabs.ContainsKey($name)) {
    [System.Windows.MessageBox]::Show($L.TabExistsText,$L.TabExistsTitle,[System.Windows.MessageBoxButton]::OK,[System.Windows.MessageBoxImage]::Warning) | Out-Null
    return
  }
  $list = New-Object System.Collections.ArrayList
  $script:Tabs[$name] = $list
  [void]$script:TabNames.Add($name)
  $cmbTabs.Items.Clear()
  foreach ($n in $script:TabNames) { [void]$cmbTabs.Items.Add($n) }
  $cmbTabs.SelectedItem = $name
  Set-CurrentTab $name
})

$btnRenameTab.Add_Click({
  if (-not $script:CurrentTabName) { return }
  $newName = [Microsoft.VisualBasic.Interaction]::InputBox($L.TabRenamePrompt,$L.TabRenameTitle,$script:CurrentTabName)
  if ([string]::IsNullOrWhiteSpace($newName) -or $newName -eq $script:CurrentTabName) { return }
  if ($script:Tabs.ContainsKey($newName)) {
    [System.Windows.MessageBox]::Show($L.TabExistsText,$L.TabExistsTitle,[System.Windows.MessageBoxButton]::OK,[System.Windows.MessageBoxImage]::Warning) | Out-Null
    return
  }
  $tabList = $script:Tabs[$script:CurrentTabName]
  [void]$script:Tabs.Remove($script:CurrentTabName)
  $script:Tabs[$newName] = $tabList
  $index = $script:TabNames.IndexOf($script:CurrentTabName)
  if ($index -ge 0) { $script:TabNames[$index] = $newName }
  $cmbTabs.Items.Clear()
  foreach ($n in $script:TabNames) { [void]$cmbTabs.Items.Add($n) }
  $cmbTabs.SelectedItem = $newName
  Set-CurrentTab $newName
})

$btnDeleteTab.Add_Click({
  if ($script:Tabs.Count -le 1 -or -not $script:CurrentTabName) {
    [System.Windows.MessageBox]::Show($L.NoTabToDeleteText,$L.NoTabToDeleteTitle,[System.Windows.MessageBoxButton]::OK,[System.Windows.MessageBoxImage]::Information) | Out-Null
    return
  }
  $oldIndex = $cmbTabs.SelectedIndex
  [void]$script:Tabs.Remove($script:CurrentTabName)
  [void]$script:TabNames.Remove($script:CurrentTabName)
  $cmbTabs.Items.Clear()
  foreach ($n in $script:TabNames) { [void]$cmbTabs.Items.Add($n) }
  if ($script:TabNames.Count -gt 0) {
    if ($oldIndex -ge $script:TabNames.Count) { $oldIndex = $script:TabNames.Count - 1 }
    if ($oldIndex -lt 0) { $oldIndex = 0 }
    $cmbTabs.SelectedIndex = $oldIndex
    $name = $cmbTabs.SelectedItem
    if ($null -ne $name) { Set-CurrentTab $name.ToString() }
  }
})

$btnAdd.Add_Click({
  $name = [Microsoft.VisualBasic.Interaction]::InputBox($L.InputNamePrompt,$L.InputNameTitle,"")
  if ([string]::IsNullOrWhiteSpace($name)) { return }
  do {
    $id = [Microsoft.VisualBasic.Interaction]::InputBox($L.InputIdPrompt,$L.InputIdTitle,"")
    if ([string]::IsNullOrWhiteSpace($id)) { return }
    if ($script:AppsList.Id -contains $id) {
      [System.Windows.MessageBox]::Show([string]::Format($L.DuplicateIdText,$id),$L.DuplicateIdTitle,[System.Windows.MessageBoxButton]::OK,[System.Windows.MessageBoxImage]::Warning) | Out-Null
      continue
    }
    if (-not (Test-AppExists $id)) {
      [System.Windows.MessageBox]::Show([string]::Format($L.InvalidIdText,$id),$L.InvalidIdTitle,[System.Windows.MessageBoxButton]::OK,[System.Windows.MessageBoxImage]::Warning) | Out-Null
    } else {
      break
    }
  } while ($true)
  [void]$script:AppsList.Add([pscustomobject]@{Name=$name;Id=$id;Action="Install";Status=""})
  Update-List
})

$btnEdit.Add_Click({
  if ($null -eq $lv.SelectedItem) { return }
  $sel = $lv.SelectedItem
  $newName = [Microsoft.VisualBasic.Interaction]::InputBox($L.InputNamePrompt,$L.Edit,$sel.Name)
  if (-not [string]::IsNullOrWhiteSpace($newName)) { $sel.Name = $newName }
  do {
    $newId = [Microsoft.VisualBasic.Interaction]::InputBox($L.InputIdPrompt,$L.Edit,$sel.Id)
    if ([string]::IsNullOrWhiteSpace($newId)) { break }
    if ($newId -ne $sel.Id -and $script:AppsList.Id -contains $newId) {
      [System.Windows.MessageBox]::Show([string]::Format($L.DuplicateIdText,$newId),$L.DuplicateIdTitle,[System.Windows.MessageBoxButton]::OK,[System.Windows.MessageBoxImage]::Warning) | Out-Null
      continue
    }
    if (-not (Test-AppExists $newId)) {
      [System.Windows.MessageBox]::Show([string]::Format($L.InvalidIdText,$newId),$L.InvalidIdTitle,[System.Windows.MessageBoxButton]::OK,[System.Windows.MessageBoxImage]::Warning) | Out-Null
    } else {
      $sel.Id = $newId
      break
    }
  } while ($true)
  Update-List
})

$btnRemove.Add_Click({
  if ($null -ne $lv.SelectedItem) {
    [void]$script:AppsList.Remove($lv.SelectedItem)
    Update-List
  }
})

$btnSearch.Add_Click({
  $lvSearchResults.ItemsSource = $null
  $txtSearchQuery.Text = ""
  $txtSearchPickId.Text = ""
  $searchOverlay.Visibility = 'Visible'
  $txtSearchQuery.Focus() | Out-Null
})

$btnCloseSearch.Add_Click({
  $searchOverlay.Visibility = 'Collapsed'
})

$btnUpdates.Add_Click({
  $updatesOverlay.Visibility = 'Visible'
  Load-AvailableUpdates
})

$btnRefreshUpdates.Add_Click({
  Load-AvailableUpdates
})

$btnCloseUpdates.Add_Click({
  $updatesOverlay.Visibility = 'Collapsed'
})

$lvSearchResults.Add_SelectionChanged({
  if ($null -ne $lvSearchResults.SelectedItem) {
    $txtSearchPickId.Text = $lvSearchResults.SelectedItem.Id
  }
})

$btnRunSearch.Add_Click({
  $script:SearchQuery = $txtSearchQuery.Text
  if ([string]::IsNullOrWhiteSpace($script:SearchQuery)) { return }
  
  $lvSearchResults.ItemsSource = $null
  $btnRunSearch.IsEnabled = $false
  
  if ($null -ne $script:SearchTimer) {
    $script:SearchTimer.Stop()
    $script:SearchTimer = $null
  }
  
  $script:SearchTimer = New-Object System.Windows.Threading.DispatcherTimer
  $script:SearchTimer.Interval = [TimeSpan]::FromMilliseconds(100)
  $script:SearchTimer.Add_Tick({
    $result = Invoke-Winget -Command "search" -Params @{
      "--query" = $script:SearchQuery
      "--source" = "winget"
      "--accept-source-agreements" = $null
    }
    $parsed = ConvertFrom-WingetSearchOutput $result.Output
    $lvSearchResults.ItemsSource = $parsed
    $btnRunSearch.IsEnabled = $true
    if ($null -ne $script:SearchTimer) {
      $script:SearchTimer.Stop()
      $script:SearchTimer = $null
    }
  })
  $script:SearchTimer.Start()
})

$txtSearchQuery.Add_KeyDown({
  if ($_.Key -eq 'Enter') {
    $btnRunSearch.RaiseEvent([System.Windows.RoutedEventArgs]::new([System.Windows.Controls.Button]::ClickEvent))
  }
})

$btnUseSearchId.Add_Click({
  $id = $txtSearchPickId.Text
  if ([string]::IsNullOrWhiteSpace($id)) { return }
  if ($script:AppsList.Id -contains $id) {
    [System.Windows.MessageBox]::Show([string]::Format($L.DuplicateIdText,$id),$L.DuplicateIdTitle,[System.Windows.MessageBoxButton]::OK,[System.Windows.MessageBoxImage]::Warning) | Out-Null
    return
  }
  if (-not (Test-AppExists $id)) {
    [System.Windows.MessageBox]::Show([string]::Format($L.InvalidIdText,$id),$L.InvalidIdTitle,[System.Windows.MessageBoxButton]::OK,[System.Windows.MessageBoxImage]::Warning) | Out-Null
    return
  }
  $selected = $lvSearchResults.SelectedItem
  $name = if ($null -ne $selected -and -not [string]::IsNullOrWhiteSpace($selected.Name)) { $selected.Name } else { $id }
  [void]$script:AppsList.Add([pscustomobject]@{Name=$name;Id=$id;Action="Install";Status=""})
  Update-List
  $searchOverlay.Visibility = 'Collapsed'
})

$btnApplyUpdates.Add_Click({
  if ($null -eq $script:UpdateResults -or $script:UpdateResults.Count -eq 0) { return }
  Start-UpdatesFlow
})

$btnApply.Add_Click({
  if ($script:AppsList.Count -eq 0) { return }
  
  $txtOutput.Text = ""
  $txtStatus.Text = $L.RunningText
  $btnApply.IsEnabled = $false
  
  $script:appsSnapshot = @()
  foreach ($a in $script:AppsList) { 
    $script:appsSnapshot += [pscustomobject]@{Name=$a.Name;Id=$a.Id;Action=$a.Action}
  }
  
  if ($null -ne $script:ApplyTimer) {
    $script:ApplyTimer.Stop()
    $script:ApplyTimer = $null
  }
  
  $script:ApplyTimer = New-Object System.Windows.Threading.DispatcherTimer
  $script:ApplyTimer.Interval = [TimeSpan]::FromMilliseconds(100)
  $script:appIndex = 0
  
  $script:ApplyTimer.Add_Tick({
    if ($script:appIndex -ge $script:appsSnapshot.Count) {
      Write-UiOutput ("=== Fine operazioni ({0}) ===" -f (Get-Date).ToString("yyyy-MM-dd HH:mm:ss"))
      $txtStatus.Text = ""
      $btnApply.IsEnabled = $true
      if ($null -ne $script:ApplyTimer) {
        $script:ApplyTimer.Stop()
        $script:ApplyTimer = $null
      }
      return
    }
    
    $app = $script:appsSnapshot[$script:appIndex]
    $script:appIndex++
    
    if ($app.Action -eq "Pause") {
      foreach ($item in $script:AppsList) {
        if ($item.Id -eq $app.Id) { $item.Status = "Pausa"; break }
      }
      Update-List
      return
    }
    
    if ([string]::IsNullOrWhiteSpace($app.Id)) { return }
    
    if ($app.Action -eq "Install") {
      foreach ($item in $script:AppsList) {
        if ($item.Id -eq $app.Id) { $item.Status = "Upgrade..."; break }
      }
      Update-List
      Write-UiOutput ("--- {0} [{1}] : upgrade ---" -f $app.Name, $app.Id)
      
      $result1 = Invoke-Winget -Command "upgrade" -Params @{
        "--id" = $app.Id
        "--exact" = $null
        "--accept-package-agreements" = $null
        "--accept-source-agreements" = $null
        "--disable-interactivity" = $null
      }
      Write-UiOutputNormalized $result1.Output
      
      if ($result1.ExitCode -eq 0) {
        foreach ($item in $script:AppsList) {
          if ($item.Id -eq $app.Id) { $item.Status = "OK"; break }
        }
        Update-List
        return
      }
      
      $noUpgradeNeeded = @(
        -1978335189,
        -1978335135,
        -1978334963,
        -1978334962
      )
      if ($noUpgradeNeeded -contains $result1.ExitCode) {
        foreach ($item in $script:AppsList) {
          if ($item.Id -eq $app.Id) { $item.Status = "Già aggiornata"; break }
        }
        Update-List
        return
      }
      
      foreach ($item in $script:AppsList) {
        if ($item.Id -eq $app.Id) { $item.Status = "Install..."; break }
      }
      Update-List
      Write-UiOutput ("--- {0} [{1}] : install ---" -f $app.Name, $app.Id)
      
      $result2 = Invoke-Winget -Command "install" -Params @{
        "--id" = $app.Id
        "--exact" = $null
        "--accept-package-agreements" = $null
        "--accept-source-agreements" = $null
        "--disable-interactivity" = $null
      }
      Write-UiOutputNormalized $result2.Output
      
      $alreadyInstalled = @(
        -1978335135,
        -1978334963
      )
      if ($result2.ExitCode -eq 0) {
        foreach ($item in $script:AppsList) {
          if ($item.Id -eq $app.Id) { $item.Status = "OK"; break }
        }
      } elseif ($alreadyInstalled -contains $result2.ExitCode) {
        foreach ($item in $script:AppsList) {
          if ($item.Id -eq $app.Id) { $item.Status = "Già installata"; break }
        }
      } else {
        foreach ($item in $script:AppsList) {
          if ($item.Id -eq $app.Id) { $item.Status = (Get-WingetErrorMessage $result2.ExitCode); break }
        }
      }
      Update-List
      return
    }
    
    if ($app.Action -eq "Uninstall") {
      foreach ($item in $script:AppsList) {
        if ($item.Id -eq $app.Id) { $item.Status = "Disinstalla..."; break }
      }
      Update-List
      Write-UiOutput ("--- {0} [{1}] : uninstall ---" -f $app.Name, $app.Id)
      
      $result3 = Invoke-Winget -Command "uninstall" -Params @{
        "--id" = $app.Id
        "--exact" = $null
        "--accept-source-agreements" = $null
        "--disable-interactivity" = $null
      }
      Write-UiOutputNormalized $result3.Output
      
      if ($result3.ExitCode -eq 0) {
        foreach ($item in $script:AppsList) {
          if ($item.Id -eq $app.Id) { $item.Status = "OK"; break }
        }
      } else {
        foreach ($item in $script:AppsList) {
          if ($item.Id -eq $app.Id) { $item.Status = (Get-WingetErrorMessage $result3.ExitCode); break }
        }
      }
      Update-List
      return
    }
  })
  
  Write-UiOutput ("=== Avvio operazioni ({0}) ===" -f (Get-Date).ToString("yyyy-MM-dd HH:mm:ss"))
  $script:ApplyTimer.Start()
})

$btnSave.Add_Click({
  $tabsForJson = @()
  foreach ($tabName in $script:TabNames) {
    $appsForJson = @()
    foreach ($app in $script:Tabs[$tabName]) {
      $appsForJson += [pscustomobject]@{Name=$app.Name;Id=$app.Id;Action=$app.Action}
    }
    $tabsForJson += [pscustomobject]@{Name=$tabName;Apps=$appsForJson}
  }
  $root = [pscustomobject]@{Tabs=$tabsForJson}
  $root | ConvertTo-Json -Depth 5 | Set-Content -Path $jsonPath -Encoding UTF8
  [System.Windows.MessageBox]::Show($L.SaveSuccessText,$L.SaveSuccessTitle,[System.Windows.MessageBoxButton]::OK,[System.Windows.MessageBoxImage]::Information) | Out-Null
})

[void]$window.ShowDialog()
