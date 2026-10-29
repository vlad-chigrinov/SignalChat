# Runs the whole SignalChat stack locally: PostgreSQL (docker), API (https://localhost:5001)
# and, when present, the Vue frontend (http://localhost:5173). Ctrl+C stops everything.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

function Assert-LastExitCode([string]$step) {
    if ($LASTEXITCODE -ne 0) { throw "$step failed (exit code $LASTEXITCODE)." }
}

$placeholder = 'change-me'

function New-RandomBytes([int]$count) {
    $bytes = New-Object byte[] $count
    [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
    return , $bytes
}

# Fills a secret in .env only when it is missing, empty or still the placeholder: an existing
# value is never replaced (a new DB password or PII key would lock out existing data).
function Set-DotEnvSecretIfMissing([string]$name, [string]$value) {
    $pattern = "^$name=(.*)$"
    $lines = @(Get-Content .env)
    $current = $lines | Where-Object { $_ -match $pattern } | Select-Object -First 1
    if ($current -and (($current -replace $pattern, '$1').Trim() -notin @('', $placeholder))) { return }

    if ($current) { $lines = $lines -replace $pattern, "$name=$value" } else { $lines += "$name=$value" }
    Set-Content -Encoding ascii .env $lines
    Write-Host "Generated $name in .env."
}

# On Windows the file mode is not POSIX 0600: .env inherits the ACL of the user profile folder,
# which is already private to the current user. dev.sh sets chmod 600 on macOS / Linux / Git Bash.
if (-not (Test-Path .env)) { Copy-Item .env.example .env; Write-Host 'Created .env from .env.example.' }
Set-DotEnvSecretIfMissing 'POSTGRES_PASSWORD' (-join ((New-RandomBytes 24) | ForEach-Object { $_.ToString('x2') }))
Set-DotEnvSecretIfMissing 'PII_ENCRYPTION_KEY' ([Convert]::ToBase64String((New-RandomBytes 32)))

dotnet dev-certs https --check --trust *> $null
if ($LASTEXITCODE -ne 0) { dotnet dev-certs https --trust; Assert-LastExitCode 'HTTPS dev certificate' }

docker compose up -d --wait db
Assert-LastExitCode 'docker compose'

$frontend = $null
if (Test-Path frontend/package.json) {
    if (-not (Test-Path frontend/node_modules)) {
        npm --prefix frontend install
        Assert-LastExitCode 'npm install'
    }
    $frontend = Start-Process npm.cmd -ArgumentList 'run', 'dev' -WorkingDirectory (Join-Path $root 'frontend') -NoNewWindow -PassThru
}

try {
    dotnet run --project backend/src/SignalChat.Api --launch-profile https
}
finally {
    if ($frontend -and -not $frontend.HasExited) { taskkill /PID $frontend.Id /T /F *> $null }
}
