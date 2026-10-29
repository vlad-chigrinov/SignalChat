# Starts the API for end-to-end runs on https://localhost:5001 with test sign-in enabled (POST /auth/test-login).
# The database is a clean signalchat_e2e, recreated on every run; the development database is never touched.
# Migrations are applied by the API on startup (Development). Ctrl+C stops the API.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$e2eDatabase = 'signalchat_e2e'

function Assert-LastExitCode([string]$step) {
    if ($LASTEXITCODE -ne 0) { throw "$step failed (exit code $LASTEXITCODE)." }
}

if (-not (Test-Path .env)) { throw '.env is missing: run ./scripts/dev.ps1 once first.' }
$postgresUser = ((Get-Content .env | Where-Object { $_ -match '^POSTGRES_USER=' } | Select-Object -First 1) -replace '^POSTGRES_USER=', '').Trim()

dotnet dev-certs https --check --trust *> $null
if ($LASTEXITCODE -ne 0) { dotnet dev-certs https --trust; Assert-LastExitCode 'HTTPS dev certificate' }

docker compose up -d --wait db
Assert-LastExitCode 'docker compose'

# Recreate the e2e database from scratch. Connects to the maintenance database "postgres", never to the dev one.
@(
    "DROP DATABASE IF EXISTS $e2eDatabase WITH (FORCE);",
    "CREATE DATABASE $e2eDatabase;"
) | docker compose exec -T db psql -v ON_ERROR_STOP=1 -U $postgresUser -d postgres
Assert-LastExitCode 'Recreating the e2e database'

# The overrides are restored afterwards: the caller's shell must not keep pointing at the e2e database.
$overrides = @{
    POSTGRES_DB            = $e2eDatabase
    ASPNETCORE_ENVIRONMENT = 'E2E'
    E2E_TEST_AUTH          = 'true'
}
$previous = @{}
foreach ($name in $overrides.Keys) { $previous[$name] = [Environment]::GetEnvironmentVariable($name) }
try {
    foreach ($name in $overrides.Keys) { [Environment]::SetEnvironmentVariable($name, $overrides[$name]) }
    dotnet run --project backend/src/SignalChat.Api --launch-profile e2e
}
finally {
    foreach ($name in $previous.Keys) { [Environment]::SetEnvironmentVariable($name, $previous[$name]) }
}
