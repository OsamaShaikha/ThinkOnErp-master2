#!/bin/bash

echo "=========================================="
echo "Force Rebuild Docker Container"
echo "=========================================="
echo ""

# Stop and remove container
echo "Step 1: Stopping and removing container..."
docker-compose -f docker-compose.simple.yml down
docker rm -f thinkonerp-api 2>/dev/null || true
echo "✅ Container removed"
echo ""

# Remove old image
echo "Step 2: Removing old Docker image..."
docker rmi thinkonerp_thinkonerp-api:latest 2>/dev/null || true
echo "✅ Old image removed"
echo ""

# Rebuild with no cache
echo "Step 3: Rebuilding image (this will take a few minutes)..."
docker-compose -f docker-compose.simple.yml build --no-cache
echo "✅ Image rebuilt"
echo ""

# Start container
echo "Step 4: Starting container..."
docker-compose -f docker-compose.simple.yml up -d
echo "✅ Container started"
echo ""

# Wait for startup
echo "Step 5: Waiting 15 seconds for application to start..."
sleep 15
echo ""

# Check status
echo "Step 6: Checking container status..."
docker ps | grep thinkonerp-api
echo ""

# Check logs
echo "Step 7: Checking logs (last 50 lines)..."
docker logs --tail 50 thinkonerp-api
echo ""

echo "=========================================="
echo "Rebuild Complete!"
echo "=========================================="
