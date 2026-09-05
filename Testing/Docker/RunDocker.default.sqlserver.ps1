# RunDocker.extern.all.ps1 (control script)

# Container name configuration from docker-compose.yml
$ContainerName = "rm_db_sqlserver"

$ErrorActionPreference = "Stop"
Clear-Host

# Determine the current script name
$scriptName = Split-Path $MyInvocation.MyCommand.Path -Leaf

# Pattern: RunDocker.<environment>.<profile>.ps1
if ($scriptName -match "RunDocker\.([^.]+)\.([^.]+)\.ps1") {
    $environment = $matches[1]
    $profile = $matches[2]

    # Configuration variables
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

    # Path to the main script (relative to the current script)
    $mainScriptPath = Join-Path $PSScriptRoot "RunDockerXecute.ps1"

    if (Test-Path $mainScriptPath) {
        # Call the main script and pass the configuration
        & $mainScriptPath -Config $CONFIG
    }
    else {
        Write-Error "Main script not found: $mainScriptPath"
        pause
        exit 1
    }
}
else {
    Write-Error "File name does not match the expected format 'RunDocker.<environment>.<profile>.ps1'"
    pause
    exit 1
}
