#!/bin/bash

# Fix Docker Container Name Conflict
# This script removes the old container and starts fresh

echo "==================================="
echo "Fix Docker Container Name Conflict"
echo "==================================="
echo ""

# Stop and remove the conflicting container
echo "Step 1: Stopping and removing old container..."
docker stop thinkonerp-api 2>/dev/null || true
docker rm thinkonerp-api 2>/dev/null || true
docker rm f4978313ec094640addbdc0ee5ed0fb95e3e56db78521e438ac24a384546ba3e 2>/dev/null || true

echo "✓ Old container removed"
echo ""

# Clean up any dangling containers
echo "Step 2: Cleaning up dangling containers..."
docker container prune -f

echo "✓ Cleanup complete"
echo ""

# Start the application with docker-compose
echo "Step 3: Starting application with docker-compose..."
docker-compose -f docker-compose.simple.yml up -d

echo ""
echo "==================================="
echo "Done! Check status with:"
echo "  docker ps"
echo "  docker logs thinkonerp-api"
echo "==================================="
