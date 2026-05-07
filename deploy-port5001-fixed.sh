#!/bin/bash

# Deploy ThinkOnErp API to Port 5001 (Fixed Version)
# This script uploads the fixed files and rebuilds the Docker image

set -e  # Exit on any error

SERVER_IP="178.104.126.99"
SERVER_USER="root"
SERVER_PASSWORD="ThinkOnErp!@123"
REMOTE_DIR="/root/thinkonerp-port5001"

echo "=========================================="
echo "ThinkOnErp Port 5001 Deployment (Fixed)"
echo "=========================================="
echo ""
echo "Server: $SERVER_IP"
echo "Remote Directory: $REMOTE_DIR"
echo ""

# Check if sshpass is installed
if ! command -v sshpass &> /dev/null; then
    echo "Error: sshpass is not installed."
    echo "Install it with: sudo apt-get install sshpass (Ubuntu/Debian)"
    echo "Or: brew install hudochenkov/sshpass/sshpass (macOS)"
    exit 1
fi

echo "Step 1: Creating remote directory..."
sshpass -p "$SERVER_PASSWORD" ssh -o StrictHostKeyChecking=no $SERVER_USER@$SERVER_IP "mkdir -p $REMOTE_DIR"

echo ""
echo "Step 2: Uploading project files..."
echo "This may take a few minutes..."

# Create a temporary directory for files to upload
TEMP_DIR=$(mktemp -d)
echo "Using temporary directory: $TEMP_DIR"

# Copy necessary files
cp -r src "$TEMP_DIR/"
cp ThinkOnErp.sln "$TEMP_DIR/"
cp Dockerfile.port5001 "$TEMP_DIR/"
cp docker-compose.port5001.yml "$TEMP_DIR/"
cp .env.port5001 "$TEMP_DIR/"
cp .dockerignore "$TEMP_DIR/" 2>/dev/null || true

# Upload files using rsync over SSH
sshpass -p "$SERVER_PASSWORD" rsync -avz --progress \
    -e "ssh -o StrictHostKeyChecking=no" \
    "$TEMP_DIR/" \
    $SERVER_USER@$SERVER_IP:$REMOTE_DIR/

# Clean up temporary directory
rm -rf "$TEMP_DIR"

echo ""
echo "Step 3: Stopping existing container (if running)..."
sshpass -p "$SERVER_PASSWORD" ssh -o StrictHostKeyChecking=no $SERVER_USER@$SERVER_IP << 'ENDSSH'
cd /root/thinkonerp-port5001
docker stop thinkonerp-api-5001 2>/dev/null || true
docker rm thinkonerp-api-5001 2>/dev/null || true
ENDSSH

echo ""
echo "Step 4: Building Docker image..."
sshpass -p "$SERVER_PASSWORD" ssh -o StrictHostKeyChecking=no $SERVER_USER@$SERVER_IP << 'ENDSSH'
cd /root/thinkonerp-port5001
docker build -f Dockerfile.port5001 -t thinkonerp-api:port5001 .
ENDSSH

echo ""
echo "Step 5: Starting container on port 5001..."
sshpass -p "$SERVER_PASSWORD" ssh -o StrictHostKeyChecking=no $SERVER_USER@$SERVER_IP << 'ENDSSH'
cd /root/thinkonerp-port5001
docker run -d \
    --name thinkonerp-api-5001 \
    --restart unless-stopped \
    -p 5001:5001 \
    --env-file .env.port5001 \
    thinkonerp-api:port5001
ENDSSH

echo ""
echo "Step 6: Waiting for application to start..."
sleep 10

echo ""
echo "Step 7: Checking container status..."
sshpass -p "$SERVER_PASSWORD" ssh -o StrictHostKeyChecking=no $SERVER_USER@$SERVER_IP << 'ENDSSH'
echo "Container Status:"
docker ps -a | grep thinkonerp-api-5001 || echo "Container not found!"
echo ""
echo "Recent Logs:"
docker logs --tail 50 thinkonerp-api-5001
ENDSSH

echo ""
echo "=========================================="
echo "Deployment Complete!"
echo "=========================================="
echo ""
echo "Access your API at:"
echo "  Swagger UI: http://$SERVER_IP:5001/swagger"
echo "  Health Check: http://$SERVER_IP:5001/health"
echo ""
echo "To view logs:"
echo "  ssh root@$SERVER_IP"
echo "  docker logs -f thinkonerp-api-5001"
echo ""
echo "To restart:"
echo "  ssh root@$SERVER_IP"
echo "  docker restart thinkonerp-api-5001"
echo ""
