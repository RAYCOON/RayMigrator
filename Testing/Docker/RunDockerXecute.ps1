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
    
    # Überprüfe ob env-file existiert
    if (-not (Test-Path $Config.EnvFile)) {
        Write-ColorOutput Red "ERROR: $($Config.EnvFile) file not found!"
        # pause
        exit 1
    }
    
    # Überprüfe ob Docker läuft
    try {
        docker info | Out-Null
    }
    catch {
        Write-ColorOutput Red "ERROR: Docker is not running!"
        # pause
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
        # Splitte den String bei "." und nimm den zweiten Teil
        $allContainers = ($ContainerNames -split '\.')[1]
        $ContainerNames = docker ps -a --format '{{.Names}}' | Where-Object { $_ -like "$allContainers" }
        if (-not $ContainerNames) {
            return @()  # Return empty array if no matching containers found
        }
    }    
    
    $elapsed = 0
	$uptimeSeconds = 0

    while ($elapsed -lt $TimeoutInSeconds) {
        $allReady = $true

        foreach ($name in $ContainerNames) {
            # Retrieve container state information
            $inspect = docker inspect --format='{{json .State}}' $name 2>$null | ConvertFrom-Json
            $status = $inspect.Status

            if ($status -ne 'running') {
                # Output negative status
				$uptimeSeconds = 0
                Write-Host " > [$name] -> $status. Uptime: $uptimeSeconds/$WaitTimeInSeconds sek., Try: $($elapsed + 1)/$TimeoutInSeconds"
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
            return $ContainerNames  # Success, return container names
        }
        else {
            if ($elapsed -lt $TimeoutInSeconds) {
                Start-Sleep -Seconds 1
            }
        }
    }

    # Für allgemeine Timeouts
    throw [System.TimeoutException]::new("Aborted: Max wait time of $TimeoutInSeconds seconds exceeded!")
}


function Show-DockerConfiguration {
    Write-ColorOutput Yellow "`nAnalyzing Docker Configuration..."
    Write-Host "----------------------------------------"
    
    # 1. Docker Compose Config
    Write-ColorOutput Cyan "Docker compose configuration for service [$($Config.ContainerName)]:"
    try {
        $composeCmd = "docker compose --progress plain -f docker-compose.yml --env-file $($Config.EnvFile) config $($Config.ContainerName)"
        Write-Host "Executing: $composeCmd"
        Write-Host "----------------------------------------"
        Invoke-Expression $composeCmd
        Write-Host "----------------------------------------"
    }
    catch {
        Write-ColorOutput Red "Error reading docker-compose configuration: $($_.Exception.Message)"
    }
	
	try {
		$output = docker compose -f docker-compose.yml --env-file $($Config.EnvFile) config --quiet
    
		# Ausgabe analysieren
		if ($output -match "ERROR:") {
			Write-Error "`nConfiguration error found"
		} else {
			Write-Host "`nConfiguration is valid"
			$output
		}
	}
	catch {
		Write-Error "`nError validating configuration: $_"
	}

    # 2. Environment Variables from env-file
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
    
    # 3. Dockerfile ARG und ENV Analyse
    # Write-ColorOutput Cyan "`nDockerfile ARG und ENV Analyse:"
    # Write-Host "----------------------------------------"
    # try {
        # $dockerfile = Get-Content "$PSScriptRoot/SqlServer/Dockerfile" -ErrorAction Stop
        # $dockerfile | Select-String -Pattern '^(ARG|ENV) ' | ForEach-Object {
            # Write-Host $_
        # }
    # }
    # catch {
        # Write-ColorOutput Red "Dockerfile konnte nicht analysiert werden: $($_.Exception.Message)"
    # }
}

function Build-DockerImages {
    Write-ColorOutput Green "`n'Build Docker Images..."
    try {
        # Lade Umgebungsvariablen aus env-file
        $envContent = Get-Content $Config.EnvFile
        foreach ($line in $envContent) {
            if ($line.Trim() -and !$line.StartsWith("#")) {
                $key, $value = $line.Split('=', 2)
                [Environment]::SetEnvironmentVariable($key.Trim(), $value.Trim())
            }
        }
        
        # Überprüfe kritische Umgebungsvariablen
        foreach ($requiredVar in $Config.RequiredVars) {
            if (-not (Get-Item "env:$requiredVar" -ErrorAction SilentlyContinue)) {
                Write-ColorOutput Red "ERROR: $requiredVar is not set in $($Config.EnvFile)"
                # pause
                exit 1
            }
        }
        
        # Build der Docker Images mit Fortschrittsanzeige
        Write-Host "Building images with docker-compose..."
        $buildCommand = "docker compose --progress plain -f docker-compose.yml --env-file $($Config.EnvFile) build --no-cache"
        Write-Host "Executing: $buildCommand"
        Invoke-Expression $buildCommand
        
        Write-ColorOutput Green "Docker images built successfully!"
    }
    catch {
        Write-ColorOutput Red "`nERROR: Failed to build Docker images!"
        Write-ColorOutput Red $_.Exception.Message
        # pause
        exit 1
    }
}


function Start-DockerContainers {
    Write-ColorOutput Green "`nStarting Docker Container..."
    try {
        # Start der Container mit Profil
        $startCommand = "docker compose --progress plain -f docker-compose.yml --profile $($Config.Profile) --env-file $($Config.EnvFile) up -d"
        Write-Host "Executing: $startCommand"
        Invoke-Expression $startCommand
        
        # Überprüfe den Exit-Code
        if ($LASTEXITCODE -ne 0) {
            throw "Docker compose up returns error with exit code $LASTEXITCODE"
        }
        
        # Warte, bis die Container bereit sind, und speichere die validierten Container-Namen
        $validatedContainers = Wait-ForContainers -WaitTimeInSeconds $Config.WaitTimeInSeconds -TimeoutInSeconds $Config.TimeoutInSeconds -ContainerNames $Config.ContainerName
        
        # Zeige Container Status
        Write-ColorOutput Cyan "`nContainer Status:"
        Write-Host "----------------------------------------"
        docker compose ps
        
        Write-ColorOutput Green "`nDocker Container started successfully!"
        Write-ColorOutput Green "Checking containers:"
        
        # Gebe die Container-Namen sauber zeilenweise aus
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


# Hauptprogramm
try {
    Write-ColorOutput Green "Starting Docker Deployment Process"
    
	Write-Host "----------------------------------------`n"
	# Optional: Ausgabe zur Überprüfung
	Write-ColorOutput Cyan "Check Konfiguration (from filename):"
	Write-Host "EnvFile: $($CONFIG.EnvFile)"
	Write-Host "Profile: $profile"

    # Validiere übergebene Konfiguration
    $requiredConfigKeys = @(
        'EnvFile', 'Profile', 'WaitTimeInSeconds', 'TimeoutInSeconds', 'RequiredVars'
    )
    
    foreach ($key in $requiredConfigKeys) {
        if (-not $Config.ContainsKey($key)) {
            throw "Fehlender Configuration-Key: $key"
        }
    }
    
	# Legt die maximale Anzahl der docker-compose Dienste (up, down, build, pull, push) fest, die gleichzeitig während bestimmter Operationen ausgeführt werden können.
	$env:COMPOSE_PARALLEL_LIMIT=3
	
    Check-Prerequisites
    Show-DockerConfiguration
    Build-DockerImages
    Start-DockerContainers
}
catch {
    Write-ColorOutput Red "`nERROR: Unexpected error(s) encountered!"
    Write-ColorOutput Red $_.Exception.Message
    # pause
    exit 1
}
finally {
    Write-Host "`nFinished starting docker containers..."
    # $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
}
