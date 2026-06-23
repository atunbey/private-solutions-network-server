param(
    [Parameter(Mandatory = $true)]
    [string]$ReleaseTag,

    [string]$EnvFile = "deploy/production.env"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $EnvFile)) {
    throw "Env file not found: $EnvFile"
}

$content = Get-Content -Raw -Path $EnvFile
if ($content -match "(?m)^PSN_RELEASE=") {
    $content = [regex]::Replace($content, "(?m)^PSN_RELEASE=.*$", "PSN_RELEASE=$ReleaseTag")
} else {
    if (-not $content.EndsWith("`n")) { $content += "`n" }
    $content += "PSN_RELEASE=$ReleaseTag`n"
}

Set-Content -Path $EnvFile -Value $content -Encoding UTF8

Write-Host "Updated PSN_RELEASE to $ReleaseTag in $EnvFile"
Write-Host "Pulling images..."
& docker compose --env-file $EnvFile pull

Write-Host "Applying release..."
& docker compose --env-file $EnvFile up -d

Write-Host "Release deployment completed: $ReleaseTag"
