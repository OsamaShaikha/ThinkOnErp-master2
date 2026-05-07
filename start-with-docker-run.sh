#!/bin/bash

#############################################
# Start ThinkOnErp API using Docker Run
# Bypasses docker-compose ContainerConfig bug
#############################################

echo "╔════════════════════════════════════════════════════════════╗"
echo "║   Start ThinkOnErp API (Direct Docker Run)                ║"
echo "╚════════════════════════════════════════════════════════════╝"
echo ""

# Step 1: Stop and remove ALL old containers
echo "Step 1: Removing all old containers..."
docker stop $(docker ps -a -q --filter "name=thinkonerp") 2>/dev/null || true
docker rm -f $(docker ps -a -q --filter "name=thinkonerp") 2>/dev/null || true
docker rm -f c54a37a497c6_thinkonerp-api 2>/dev/null || true
docker rm -f thinkonerp-api 2>/dev/null || true
docker rm -f thinkonerp-thinkonerp-api 2>/dev/null || true
echo "✓ Old containers removed"
echo ""

# Step 2: Run the container directly with docker run
echo "Step 2: Starting container with docker run..."
docker run -d \
  --name thinkonerp-api \
  --restart unless-stopped \
  -p 5000:8080 \
  --env-file .env.production \
  thinkonerp_thinkonerp-api:latest

if [ $? -eq 0 ]; then
    echo "✓ Container started successfully"
else
    echo "✗ Failed to start container"
    exit 1
fi
echo ""

# Step 3: Wait for startup
echo "Step 3: Waiting for application to start..."
sleep 10
echo ""

# Step 4: Show logs
echo "Step 4: Application logs:"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
docker logs thinkonerp-api --tail 50
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""

# Step 5: Check container status
echo "Step 5: Container status:"
docker ps | grep thinkonerp
echo ""

# Step 6: Check if application started successfully
echo "Step 6: Checking application status..."
if docker logs thinkonerp-api 2>&1 | grep -q "Application started"; then
    echo "✓ Application started successfully!"
    echo ""
    echo "╔════════════════════════════════════════════════════════════╗"
    echo "║                  DEPLOYMENT SUCCESSFUL!                    ║"
    echo "╚════════════════════════════════════════════════════════════╝"
    echo ""
    echo "🎉 API is running on: http://178.104.126.99:5000"
    echo "📖 Swagger UI: http://178.104.126.99:5000/swagger"
    echo "💚 Health Check: http://178.104.126.99:5000/health"
else
    echo "⚠ Application may not have started correctly"
    echo "Check logs above for errors"
fi
echo ""

echo "📋 Useful Commands:"
echo "   View live logs:    docker logs -f thinkonerp-api"
echo "   Stop container:    docker stop thinkonerp-api"
echo "   Start container:   docker start thinkonerp-api"
echo "   Restart:           docker restart thinkonerp-api"
echo "   Remove:            docker rm -f thinkonerp-api"
echo ""
echo "🧪 Test API:"
echo "   curl http://localhost:5000/health"
echo ""
