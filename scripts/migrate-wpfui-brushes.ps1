# PowerShell script to replace all custom brush keys with WPF-UI Fluent equivalents
param([string]$UiDir)

if (-not $UiDir) {
    $solutionRoot = Split-Path -Parent $PSScriptRoot
    $UiDir = Join-Path $solutionRoot "src\MySecondBrain.UI"
}

$xamlFiles = Get-ChildItem -Path $UiDir -Recurse -Filter *.xaml | Where-Object { $_.DirectoryName -notlike '*\obj\*' -and $_.DirectoryName -notlike '*\bin\*' }

$replacements = @{
    'ContentBackground'      = 'ApplicationBackgroundBrush'
    'InputBackground'        = 'CardBackgroundFillColorDefaultBrush'
    'ContentForeground'      = 'TextFillColorPrimaryBrush'
    'SubtleBrush'            = 'TextFillColorSecondaryBrush'
    'AccentBrush'            = 'AccentFillColorDefaultBrush'
    'AccentForeground'       = 'TextOnAccentFillColorPrimaryBrush'
    'BorderBrush'            = 'CardStrokeColorDefaultBrush'
    'AppBackground'          = 'ApplicationBackgroundBrush'
    'SidebarBackground'      = 'ApplicationBackgroundBrush'
    'PanelBackground'        = 'CardBackgroundFillColorDefaultBrush'
    'TabBarBackground'       = 'CardBackgroundFillColorDefaultBrush'
    'HeaderBackground'       = 'CardBackgroundFillColorDefaultBrush'
    'CardBackground'         = 'ControlFillColorDefaultBrush'
    'AppForeground'          = 'TextFillColorPrimaryBrush'
    'SidebarForeground'      = 'TextFillColorPrimaryBrush'
    'PanelForeground'        = 'TextFillColorPrimaryBrush'
    'GridSplitterBrush'      = 'CardStrokeColorDefaultBrush'
    'StarBrush'              = 'TextFillColorSecondaryBrush'
    'NavActiveBackground'    = 'AccentFillColorDefaultBrush'
    'NavInactiveForeground'  = 'TextFillColorSecondaryBrush'
    'AccentBackground'       = 'ControlFillColorDefaultBrush'
    'SuccessBrush'           = 'SystemFillColorSuccessBrush'
    'WarningBrush'           = 'SystemFillColorCautionBrush'
    'ErrorBrush'             = 'SystemFillColorCriticalBrush'
}

$totalChanges = 0
$totalFiles = 0

foreach ($file in $xamlFiles) {
    $content = Get-Content $file.FullName -Raw
    $original = $content
    $fileChanges = 0

    foreach ($oldKey in $replacements.Keys) {
        $newKey = $replacements[$oldKey]
        
        # Replace {DynamicResource OldKey} with {DynamicResource NewKey}
        $patternOld = [regex]::Escape("{DynamicResource $oldKey}")
        $replacementNew = "{DynamicResource $newKey}"
        if ($content -match $patternOld) {
            $content = $content -replace $patternOld, $replacementNew
            $fileChanges++
        }

        # Replace {StaticResource OldKey} with {StaticResource NewKey}
        $patternOldStatic = [regex]::Escape("{StaticResource $oldKey}")
        $replacementNewStatic = "{StaticResource $newKey}"
        if ($content -match $patternOldStatic) {
            $content = $content -replace $patternOldStatic, $replacementNewStatic
            $fileChanges++
        }
    }

    if ($fileChanges -gt 0) {
        Set-Content $file.FullName $content -NoNewline
        $totalChanges += $fileChanges
        $totalFiles++
        Write-Host "$($file.Name): $fileChanges replacements"
    }
}

Write-Host "Done! $totalChanges replacements across $totalFiles files."
