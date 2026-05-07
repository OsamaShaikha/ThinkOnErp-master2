#!/bin/bash

echo "========================================="
echo "ThinkOnErp API - FINAL DEPLOYMENT"
echo "========================================="
echo ""
echo "All configuration errors have been fixed in appsettings.Production.json"
echo ""

# Stop containers
echo "Step 1: Stopping containers..."
docker-compose down
echo "✓ Done"
echo ""

# Remove old image
echo "Step 2: Removing old image..."
docker rmi thinkonerp-thinkonerp-api 2>/dev/null || echo "No old image to remove"
echo "✓ Done"
echo ""

# Rebuild
echo "Step 3: Rebuilding image with fixed configuration..."
docker-compose build --no-cache thinkonerp-api
if [ $? -ne 0 ]; then
    echo "✗ Build failed!"
    exit 1
fi
echo "✓ Done"
echo ""

# Start
echo "Step 4: Starting application..."
docker-compose up -d thinkonerp-api
if [ $? -ne 0 ]; then
    echo "✗ Start failed!"
    exit 1
fi
echo "✓ Done"
echo ""

# Wait
echo "Step 5: Waiting for startup..."
sleep 5
echo ""

# Show logs
echo "========================================="
echo "Application Logs (Ctrl+C to exit):"
echo "========================================="
docker logs -f thinkonerp-api
