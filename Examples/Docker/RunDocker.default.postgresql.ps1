# RunDocker.default.postgresql.ps1 (Control script)

# Container name from docker-compose.yml
$ContainerName = "rm_exampleDB_PostgreSQL"

$ErrorActionPreference = "Stop"
Clear-Host

# Determine current script name
$scriptName = Split-Path $MyInvocation.MyCommand.Path -Leaf

# Pattern: RunDocker.<environment>.<profile>.ps1
if ($scriptName -match "RunDocker\.([^.]+)\.([^.]+)\.ps1") {
    $environment = $matches[1]
    $profile = $matches[2]

    # Configuration
    $CONFIG = @{
        EnvFile = "$environment.env"
        Profile = $profile
        ContainerName = $ContainerName
        WaitTimeInSeconds = 3
        TimeoutInSeconds = 20
        RequiredVars = @()
    }

    # Path to main script (relative to current script)
    $mainScriptPath = Join-Path $PSScriptRoot "RunDockerXecute.ps1"

    if (Test-Path $mainScriptPath) {
        & $mainScriptPath -Config $CONFIG
    }
    else {
        Write-Error "Main script not found: $mainScriptPath"
        exit 1
    }
}
else {
    Write-Error "Filename does not match expected format 'RunDocker.<environment>.<profile>.ps1'"
    exit 1
}
