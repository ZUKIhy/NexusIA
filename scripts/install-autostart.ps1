$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Split-Path -Parent $ScriptDir
$StartScript = Join-Path $ScriptDir "start-nexus.ps1"
$StartupFolder = [Environment]::GetFolderPath("Startup")
$ShortcutPath = Join-Path $StartupFolder "Nexus Autostart.lnk"

$Shell = New-Object -ComObject WScript.Shell
$Shortcut = $Shell.CreateShortcut($ShortcutPath)
$Shortcut.TargetPath = "powershell.exe"
$Shortcut.Arguments = "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File `"$StartScript`""
$Shortcut.WorkingDirectory = $ProjectRoot
$Shortcut.IconLocation = "powershell.exe,0"
$Shortcut.Description = "Inicia Nexus backend, frontend e Ollama"
$Shortcut.Save()

Write-Host "Autostart criado em: $ShortcutPath"
