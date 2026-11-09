param([Parameter(Mandatory=$true)][string]$BackupFile)
$ErrorActionPreference = 'Stop'
$restorePath = (Resolve-Path -LiteralPath $BackupFile).Path
docker compose stop api web
docker compose cp "$restorePath" db:/tmp/restore.dump
if ($LASTEXITCODE -ne 0) { throw 'Restore copy failed' }
docker compose exec -T db pg_restore -U securebank -d securebank --clean --if-exists --single-transaction /tmp/restore.dump
if ($LASTEXITCODE -ne 0) { throw 'pg_restore failed; services remain stopped' }
$invalidateSql = 'UPDATE "AspNetUsers" SET "SecurityStamp"=gen_random_uuid()::text;'
$invalidateSql | docker compose exec -T db psql -U securebank -d securebank -v ON_ERROR_STOP=1
if ($LASTEXITCODE -ne 0) { throw 'Session invalidation failed; services remain stopped' }
docker compose start api web
$verificationSql = 'SELECT count(*) FROM "Accounts"; SELECT sum("Balance") FROM "Accounts";'
$verificationSql | docker compose exec -T db psql -U securebank -d securebank -v ON_ERROR_STOP=1
if ($LASTEXITCODE -ne 0) { throw 'Restore verification failed' }
