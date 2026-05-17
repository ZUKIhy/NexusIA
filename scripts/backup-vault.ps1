$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Split-Path -Parent $ScriptDir
$EnvPath = Join-Path $ProjectRoot "backend\.env"
$VaultPath = ""

if (Test-Path $EnvPath) {
    foreach ($line in Get-Content $EnvPath) {
        if ($line -match "^VAULT_PATH=(.+)$") {
            $VaultPath = $Matches[1].Trim()
        }
    }
}

if (-not $VaultPath) {
    $VaultPath = Join-Path $ProjectRoot "vault"
}

if (-not [System.IO.Path]::IsPathRooted($VaultPath)) {
    $VaultPath = Join-Path (Join-Path $ProjectRoot "backend") $VaultPath
    $VaultPath = [System.IO.Path]::GetFullPath($VaultPath)
}

if (-not (Test-Path $VaultPath)) {
    throw "Vault nao encontrado em: $VaultPath"
}

$BackupBase = "C:\Users\User\OneDrive\Documentos\nexus-gabriel\Backups"

if (-not (Test-Path (Split-Path -Parent $BackupBase))) {
    $BackupBase = Join-Path $ProjectRoot "Backups"
}

$BackupRoot = Join-Path $BackupBase "NexusVault"
New-Item -ItemType Directory -Force -Path $BackupRoot | Out-Null

$Stamp = Get-Date -Format "yyyy-MM-dd-HHmmss"
$ZipPath = Join-Path $BackupRoot "nexus-vault-$Stamp.zip"

Compress-Archive -Path (Join-Path $VaultPath "*") -DestinationPath $ZipPath -Force

Get-ChildItem $BackupRoot -Filter "nexus-vault-*.zip" |
    Sort-Object LastWriteTime -Descending |
    Select-Object -Skip 10 |
    Remove-Item -Force

Write-Host "Backup criado em: $ZipPath"
