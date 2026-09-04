param(
    [string]$SqlServer = 'localhost',
    [string]$Database = 'KianStore_2',
    [string]$BackupDirectory = 'D:\KianStoreBackups'
)

$ErrorActionPreference = 'Stop'

New-Item -ItemType Directory -Path $BackupDirectory -Force | Out-Null
$timestamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$backupFile = Join-Path $BackupDirectory "${Database}_${timestamp}.bak"

$sql = "BACKUP DATABASE [$Database] TO DISK = N'$backupFile' WITH INIT, COMPRESSION, CHECKSUM, STATS = 10;"

Write-Host "Backing up $Database on $SqlServer..." -ForegroundColor Cyan
sqlcmd -S $SqlServer -E -Q $sql

if ($LASTEXITCODE -ne 0) {
    throw "sqlcmd backup failed with exit code $LASTEXITCODE."
}

Write-Host "Backup completed: $backupFile" -ForegroundColor Green
