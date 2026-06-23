param(
    [string]$EnvFile = "deploy/production.env"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $EnvFile)) {
    throw "Env file not found: $EnvFile"
}

$lines = Get-Content -Path $EnvFile
$release = $null
$previous = $null

foreach ($line in $lines) {
    if ($line -match '^PSN_RELEASE=(.+)$') { $release = $Matches[1] }
    if ($line -match '^PSN_PREVIOUS_RELEASE=(.+)$') { $previous = $Matches[1] }
}

Write-Host "Active release:   $release"
Write-Host "Previous release: $previous"
Write-Host ""
Write-Host "Running service status check..."
& docker compose --env-file $EnvFile ps
