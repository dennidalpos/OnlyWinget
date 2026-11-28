Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

function Get-UiTestStateSnapshot {
    param([Parameter(Mandatory)][System.Windows.Automation.AutomationElement]$Root)

    $elements = @($Root) + @($Root.FindAll([System.Windows.Automation.TreeScope]::Descendants,
        [System.Windows.Automation.Condition]::TrueCondition))
    $state = foreach ($element in $elements) {
        $toggleState = $null
        $isSelected = $null
        $selectedItems = $null
        $pattern = $null
        if ($element.TryGetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern, [ref]$pattern)) {
            $toggleState = [string]$pattern.Current.ToggleState
        }
        if ($element.TryGetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern, [ref]$pattern)) {
            $isSelected = $pattern.Current.IsSelected
        }
        if ($element.TryGetCurrentPattern([System.Windows.Automation.SelectionPattern]::Pattern, [ref]$pattern)) {
            $selectedItems = @($pattern.Current.GetSelection() | ForEach-Object {
                [pscustomobject]@{ name = $_.Current.Name; automationId = $_.Current.AutomationId }
            })
        }
        [pscustomobject]@{
            name = $element.Current.Name
            automationId = $element.Current.AutomationId
            controlType = $element.Current.ControlType.ProgrammaticName
            toggleState = $toggleState
            isSelected = $isSelected
            selectedItems = $selectedItems
        }
    }
    ConvertTo-Json -InputObject @($state) -Depth 5 -Compress
}
