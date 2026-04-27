#!/bin/bash

echo "=========================================="
echo "Rebuilding and Restarting Container"
echo "=========================================="
echo ""

# Step 1: Stop existing container
echo "Step 1: Stopping existing container..."
docker-compose -f docker-compose.simple.yml down
echo "✅ Container stopped"
echo ""

# Step 2: Rebuild image
echo "Step 2: Rebuilding Docker image..."
docker-compose -f docker-compose.simple.yml build --no-cache
echo "✅ Image rebuilt"
echo ""

# Step 3: Start container
echo "Step 3: Starting container..."
docker-compose -f docker-compose.simple.yml up -d
echo "✅ Container started"
echo ""

# Step 4: Wait for startup
echo "Step 4: Waiting 15 seconds for application to start..."
sleep 15
echo ""

# Step 5: Check status
echo "Step 5: Checking container status..."
docker ps | grep thinkonerp-api
echo ""

# Step 6: Check logs
echo "Step 6: Checking logs (last 30 lines)..."
docker logs --tail 30 thinkonerp-api
echo ""

# Step 7: Test connection
echo "Step 7: Testing connection..."
curl -s http://localhost:5000/health || echo "❌ Connection failed"
echo ""

echo "=========================================="
echo "Complete!"
echo "=========================================="
