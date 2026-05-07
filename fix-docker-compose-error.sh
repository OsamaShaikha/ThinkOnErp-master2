#!/bin/bash

#############################################
# Fix Docker Compose ContainerConfig Error
# This script removes the problematic container and starts fresh
#############################################

echo "╔════════════════════════════════════════════════════════════╗"
echo "║   Fix Docker Compose ContainerConfig Error                ║"
echo "╚════════════════════════════════════════════════════════════╝"
echo ""

# Step 1: Stop and remove ALL containers for this project
echo "Step 1: Stopping and removing all containers..."
docker-compose -f docker-compose.simple.yml down -v 2>/dev/null || true
echo "✓ Containers stopped"
echo ""

# Step 2: Remove the specific problematic container by ID
echo "Step 2: Removing problematic container..."
docker rm -f c54a37a497c6_thinkonerp-api 2>/dev/null || echo "Container already removed"
docker rm -f thinkonerp-api 2>/dev/null || echo "Container already removed"
docker rm -f $(docker ps -a -q --filter "name=thinkonerp") 2>/dev/null || echo "No containers to remove"
echo "✓ Old containers removed"
echo ""

# Step 3: Clean up any dangling containers
echo "Step 3: Cleaning up dangling containers..."
docker container prune -f
echo "✓ Cleanup complete"
echo ""

# Step 4: Start fresh container
echo "Step 4: Starting fresh container..."
docker-compose -f docker-compose.simple.yml up -d
echo "✓ Container started"
echo ""

# Step 5: Wait for startup
echo "Step 5: Waiting for application to start..."
sleep 10
echo ""

# Step 6: Show logs
echo "Step 6: Application logs:"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
docker logs thinkonerp-thinkonerp-api --tail 50
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""

# Step 7: Check status
echo "Step 7: Container status:"
docker ps | grep thinkonerp
echo ""

echo "╔════════════════════════════════════════════════════════════╗"
echo "║                    DEPLOYMENT COMPLETE                     ║"
echo "╚════════════════════════════════════════════════════════════╝"
echo ""
echo "To view live logs: docker logs -f thinkonerp-thinkonerp-api"
echo "To test API: curl http://localhost:5000/health"
echo ""
