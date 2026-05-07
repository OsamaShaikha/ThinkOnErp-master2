#!/bin/bash

# Fix and Restart ThinkOnErp API
# This script rebuilds the Docker image and restarts the container with the fixed configuration

echo "========================================="
echo "ThinkOnErp API - Fix and Restart"
echo "========================================="
echo ""

# Stop and remove existing containers
echo "Step 1: Stopping existing containers..."
docker-compose down
echo "✓ Containers stopped"
echo ""

# Remove old images to force rebuild
echo "Step 2: Removing old images..."
docker rmi thinkonerp-thinkonerp-api 2>/dev/null || true
echo "✓ Old images removed"
echo ""

# Rebuild the image
echo "Step 3: Rebuilding Docker image..."
docker-compose build --no-cache thinkonerp-api
if [ $? -ne 0 ]; then
    echo "✗ Build failed!"
    exit 1
fi
echo "✓ Image rebuilt successfully"
echo ""

# Start the containers
echo "Step 4: Starting containers..."
docker-compose up -d thinkonerp-api
if [ $? -ne 0 ]; then
    echo "✗ Failed to start containers!"
    exit 1
fi
echo "✓ Containers started"
echo ""

# Wait for container to be ready
echo "Step 5: Waiting for application to start..."
sleep 5
echo ""

# Show logs
echo "Step 6: Showing application logs..."
echo "========================================="
docker logs --tail=50 -f thinkonerp-api
