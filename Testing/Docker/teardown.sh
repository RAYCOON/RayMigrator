#!/bin/bash
# Stops all Docker containers, removes volumes and network
# Usage: ./teardown.sh

set -e

echo "Stopping and removing all RayMigrator Docker containers..."

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
cd "$SCRIPT_DIR"

docker-compose --env-file default.env --profile all down -v

echo "All containers stopped and volumes removed."
