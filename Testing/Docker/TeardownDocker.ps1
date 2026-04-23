# Stops all Docker containers, removes volumes and network
# Usage: pwsh TeardownDocker.ps1

$ErrorActionPreference = "Stop"

Write-Host "Stopping and removing all RayMigrator Docker containers..." -ForegroundColor Yellow

docker-compose --env-file default.env --profile all down -v

Write-Host "All containers stopped and volumes removed." -ForegroundColor Green
