$ErrorActionPreference = "SilentlyContinue"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Split-Path -Parent $ScriptDir
$BackendDir = Join-Path $ProjectRoot "backend"
$FrontendDir = Join-Path $ProjectRoot "frontend"
$LogDir = Join-Path $ProjectRoot "logs"
$FrontendUrl = "http://localhost:5173/today"
$OllamaCommand = Get-Command "ollama.exe" -ErrorAction SilentlyContinue
$OllamaExe = if ($OllamaCommand) { $OllamaCommand.Source } else { $null }

if (-not $OllamaExe) {
    $OllamaExe = Join-Path $env:LOCALAPPDATA "Programs\Ollama\ollama.exe"
}

New-Item -ItemType Directory -Force -Path $LogDir | Out-Null

function Stop-PortProcess {
    param([int]$Port)

    $processId = Get-NetTCPConnection -State Listen -ErrorAction SilentlyContinue |
        Where-Object { $_.LocalPort -eq $Port } |
        Select-Object -First 1 -ExpandProperty OwningProcess

    if ($processId) {
        Stop-Process -Id $processId -Force -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 2
    }
}

function Start-HiddenPowerShell {
    param(
        [string]$WorkingDirectory,
        [string]$Command,
        [string]$LogName
    )

    $logPath = Join-Path $LogDir $LogName
    $wrapped = "Set-Location '$WorkingDirectory'; $Command *> '$logPath'"

    Start-Process powershell.exe -WindowStyle Hidden -ArgumentList @(
        "-NoProfile",
        "-ExecutionPolicy",
        "Bypass",
        "-Command",
        $wrapped
    ) | Out-Null
}

$ollamaListening = Get-NetTCPConnection -State Listen -ErrorAction SilentlyContinue |
    Where-Object { $_.LocalPort -eq 11434 }

if (-not $ollamaListening) {
    if (Test-Path $OllamaExe) {
        Start-HiddenPowerShell -WorkingDirectory $ProjectRoot -Command "`"$OllamaExe`" serve" -LogName "ollama.log"
    }
    Start-Sleep -Seconds 4
}

if (Test-Path $OllamaExe) {
    Start-HiddenPowerShell -WorkingDirectory $ProjectRoot -Command "`"$OllamaExe`" ps" -LogName "ollama-status.log"
}

Stop-PortProcess -Port 5000
Start-HiddenPowerShell -WorkingDirectory $BackendDir -Command "dotnet run --urls http://localhost:5000" -LogName "backend.log"

Stop-PortProcess -Port 5173
Start-HiddenPowerShell -WorkingDirectory $FrontendDir -Command "npm.cmd run dev -- --host 127.0.0.1" -LogName "frontend.log"

Start-Sleep -Seconds 5

Start-Process $FrontendUrl | Out-Null
