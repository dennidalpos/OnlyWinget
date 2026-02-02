
function Get-MainWindowXaml {
    
    param(
        [Parameter(Mandatory=$true)]
        [hashtable]$Strings
    )

    $L = $Strings

    return @"
<Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="$($L.Title)" Height="680" Width="980" WindowStartupLocation="CenterScreen">
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

    <UniformGrid Grid.Row="1" Rows="1" Columns="5" Margin="0,0,0,10">
      <Button Name="BtnAdd"    Content="$($L.Add)"    Height="32" Margin="0,0,8,0"/>
      <Button Name="BtnEdit"   Content="$($L.Edit)"   Height="32" Margin="0,0,8,0"/>
      <Button Name="BtnRemove" Content="$($L.Remove)" Height="32" Margin="0,0,8,0"/>
      <Button Name="BtnSearch" Content="$($L.Search)" Height="32" Margin="0,0,8,0"/>
      <Button Name="BtnApply"  Content="$($L.Apply)"  Height="32"/>
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
                <Button Grid.Column="4" Name="BtnCloseSearch" Content="$($L.CloseButton)" Height="28" MinWidth="120"/>
              </Grid>

              <ListView Grid.Row="2" Name="LvSearchResults" SelectionMode="Single">
                <ListView.View>
                  <GridView>
                    <GridViewColumn Header="$($L.Name)" DisplayMemberBinding="{Binding Name}" Width="280"/>
                    <GridViewColumn Header="ID" DisplayMemberBinding="{Binding Id}" Width="300"/>
                    <GridViewColumn Header="$($L.VersionHeader)" DisplayMemberBinding="{Binding Version}" Width="100"/>
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
                <Button Grid.Column="2" Name="BtnUseSearchId" Content="$($L.UseIdButton)" Height="28" MinWidth="140"/>
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
      <Button Grid.Column="1" Name="BtnSave" Content="$($L.Save)" Width="160" Height="32"/>
    </Grid>
  </Grid>
</Window>
"@
}

function Initialize-MainWindow {
    
    param(
        [Parameter(Mandatory=$true)]
        [string]$XamlContent
    )

    $reader = New-Object System.Xml.XmlNodeReader([xml]$XamlContent)
    $window = [Windows.Markup.XamlReader]::Load($reader)

    $controls = @{
        Window = $window
        LvApps = $window.FindName("LvApps")
        BtnAdd = $window.FindName("BtnAdd")
        BtnEdit = $window.FindName("BtnEdit")
        BtnRemove = $window.FindName("BtnRemove")
        BtnSearch = $window.FindName("BtnSearch")
        BtnApply = $window.FindName("BtnApply")
        BtnSave = $window.FindName("BtnSave")
        CmbTabs = $window.FindName("CmbTabs")
        BtnNewTab = $window.FindName("BtnNewTab")
        BtnRenameTab = $window.FindName("BtnRenameTab")
        BtnDeleteTab = $window.FindName("BtnDeleteTab")
        TxtOutput = $window.FindName("TxtOutput")
        TxtStatus = $window.FindName("TxtStatus")
        SearchOverlay = $window.FindName("SearchOverlay")
        TxtSearchQuery = $window.FindName("TxtSearchQuery")
        BtnRunSearch = $window.FindName("BtnRunSearch")
        BtnCloseSearch = $window.FindName("BtnCloseSearch")
        LvSearchResults = $window.FindName("LvSearchResults")
        TxtSearchPickId = $window.FindName("TxtSearchPickId")
        BtnUseSearchId = $window.FindName("BtnUseSearchId")
    }

    return $controls
}

Export-ModuleMember -Function @(
    'Get-MainWindowXaml',
    'Initialize-MainWindow'
)
