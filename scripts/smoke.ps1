param([string]$BaseUrl='http://localhost:8080', [string]$DemoPassword)
$ErrorActionPreference='Stop'
if (-not $DemoPassword) {
    $passwordLine = Get-Content -LiteralPath .env | Where-Object { $_ -match '^DEMO_PASSWORD=' } | Select-Object -First 1
    if (-not $passwordLine) { throw 'Set DEMO_PASSWORD in .env or pass -DemoPassword.' }
    $DemoPassword = ($passwordLine -split '=',2)[1].Trim().Trim('"').Trim("'")
}
function Login-Bank([string]$Email) {
    $csrf = Invoke-RestMethod "$BaseUrl/api/auth/csrf" -SessionVariable bankSession
    $headers = @{'X-CSRF-TOKEN'=$csrf.token}
    $body = @{email=$Email;password=$DemoPassword} | ConvertTo-Json
    Invoke-RestMethod "$BaseUrl/api/auth/login" -Method Post -WebSession $bankSession -Headers $headers -ContentType application/json -Body $body | Out-Null
    $csrf = Invoke-RestMethod "$BaseUrl/api/auth/csrf" -WebSession $bankSession
    return [PSCustomObject]@{Session=$bankSession;Headers=@{'X-CSRF-TOKEN'=$csrf.token}}
}
function Assert-Status([string]$Path, $Bank, [int]$Expected) {
    $actual = 0
    try { $response=Invoke-WebRequest "$BaseUrl$Path" -WebSession $Bank.Session -UseBasicParsing; $actual=[int]$response.StatusCode }
    catch { if ($_.Exception.Response) { $actual=[int]$_.Exception.Response.StatusCode } else { throw } }
    if ($actual -ne $Expected) { throw "Expected $Expected for $Path; received $actual" }
}
$anna=Login-Bank 'anna@securebank.test'
$jan=Login-Bank 'jan@securebank.test'
$admin=Login-Bank 'admin@securebank.test'
$from=@(Invoke-RestMethod "$BaseUrl/api/accounts" -WebSession $anna.Session)[0]
$to=@(Invoke-RestMethod "$BaseUrl/api/accounts" -WebSession $jan.Session)[0]
$me=Invoke-RestMethod "$BaseUrl/api/auth/me" -WebSession $anna.Session
Assert-Status '/api/admin/users' $anna 403
Assert-Status "/api/accounts/$($to.id)" $anna 404
if ($from.balance -lt 100) { throw 'Anna needs at least 100 PLN for the smoke transfer.' }
$body=@{fromAccountId=$from.id;toAccountNumber=$to.accountNumber;amount=100;title='Smoke verification'} | ConvertTo-Json
$transfer=Invoke-RestMethod "$BaseUrl/api/transactions" -Method Post -WebSession $anna.Session -Headers $anna.Headers -ContentType application/json -Body $body
$afterFrom=Invoke-RestMethod "$BaseUrl/api/accounts/$($from.id)" -WebSession $anna.Session
$afterTo=Invoke-RestMethod "$BaseUrl/api/accounts/$($to.id)" -WebSession $jan.Session
if ([decimal]$afterFrom.balance -ne ([decimal]$from.balance-100) -or [decimal]$afterTo.balance -ne ([decimal]$to.balance+100)) { throw 'Transfer balance mismatch' }
$history=@(Invoke-RestMethod "$BaseUrl/api/transactions" -WebSession $anna.Session)
if (-not ($history | Where-Object id -eq $transfer.id)) { throw 'Transfer missing in history' }
$logs=@(Invoke-RestMethod "$BaseUrl/api/admin/logs?userId=$($me.id)" -WebSession $admin.Session)
foreach ($eventType in @('LOGIN_SUCCESS','UNAUTHORIZED_ACCESS','TRANSFER_SUCCESS')) {
    if (-not ($logs | Where-Object eventType -eq $eventType)) { throw "Missing audit event: $eventType" }
}
Write-Output 'HTTP smoke OK: login, accounts, transfer, history, RBAC, ownership, security logs.'
Write-Output 'Created a demonstrative 100 PLN transfer from Anna to Jan.'
