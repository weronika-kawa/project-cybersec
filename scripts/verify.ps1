$ErrorActionPreference = 'Stop'
docker compose config --quiet
if ($LASTEXITCODE -ne 0) { throw 'Invalid compose configuration' }
docker compose up -d --build
if ($LASTEXITCODE -ne 0) { throw 'Build/start failed' }
docker compose --profile test run --rm tests
if ($LASTEXITCODE -ne 0) { throw 'Tests failed' }
Invoke-WebRequest http://localhost:8080/ -UseBasicParsing | Select-Object StatusCode
Invoke-RestMethod http://localhost:8080/api/auth/csrf
& "$PSScriptRoot/smoke.ps1"
