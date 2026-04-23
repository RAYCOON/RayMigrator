#!/bin/bash
# run-docker-all.sh - Start all example Docker containers (SqlServer + PostgreSQL)

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

ENV_FILE="default.env"
PROFILE="all"
WAIT_TIME=3
TIMEOUT=20

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m'

echo -e "${GREEN}Starting Docker Deployment Process${NC}"
echo "----------------------------------------"
echo -e "${CYAN}Configuration:${NC}"
echo "EnvFile: $ENV_FILE"
echo "Profile: $PROFILE"

# Check prerequisites
echo -e "${YELLOW}\nChecking prerequisites...${NC}"

if [ ! -f "$ENV_FILE" ]; then
    echo -e "${RED}ERROR: $ENV_FILE file not found!${NC}"
    exit 1
fi

if ! docker info > /dev/null 2>&1; then
    echo -e "${RED}ERROR: Docker is not running!${NC}"
    exit 1
fi

# Show configuration
echo -e "${YELLOW}\nAnalyzing Docker Configuration...${NC}"
echo "----------------------------------------"
echo -e "${CYAN}Environment variables of $ENV_FILE:${NC}"
echo "----------------------------------------"
grep -v '^#' "$ENV_FILE" | grep -v '^$' | while IFS='=' read -r key value; do
    echo "$key = $value"
done

# Build images
echo -e "${GREEN}\nBuild Docker Images...${NC}"
echo "Building images with docker-compose..."
COMPOSE_PARALLEL_LIMIT=3 docker compose --progress plain -f docker-compose.yml --profile "$PROFILE" --env-file "$ENV_FILE" build --no-cache
echo -e "${GREEN}Docker images built successfully!${NC}"

# Start containers
echo -e "${GREEN}\nStarting Docker Containers...${NC}"
COMPOSE_PARALLEL_LIMIT=3 docker compose --progress plain -f docker-compose.yml --profile "$PROFILE" --env-file "$ENV_FILE" up -d

# Wait for containers
echo -e "${YELLOW}\nWaiting for containers to be ready...${NC}"
elapsed=0
while [ $elapsed -lt $TIMEOUT ]; do
    all_running=true
    found_any=false
    for container in $(docker ps -a --format '{{.Names}}' | grep "rm_exampleDB_" || true); do
        found_any=true
        status=$(docker inspect --format='{{.State.Status}}' "$container" 2>/dev/null || echo "not found")
        if [ "$status" != "running" ]; then
            echo " > [$container] -> $status. Try: $((elapsed + 1))/$TIMEOUT"
            all_running=false
            break
        fi
    done

    if [ "$found_any" = false ]; then
        all_running=false
    fi

    if [ "$all_running" = true ] && [ $elapsed -ge $WAIT_TIME ]; then
        break
    fi

    elapsed=$((elapsed + 1))
    if [ $elapsed -lt $TIMEOUT ]; then
        sleep 1
    fi
done

if [ $elapsed -ge $TIMEOUT ]; then
    echo -e "${RED}Aborted: Max wait time of $TIMEOUT seconds exceeded!${NC}"
    exit 1
fi

# Show status
echo -e "${CYAN}\nContainer Status:${NC}"
echo "----------------------------------------"
docker compose --profile "$PROFILE" --env-file "$ENV_FILE" ps

echo -e "${GREEN}\nDocker Containers started successfully!${NC}"
echo -e "${GREEN}Please wait for all containers completing their startup process!${NC}"
