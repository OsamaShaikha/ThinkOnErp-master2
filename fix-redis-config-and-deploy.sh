#!/bin/bash

# Fix Redis Configuration and Deploy Script
# This script fixes the RedisConnectionString validation issue and rebuilds the Docker image

echo "=========================================="
echo "Fix Redis Configuration and Deploy"
echo "=========================================="
echo ""

# Step 1: Stop the running container
echo "Step 1: Stopping running container..."
docker-compose -f docker-compose.simple.yml down
echo "✓ Container stopped"
echo ""

# Step 2: Remove old image
echo "Step 2: Removing old Docker image..."
docker rmi thinkonerp-thinkonerp-api:latest 2>/dev/null || echo "No old image to remove"
echo "✓ Old image removed"
echo ""

# Step 3: Rebuild the image with no cache
echo "Step 3: Rebuilding Docker image (this may take a few minutes)..."
docker-compose -f docker-compose.simple.yml build --no-cache thinkonerp-api
if [ $? -ne 0 ]; then
    echo "❌ Docker build failed!"
    exit 1
fi
echo "✓ Docker image rebuilt successfully"
echo ""

# Step 4: Start the container
echo "Step 4: Starting container..."
docker-compose -f docker-compose.simple.yml up -d thinkonerp-api
if [ $? -ne 0 ]; then
    echo "❌ Failed to start container!"
    exit 1
fi
echo "✓ Container started"
echo ""

# Step 5: Wait a few seconds for startup
echo "Step 5: Waiting for application to start..."
sleep 5
echo ""

# Step 6: Check logs
echo "Step 6: Checking application logs..."
echo "=========================================="
docker logs thinkonerp-thinkonerp-api --tail 50
echo "=========================================="
echo ""

# Step 7: Check container status
echo "Step 7: Container status:"
docker ps -a | grep thinkonerp-api
echo ""

echo "=========================================="
echo "Deployment Complete!"
echo "=========================================="
echo ""
echo "To view live logs, run:"
echo "  docker logs -f thinkonerp-thinkonerp-api"
echo ""
echo "To check if API is responding:"
echo "  curl http://localhost:5000/health"
echo ""
