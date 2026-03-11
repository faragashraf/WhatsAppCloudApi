$ErrorActionPreference = 'Stop'

$baseUrl = 'http://localhost:5222'
$projectDirectory = (Resolve-Path (Join-Path $PSScriptRoot '..\WhatsAppCloudApi.Api')).Path
$projectPath = Join-Path $projectDirectory 'WhatsAppCloudApi.Api.csproj'
$developmentSettingsPath = Join-Path $projectDirectory 'appsettings.Development.json'
$defaultSettingsPath = Join-Path $projectDirectory 'appsettings.json'
$loginBodyFile = Join-Path $PSScriptRoot 'tmp-login.json'
$stdoutFile = Join-Path $PSScriptRoot 'tmp-run-stdout.log'
$stderrFile = Join-Path $PSScriptRoot 'tmp-run-stderr.log'
'{"email":"security-test@example.com","password":"WrongPass1!"}' | Set-Content -Path $loginBodyFile -NoNewline
$originalJwtEnv = $env:JWT__KEY

function Normalize-PathBase {
    param(
        [AllowNull()]
        [string]$Value
    )

    if ([string]::IsNullOrWhiteSpace($Value)) {
        return '/'
    }

    $normalized = $Value.Trim()
    if (-not $normalized.StartsWith('/')) {
        $normalized = "/$normalized"
    }

    if ($normalized.Length -gt 1 -and $normalized.EndsWith('/')) {
        $normalized = $normalized.TrimEnd('/')
    }

    return $normalized
}

function Build-Url {
    param(
        [Parameter(Mandatory = $true)]
        [string]$BaseUrl,
        [Parameter(Mandatory = $true)]
        [string]$PathBase,
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $prefix = if ($PathBase -eq '/') { '' } else { $PathBase }
    return "$BaseUrl$prefix$Path"
}

$configuredPathBase = '/'
if (Test-Path $developmentSettingsPath) {
    $developmentSettings = Get-Content -Path $developmentSettingsPath -Raw | ConvertFrom-Json
    if ($null -ne $developmentSettings.PathBase) {
        $configuredPathBase = [string]$developmentSettings.PathBase
    }
}

$pathBase = Normalize-PathBase -Value $configuredPathBase

if ([string]::IsNullOrWhiteSpace($env:JWT__KEY)) {
    $resolvedJwtKey = $null

    if (Test-Path $developmentSettingsPath) {
        $developmentSettings = Get-Content -Path $developmentSettingsPath -Raw | ConvertFrom-Json
        if ($null -ne $developmentSettings.Jwt -and $null -ne $developmentSettings.Jwt.Key) {
            $resolvedJwtKey = [string]$developmentSettings.Jwt.Key
        }
    }

    if ([string]::IsNullOrWhiteSpace($resolvedJwtKey) -and (Test-Path $defaultSettingsPath)) {
        $defaultSettings = Get-Content -Path $defaultSettingsPath -Raw | ConvertFrom-Json
        if ($null -ne $defaultSettings.Jwt -and $null -ne $defaultSettings.Jwt.Key) {
            $resolvedJwtKey = [string]$defaultSettings.Jwt.Key
        }
    }

    $isPlaceholder = [string]::IsNullOrWhiteSpace($resolvedJwtKey) `
        -or $resolvedJwtKey.StartsWith('REPLACE_WITH_', [System.StringComparison]::OrdinalIgnoreCase) `
        -or $resolvedJwtKey.StartsWith('__SET_', [System.StringComparison]::OrdinalIgnoreCase) `
        -or $resolvedJwtKey.Length -lt 64

    if ($isPlaceholder) {
        $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
        try {
            $bytes = New-Object byte[] 64
            $rng.GetBytes($bytes)
            $env:JWT__KEY = [Convert]::ToBase64String($bytes)
        }
        finally {
            $rng.Dispose()
        }
    }
}

function Invoke-CurlStatus {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Method,
        [Parameter(Mandatory = $true)]
        [string]$Url,
        [string]$Body = ''
    )

    if ([string]::IsNullOrEmpty($Body)) {
        return ((& curl.exe -s -X $Method -o - -w "`nSTATUS:%{http_code}" $Url) -join "`n")
    }

    return ((& curl.exe -s -X $Method -H 'Content-Type: application/json' -d $Body -o - -w "`nSTATUS:%{http_code}" $Url) -join "`n")
}

$proc = Start-Process dotnet `
    -ArgumentList @('run', '--project', "`"$projectPath`"", '--no-build', '--launch-profile', 'http') `
    -PassThru `
    -WindowStyle Hidden `
    -RedirectStandardOutput $stdoutFile `
    -RedirectStandardError $stderrFile

try {
    $healthUrl = Build-Url -BaseUrl $baseUrl -PathBase $pathBase -Path '/health'
    $alternateHealthUrl = if ($pathBase -eq '/WhatsAppApi') {
        Build-Url -BaseUrl $baseUrl -PathBase '/' -Path '/health'
    }
    else {
        Build-Url -BaseUrl $baseUrl -PathBase '/WhatsAppApi' -Path '/health'
    }
    $webhookUrl = Build-Url -BaseUrl $baseUrl -PathBase $pathBase -Path '/api/webhook'
    $webhookLogsUrl = Build-Url -BaseUrl $baseUrl -PathBase $pathBase -Path '/api/webhook/logs'
    $loginUrl = Build-Url -BaseUrl $baseUrl -PathBase $pathBase -Path '/api/auth/login'

    $started = $false
    for ($attempt = 1; $attempt -le 45; $attempt++) {
        if ($proc.HasExited) {
            break
        }

        $healthProbe = (& curl.exe -s -o NUL -w "%{http_code}" $healthUrl)
        if ($healthProbe -match '^\d{3}$' -and $healthProbe -ne '000') {
            $started = $true
            break
        }

        Start-Sleep -Seconds 2
    }

    if (-not $started) {
        if (Test-Path $stdoutFile) {
            Write-Output '===RUN_STDOUT==='
            Get-Content $stdoutFile
        }
        if (Test-Path $stderrFile) {
            Write-Output '===RUN_STDERR==='
            Get-Content $stderrFile
        }
        throw "API did not start or was unreachable at $healthUrl"
    }

    $healthHeaders = & curl.exe -s -D - -o NUL $healthUrl
    $health = Invoke-CurlStatus -Method GET -Url $healthUrl
    $alternateHealthStatus = (& curl.exe -s -o NUL -w "%{http_code}" $alternateHealthUrl)
    $webhookVerify = Invoke-CurlStatus -Method GET -Url $webhookUrl
    $webhookReceive = ((& curl.exe -s -X POST -H 'Content-Type: application/json' --data-binary '{}' -o - -w "`nSTATUS:%{http_code}" $webhookUrl) -join "`n")
    $webhookLogs = Invoke-CurlStatus -Method GET -Url $webhookLogsUrl
    $loginAttempt = ((& curl.exe -s -X POST -H 'Content-Type: application/json' --data-binary "@$loginBodyFile" -o - -w "`nSTATUS:%{http_code}" $loginUrl) -join "`n")
    $corsAllowed = & curl.exe -s -D - -o NUL -X OPTIONS -H 'Origin: http://localhost:4200' -H 'Access-Control-Request-Method: POST' $loginUrl
    $corsAllowed4201 = & curl.exe -s -D - -o NUL -X OPTIONS -H 'Origin: http://localhost:4201' -H 'Access-Control-Request-Method: POST' $loginUrl
    $corsBlocked = & curl.exe -s -D - -o NUL -X OPTIONS -H 'Origin: https://evil.example' -H 'Access-Control-Request-Method: POST' $loginUrl

    $rateStatuses = @()
    for ($i = 1; $i -le 14; $i++) {
        $resp = ((& curl.exe -s -X POST -H 'Content-Type: application/json' --data-binary "@$loginBodyFile" -o - -w "`nSTATUS:%{http_code}" $loginUrl) -join "`n")
        $statusMatch = [regex]::Match($resp, 'STATUS:(\d{3})')
        if ($statusMatch.Success) {
            $rateStatuses += $statusMatch.Groups[1].Value
        }
    }

    Write-Output '===HEALTH_HEADERS==='
    Write-Output $healthHeaders
    Write-Output '===HEALTH==='
    Write-Output $health
    Write-Output '===ALTERNATE_PATHBASE_HEALTH_STATUS==='
    Write-Output $alternateHealthStatus
    Write-Output '===WEBHOOK_VERIFY==='
    Write-Output $webhookVerify
    Write-Output '===WEBHOOK_RECEIVE==='
    Write-Output $webhookReceive
    Write-Output '===WEBHOOK_LOGS==='
    Write-Output $webhookLogs
    Write-Output '===LOGIN_ATTEMPT==='
    Write-Output $loginAttempt
    Write-Output '===CORS_ALLOWED_PRELIGHT==='
    Write-Output $corsAllowed
    Write-Output '===CORS_ALLOWED_4201_PRELIGHT==='
    Write-Output $corsAllowed4201
    Write-Output '===CORS_BLOCKED_PRELIGHT==='
    Write-Output $corsBlocked
    Write-Output '===RATE_STATUS_SERIES==='
    Write-Output ($rateStatuses -join ',')
}
finally {
    if ($null -eq $originalJwtEnv) {
        Remove-Item Env:JWT__KEY -ErrorAction SilentlyContinue
    }
    else {
        $env:JWT__KEY = $originalJwtEnv
    }
    Remove-Item $loginBodyFile -ErrorAction SilentlyContinue
    Remove-Item $stdoutFile -ErrorAction SilentlyContinue
    Remove-Item $stderrFile -ErrorAction SilentlyContinue
    if ($null -ne $proc -and -not $proc.HasExited) {
        Stop-Process -Id $proc.Id -Force
    }
}
