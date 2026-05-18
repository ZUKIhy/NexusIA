param(
    [string]$BaseUrl = "http://localhost:5000"
)

$ErrorActionPreference = "Stop"

function Test-Endpoint {
    param(
        [string]$Name,
        [string]$Method = "GET",
        [string]$Path,
        [string]$Body = "{}"
    )

    try {
        $uri = "$BaseUrl$Path"
        if ($Method -eq "POST") {
            Invoke-RestMethod -Uri $uri -Method Post -ContentType "application/json" -Body $Body -TimeoutSec 30 | Out-Null
        } else {
            Invoke-RestMethod -Uri $uri -TimeoutSec 20 | Out-Null
        }

        [pscustomobject]@{ Check = $Name; Status = "OK"; Detail = $Path }
    } catch {
        [pscustomobject]@{ Check = $Name; Status = "FAIL"; Detail = $_.Exception.Message }
    }
}

function Test-PathExists {
    param(
        [string]$Name,
        [string]$Path
    )

    [pscustomobject]@{
        Check = $Name
        Status = if (Test-Path $Path) { "OK" } else { "FAIL" }
        Detail = $Path
    }
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$backendEnv = Join-Path $repoRoot "backend\.env"
$vaultPath = Join-Path $repoRoot "vault"

if (Test-Path $backendEnv) {
    Get-Content $backendEnv | ForEach-Object {
        $line = $_.Trim()
        if ($line -and -not $line.StartsWith("#") -and $line.Contains("=")) {
            $parts = $line.Split("=", 2)
            if ($parts[0].Trim() -eq "VAULT_PATH") {
                $configuredVault = $parts[1].Trim()
                if ([System.IO.Path]::IsPathRooted($configuredVault)) {
                    $vaultPath = $configuredVault
                } else {
                    $vaultPath = Join-Path (Join-Path $repoRoot "backend") $configuredVault
                }
            }
        }
    }
}

$checks = @()
$checks += Test-Endpoint -Name "Nexus status" -Path "/api/nexus/status"
$checks += Test-Endpoint -Name "Index" -Path "/api/index"
$checks += Test-Endpoint -Name "Docs search" -Path "/api/docs/search?q=journal"
$checks += Test-Endpoint -Name "Home Assistant" -Path "/api/home/status"
$checks += Test-Endpoint -Name "Network status" -Path "/api/network/status"
$checks += Test-Endpoint -Name "Chat local" -Method "POST" -Path "/api/nexus/chat" -Body '{"message":"Nexus, status da rede"}'
$checks += Test-PathExists -Name "Vault existe" -Path $vaultPath
$checks += Test-PathExists -Name "Backup script existe" -Path (Join-Path $repoRoot "scripts\backup-vault.ps1")

$checks | Format-Table -AutoSize

if ($checks.Status -contains "FAIL") {
    exit 1
}
