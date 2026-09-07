$ErrorActionPreference = 'Stop'

$appName = "Kick"
$exeName = "KickAutoRecorder.App.exe"
$installDir = "$env:LocalAppData\Programs\Kick"
$publishDir = "c:\Users\kcuma\Desktop\anaklasor\projelerim\kick\src\KickAutoRecorder.App\bin\Release\net8.0-windows\win-x64\publish"

Write-Host "Installing $appName..." -ForegroundColor Cyan

if (Get-Process -Name "KickAutoRecorder.App" -ErrorAction SilentlyContinue) {
    Write-Host "Stopping running application..."
    Stop-Process -Name "KickAutoRecorder.App" -Force
    Start-Sleep -Seconds 2
}

if (-not (Test-Path "$installDir")) {
    New-Item -ItemType Directory -Path "$installDir" | Out-Null
}

Write-Host "Copying files to $installDir..."
Copy-Item -Path "$publishDir\*" -Destination "$installDir" -Recurse -Force

$wshShell = New-Object -ComObject WScript.Shell

$desktopPath = [Environment]::GetFolderPath('Desktop')
$shortcutPath = Join-Path $desktopPath "$appName.lnk"
$shortcut = $wshShell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = Join-Path $installDir $exeName
$shortcut.WorkingDirectory = $installDir
$shortcut.IconLocation = "$((Join-Path $installDir $exeName)),0"
$shortcut.Save()

$startMenuPath = Join-Path ([Environment]::GetFolderPath('Programs')) $appName
if (-not (Test-Path $startMenuPath)) {
    New-Item -ItemType Directory -Path $startMenuPath | Out-Null
}
$startMenuShortcutPath = Join-Path $startMenuPath "$appName.lnk"
$startMenuShortcut = $wshShell.CreateShortcut($startMenuShortcutPath)
$startMenuShortcut.TargetPath = Join-Path $installDir $exeName
$startMenuShortcut.WorkingDirectory = $installDir
$startMenuShortcut.IconLocation = "$((Join-Path $installDir $exeName)),0"
$startMenuShortcut.Save()

$registryPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\Kick"
if (-not (Test-Path $registryPath)) {
    New-Item -Path $registryPath -Force | Out-Null
}

$uninstallScriptPath = Join-Path $installDir "Uninstall.ps1"
$uninstallContent = @"
`$ErrorActionPreference = 'Stop'
Write-Host 'Uninstalling Kick...'
if (Get-Process -Name 'KickAutoRecorder.App' -ErrorAction SilentlyContinue) { Stop-Process -Name 'KickAutoRecorder.App' -Force; Start-Sleep -Seconds 2 }
Remove-Item -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\Kick' -Recurse -Force -ErrorAction SilentlyContinue
`$desktopPath = [Environment]::GetFolderPath('Desktop')
Remove-Item -Path (Join-Path `$desktopPath 'Kick.lnk') -Force -ErrorAction SilentlyContinue
`$startMenuPath = Join-Path ([Environment]::GetFolderPath('Programs')) 'Kick'
Remove-Item -Path `$startMenuPath -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -Path '`$env:LocalAppData\Programs\Kick' -Recurse -Force -ErrorAction SilentlyContinue
Write-Host 'Uninstalled successfully.'
Start-Sleep -Seconds 3
"@

Set-Content -Path $uninstallScriptPath -Value $uninstallContent

Set-ItemProperty -Path $registryPath -Name "DisplayName" -Value $appName
Set-ItemProperty -Path $registryPath -Name "DisplayIcon" -Value "$((Join-Path $installDir $exeName)),0"
Set-ItemProperty -Path $registryPath -Name "DisplayVersion" -Value "1.0.0"
Set-ItemProperty -Path $registryPath -Name "Publisher" -Value "Kick"
$uninstallString = "powershell.exe -ExecutionPolicy Bypass -WindowStyle Hidden -File ""$uninstallScriptPath"""
Set-ItemProperty -Path $registryPath -Name "UninstallString" -Value $uninstallString
Set-ItemProperty -Path $registryPath -Name "InstallLocation" -Value $installDir
Set-ItemProperty -Path $registryPath -Name "NoModify" -Value 1
Set-ItemProperty -Path $registryPath -Name "NoRepair" -Value 1

Write-Host "Installation Complete! You can now launch $appName from your desktop." -ForegroundColor Green
