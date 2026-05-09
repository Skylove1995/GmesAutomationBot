param(
    [Parameter(Mandatory = $true)]
    [string]$ExePath,

    [Parameter(Mandatory = $true)]
    [string]$WorkingDirectory,

    [int]$IntervalMinutes = 15,

    [string]$TaskName = "GMES Production Importer"
)

$resolvedExe = (Resolve-Path -LiteralPath $ExePath).Path
$resolvedWorkingDirectory = (Resolve-Path -LiteralPath $WorkingDirectory).Path

$action = New-ScheduledTaskAction `
    -Execute $resolvedExe `
    -Argument "run-once" `
    -WorkingDirectory $resolvedWorkingDirectory

$trigger = New-ScheduledTaskTrigger `
    -Once `
    -At (Get-Date).AddMinutes(1) `
    -RepetitionInterval (New-TimeSpan -Minutes $IntervalMinutes) `
    -RepetitionDuration (New-TimeSpan -Days 3650)

$settings = New-ScheduledTaskSettingsSet `
    -MultipleInstances IgnoreNew `
    -StartWhenAvailable `
    -ExecutionTimeLimit (New-TimeSpan -Minutes ([Math]::Max(10, $IntervalMinutes - 1)))

Register-ScheduledTask `
    -TaskName $TaskName `
    -Action $action `
    -Trigger $trigger `
    -Settings $settings `
    -Description "Exports GMES production data and imports it into mex_mes.tb_gmes_production." `
    -Force

Write-Host "Scheduled task '$TaskName' installed with $IntervalMinutes minute interval."
