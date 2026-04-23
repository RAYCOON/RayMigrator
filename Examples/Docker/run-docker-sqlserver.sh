#!/bin/bash
# run-docker-sqlserver.sh - Start SqlServer example Docker container

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

ENV_FILE="default.env"
PROFILE="sqlserver"
CONTAINER_NAME="rm_exampleDB_SqlServer"
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
echo "Container: $CONTAINER_NAME"

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

# Build images
echo -e "${GREEN}\nBuild Docker Images...${NC}"
COMPOSE_PARALLEL_LIMIT=3 docker compose --progress plain -f docker-compose.yml --profile "$PROFILE" --env-file "$ENV_FILE" build --no-cache
echo -e "${GREEN}Docker images built successfully!${NC}"

# Start container
echo -e "${GREEN}\nStarting Docker Container...${NC}"
COMPOSE_PARALLEL_LIMIT=3 docker compose --progress plain -f docker-compose.yml --profile "$PROFILE" --env-file "$ENV_FILE" up -d

# Wait for container
echo -e "${YELLOW}\nWaiting for container to be ready...${NC}"
elapsed=0
while [ $elapsed -lt $TIMEOUT ]; do
    status=$(docker inspect --format='{{.State.Status}}' "$CONTAINER_NAME" 2>/dev/null || echo "not found")
    if [ "$status" = "running" ] && [ $elapsed -ge $WAIT_TIME ]; then
        break
    fi
    if [ "$status" != "running" ]; then
        echo " > [$CONTAINER_NAME] -> $status. Try: $((elapsed + 1))/$TIMEOUT"
    fi
    elapsed=$((elapsed + 1))
    sleep 1
done

if [ $elapsed -ge $TIMEOUT ]; then
    echo -e "${RED}Aborted: Max wait time of $TIMEOUT seconds exceeded!${NC}"
    exit 1
fi

# Show status
echo -e "${CYAN}\nContainer Status:${NC}"
echo "----------------------------------------"
docker compose --profile "$PROFILE" --env-file "$ENV_FILE" ps

echo -e "${GREEN}\nDocker Container started successfully: $CONTAINER_NAME${NC}"
echo -e "${GREEN}Please wait for the container completing its startup process!${NC}"
