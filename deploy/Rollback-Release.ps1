param(
    [string]$EnvFile = "deploy/production.env"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $EnvFile)) {
    throw "Env file not found: $EnvFile"
}

$lines = Get-Content -Path $EnvFile
$current = $null
$previous = $null

foreach ($line in $lines) {
    if ($line -match '^PSN_RELEASE=(.+)$') { $current = $Matches[1] }
    if ($line -match '^PSN_PREVIOUS_RELEASE=(.+)$') { $previous = $Matches[1] }
}

if ([string]::IsNullOrWhiteSpace($previous)) {
    throw "PSN_PREVIOUS_RELEASE is missing in $EnvFile"
}

$content = Get-Content -Raw -Path $EnvFile
$content = [regex]::Replace($content, "(?m)^PSN_RELEASE=.*$", "PSN_RELEASE=$previous")

if (-not [string]::IsNullOrWhiteSpace($current)) {
    if ($content -match "(?m)^PSN_PREVIOUS_RELEASE=") {
        $content = [regex]::Replace($content, "(?m)^PSN_PREVIOUS_RELEASE=.*$", "PSN_PREVIOUS_RELEASE=$current")
    }
}

Set-Content -Path $EnvFile -Value $content -Encoding UTF8

Write-Host "Rolling back to release: $previous"
& docker compose --env-file $EnvFile pull
& docker compose --env-file $EnvFile up -d

Write-Host "Rollback completed. Active release: $previous"
