$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force backups | Out-Null
$backupName = 'securebank-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.dump'
docker compose exec -T db pg_dump -U securebank -d securebank -Fc -f /tmp/securebank.dump
if ($LASTEXITCODE -ne 0) { throw 'pg_dump failed' }
docker compose cp db:/tmp/securebank.dump "backups/$backupName"
if ($LASTEXITCODE -ne 0) { throw 'Backup copy failed' }
Write-Output "Backup: backups/$backupName"
