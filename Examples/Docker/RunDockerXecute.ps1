param (
    [Parameter(Mandatory=$true)]
    [hashtable]$Config
)

function Write-ColorOutput($ForegroundColor) {
    $fc = $host.UI.RawUI.ForegroundColor
    $host.UI.RawUI.ForegroundColor = $ForegroundColor
    if ($args) {
        Write-Output $args
    }
    $host.UI.RawUI.ForegroundColor = $fc
}

function Check-Prerequisites {
    Write-ColorOutput Yellow "`nChecking prerequisites..."

    # Check if env-file exists
    if (-not (Test-Path $Config.EnvFile)) {
        Write-ColorOutput Red "ERROR: $($Config.EnvFile) file not found!"
        exit 1
    }

    # Check if Docker is running
    try {
        docker info | Out-Null
    }
    catch {
        Write-ColorOutput Red "ERROR: Docker is not running!"
        exit 1
    }
}


function Wait-ForContainers {
    param (
        [int]$WaitTimeInSeconds,
        [int]$TimeoutInSeconds,
        [string[]]$ContainerNames
    )

    if ($ContainerNames -like "all.*") {
        # Split the string at "." and take the second part
        $allContainers = ($ContainerNames -split '\.')[1]
        $ContainerNames = docker ps -a --format '{{.Names}}' | Where-Object { $_ -like "$allContainers" }
        if (-not $ContainerNames) {
            return @()
        }
    }

    $elapsed = 0
    $uptimeSeconds = 0

    while ($elapsed -lt $TimeoutInSeconds) {
        $allReady = $true

        foreach ($name in $ContainerNames) {
            $inspect = docker inspect --format='{{json .State}}' $name 2>$null | ConvertFrom-Json
            $status = $inspect.Status

            if ($status -ne 'running') {
                $uptimeSeconds = 0
                Write-Host " > [$name] -> $status. Uptime: $uptimeSeconds/$WaitTimeInSeconds sec., Try: $($elapsed + 1)/$TimeoutInSeconds"
                $allReady = $false
                break
            }
            else {
                if ($uptimeSeconds -lt $WaitTimeInSeconds) {
                    $allReady = $false
                    break
                }
            }
        }

        $elapsed += 1
        $uptimeSeconds +=1

        if ($allReady) {
            return $ContainerNames
        }
        else {
            if ($elapsed -lt $TimeoutInSeconds) {
                Start-Sleep -Seconds 1
            }
        }
    }

    throw [System.TimeoutException]::new("Aborted: Max wait time of $TimeoutInSeconds seconds exceeded!")
}


function Show-DockerConfiguration {
    Write-ColorOutput Yellow "`nAnalyzing Docker Configuration..."
    Write-Host "----------------------------------------"

    Write-ColorOutput Cyan "Docker compose configuration for service [$($Config.ContainerName)]:"
    try {
        $composeCmd = "docker compose --progress plain -f docker-compose.yml --env-file $($Config.EnvFile) config"
        Write-Host "Executing: $composeCmd"
        Write-Host "----------------------------------------"
        Invoke-Expression $composeCmd
        Write-Host "----------------------------------------"
    }
    catch {
        Write-ColorOutput Red "Error reading docker-compose configuration: $($_.Exception.Message)"
    }

    Write-ColorOutput Cyan "`nEnvironment variables of $($Config.EnvFile):"
    Write-Host "----------------------------------------"
    try {
        Get-Content $Config.EnvFile | ForEach-Object {
            if ($_ -match '^([^#].+)=(.*)$') {
                Write-Host "$($matches[1]) = $($matches[2])"
            }
        }
    }
    catch {
        Write-ColorOutput Red "Failed to read env file: $($_.Exception.Message)"
    }
}

function Build-DockerImages {
    Write-ColorOutput Green "`nBuild Docker Images..."
    try {
        $envContent = Get-Content $Config.EnvFile
        foreach ($line in $envContent) {
            if ($line.Trim() -and !$line.StartsWith("#")) {
                $key, $value = $line.Split('=', 2)
                [Environment]::SetEnvironmentVariable($key.Trim(), $value.Trim())
            }
        }

        foreach ($requiredVar in $Config.RequiredVars) {
            if (-not (Get-Item "env:$requiredVar" -ErrorAction SilentlyContinue)) {
                Write-ColorOutput Red "ERROR: $requiredVar is not set in $($Config.EnvFile)"
                exit 1
            }
        }

        Write-Host "Building images with docker-compose..."
        $buildCommand = "docker compose --progress plain -f docker-compose.yml --profile $($Config.Profile) --env-file $($Config.EnvFile) build --no-cache"
        Write-Host "Executing: $buildCommand"
        Invoke-Expression $buildCommand

        Write-ColorOutput Green "Docker images built successfully!"
    }
    catch {
        Write-ColorOutput Red "`nERROR: Failed to build Docker images!"
        Write-ColorOutput Red $_.Exception.Message
        exit 1
    }
}


function Start-DockerContainers {
    Write-ColorOutput Green "`nStarting Docker Containers..."
    try {
        $startCommand = "docker compose --progress plain -f docker-compose.yml --profile $($Config.Profile) --env-file $($Config.EnvFile) up -d"
        Write-Host "Executing: $startCommand"
        Invoke-Expression $startCommand

        if ($LASTEXITCODE -ne 0) {
            throw "Docker compose up returns error with exit code $LASTEXITCODE"
        }

        $validatedContainers = Wait-ForContainers -WaitTimeInSeconds $Config.WaitTimeInSeconds -TimeoutInSeconds $Config.TimeoutInSeconds -ContainerNames $Config.ContainerName

        Write-ColorOutput Cyan "`nContainer Status:"
        Write-Host "----------------------------------------"
        docker compose --profile $($Config.Profile) --env-file $($Config.EnvFile) ps

        Write-ColorOutput Green "`nDocker Containers started successfully!"
        Write-ColorOutput Green "Checking containers:"

        foreach ($container in $validatedContainers) {
            Write-ColorOutput Green "- $container"
        }

        Write-ColorOutput Green "`nPlease wait for all containers completing their startup process!"
    }
    catch {
        Write-ColorOutput Red "ERROR starting container(s)!"
        Write-ColorOutput Red $_.Exception.Message
        exit 1
    }
}


# Main
try {
    Write-ColorOutput Green "Starting Docker Deployment Process"

    Write-Host "----------------------------------------`n"
    Write-ColorOutput Cyan "Configuration (from filename):"
    Write-Host "EnvFile: $($Config.EnvFile)"
    Write-Host "Profile: $($Config.Profile)"

    $requiredConfigKeys = @(
        'EnvFile', 'Profile', 'WaitTimeInSeconds', 'TimeoutInSeconds', 'RequiredVars'
    )

    foreach ($key in $requiredConfigKeys) {
        if (-not $Config.ContainsKey($key)) {
            throw "Missing configuration key: $key"
        }
    }

    $env:COMPOSE_PARALLEL_LIMIT=3

    Check-Prerequisites
    Show-DockerConfiguration
    Build-DockerImages
    Start-DockerContainers
}
catch {
    Write-ColorOutput Red "`nERROR: Unexpected error(s) encountered!"
    Write-ColorOutput Red $_.Exception.Message
    exit 1
}
finally {
    Write-Host "`nFinished starting docker containers..."
}
