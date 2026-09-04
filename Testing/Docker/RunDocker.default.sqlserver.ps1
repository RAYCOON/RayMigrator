# RunDocker.extern.all.ps1 (Steuerungsskript)

# Konfiguration des ContainerNamen aus docker-compose.yml
$ContainerName = "rm_db_sqlserver"

$ErrorActionPreference = "Stop"
Clear-Host

# Aktuellen Skriptnamen ermitteln
$scriptName = Split-Path $MyInvocation.MyCommand.Path -Leaf

# Pattern: RunDocker.<environment>.<profile>.ps1
if ($scriptName -match "RunDocker\.([^.]+)\.([^.]+)\.ps1") {
    $environment = $matches[1]
    $profile = $matches[2]

    # Konfigurationsvariablen
    $CONFIG = @{
        EnvFile = "$environment.env"
        Profile = $profile
        ContainerName = $ContainerName
        WaitTimeInSeconds = 3
        TimeoutInSeconds = 20
        RequiredVars = @(
            "SOURCE_MICROSOFT", "MSSQL_SA_PASSWORD", "RM_LOGIN_PASSWORD"
        )
    }

    # Pfad zum Hauptskript (relativ zum aktuellen Skript)
    $mainScriptPath = Join-Path $PSScriptRoot "RunDockerXecute.ps1"

    if (Test-Path $mainScriptPath) {
        # Aufruf des Hauptskripts mit Übergabe der Konfiguration
        & $mainScriptPath -Config $CONFIG
    }
    else {
        Write-Error "Hauptskript nicht gefunden: $mainScriptPath"
        pause
        exit 1
    }
}
else {
    Write-Error "Dateiname entspricht nicht dem erwarteten Format 'RunDocker.<environment>.<profile>.ps1'"
    pause
    exit 1
}
