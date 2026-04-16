param(
    [string]$TaskName = "ClaudeLog.Web"
)

$ErrorActionPreference = "Stop"

function Pause-BeforeExit {
    Write-Host
    Read-Host "Press Enter to exit"
}

$appRoot = "C:\Apps\ClaudeLog.Web"
$appExe = Join-Path $appRoot "ClaudeLog.Web.exe"
$appUrl = "http://localhost:15088"
$currentUser = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name
$taskDescription = "Starts ClaudeLog.Web at user logon from C:\Apps\ClaudeLog.Web."
$launcherScript = Join-Path $appRoot "Start-ClaudeLog.Web.ps1"

function Write-Section([string]$Title) {
    Write-Host "============================================"
    Write-Host $Title
    Write-Host "============================================"
    Write-Host
}

try {
    Write-Section "ClaudeLog - Install or Update Scheduled Task"

    if (-not (Test-Path -LiteralPath $appExe)) {
        throw "Published app not found at $appExe. Run ClaudeLog.update-and-run.bat first."
    }

    $launcherContent = @'
param(
    [string]$AppExe,
    [string]$AppRoot,
    [string]$AppUrl
)

$env:ASPNETCORE_ENVIRONMENT = 'Production'
$env:ASPNETCORE_URLS = $AppUrl

Start-Process `
    -FilePath $AppExe `
    -WorkingDirectory $AppRoot `
    -WindowStyle Hidden
'@

    Set-Content -LiteralPath $launcherScript -Value $launcherContent -Encoding ASCII

    $command = @"
& '$launcherScript' -AppExe '$appExe' -AppRoot '$appRoot' -AppUrl '$appUrl'
"@

    $encodedCommand = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($command))

    $action = New-ScheduledTaskAction `
        -Execute "powershell.exe" `
        -Argument "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -EncodedCommand $encodedCommand" `
        -WorkingDirectory $appRoot

    $trigger = New-ScheduledTaskTrigger -AtLogOn -User $currentUser

    $settings = New-ScheduledTaskSettingsSet `
        -AllowStartIfOnBatteries `
        -DontStopIfGoingOnBatteries `
        -StartWhenAvailable `
        -MultipleInstances IgnoreNew `
        -RestartCount 3 `
        -RestartInterval (New-TimeSpan -Minutes 1)

    $principal = New-ScheduledTaskPrincipal `
        -UserId $currentUser `
        -LogonType Interactive `
        -RunLevel Limited

    $task = New-ScheduledTask `
        -Action $action `
        -Trigger $trigger `
        -Principal $principal `
        -Settings $settings `
        -Description $taskDescription

    $existingTask = Get-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue
    if ($null -ne $existingTask) {
        Write-Host "Updating scheduled task '$TaskName' for user $currentUser..."
        Stop-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue | Out-Null
        Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false
    }
    else {
        Write-Host "Creating scheduled task '$TaskName' for user $currentUser..."
    }

    Register-ScheduledTask -TaskName $TaskName -InputObject $task | Out-Null

    Write-Host
    Write-Host "Scheduled task '$TaskName' is ready."
    Write-Host "It will start ClaudeLog.Web at logon for $currentUser."
    Write-Host "Target executable: $appExe"
    Write-Host "Launcher script: $launcherScript"
    Write-Host "URL: $appUrl"
    Write-Host "You can start it now with:"
    Write-Host "  Start-ScheduledTask -TaskName '$TaskName'"
}
catch {
    Write-Host
    Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
}
finally {
    Pause-BeforeExit
}
