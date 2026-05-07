#!/bin/bash

# ThinkOnErp API - Deploy to Server 178.104.126.99 on Port 5001
# This script uploads files and deploys the API on the remote server

set -e

echo "=========================================="
echo "ThinkOnErp API - Server Deployment (Port 5001)"
echo "Server: 178.104.126.99"
echo "Port: 5001"
echo "=========================================="
echo ""

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Server details
SERVER_IP="178.104.126.99"
SERVER_USER="root"
SERVER_PASSWORD="ThinkOnErp!@123"
SERVER_PATH="~/ThinkOnErp"
CONTAINER_NAME="thinkonerp-api-5001"

echo -e "${BLUE}Step 1: Uploading files to server...${NC}"
echo ""

# Upload necessary files
echo -e "${YELLOW}Uploading Dockerfile.port5001...${NC}"
scp Dockerfile.port5001 ${SERVER_USER}@${SERVER_IP}:${SERVER_PATH}/

echo -e "${YELLOW}Uploading docker-compose.port5001.yml...${NC}"
scp docker-compose.port5001.yml ${SERVER_USER}@${SERVER_IP}:${SERVER_PATH}/

echo -e "${YELLOW}Uploading .env.port5001...${NC}"
scp .env.port5001 ${SERVER_USER}@${SERVER_IP}:${SERVER_PATH}/.env.production

echo -e "${GREEN}✓ Files uploaded successfully${NC}"
echo ""

echo -e "${BLUE}Step 2: Deploying on server...${NC}"
echo ""

# Execute deployment commands on server
ssh ${SERVER_USER}@${SERVER_IP} << 'ENDSSH'
cd ~/ThinkOnErp

echo "Stopping existing container (if any)..."
docker-compose -f docker-compose.port5001.yml down 2>/dev/null || true

echo "Building Docker image..."
docker-compose -f docker-compose.port5001.yml build --no-cache

echo "Starting container on port 5001..."
docker-compose -f docker-compose.port5001.yml up -d

echo "Waiting for API to be ready..."
sleep 15

# Check container status
if docker ps | grep -q thinkonerp-api-5001; then
    echo "✓ Container is running"
else
    echo "✗ Container failed to start"
    echo "Checking logs..."
    docker-compose -f docker-compose.port5001.yml logs --tail=50
    exit 1
fi

# Test health endpoint
echo "Testing health endpoint..."
if curl -f http://localhost:5001/health &> /dev/null; then
    echo "✓ Health check passed"
else
    echo "⚠ Health check failed (API might still be starting)"
fi

echo ""
echo "=========================================="
echo "Deployment Complete!"
echo "=========================================="
echo ""
echo "API is running on:"
echo "  - Swagger UI: http://178.104.126.99:5001/swagger"
echo "  - Health Check: http://178.104.126.99:5001/health"
echo "  - Base URL: http://178.104.126.99:5001"
echo ""
echo "Container name: thinkonerp-api-5001"
echo ""
echo "Useful commands:"
echo "  - View logs: docker-compose -f docker-compose.port5001.yml logs -f"
echo "  - Stop: docker-compose -f docker-compose.port5001.yml down"
echo "  - Restart: docker-compose -f docker-compose.port5001.yml restart"
echo "  - Status: docker-compose -f docker-compose.port5001.yml ps"
echo ""
ENDSSH

echo ""
echo -e "${GREEN}=========================================="
echo "Deployment to server completed!"
echo "==========================================${NC}"
echo ""
echo -e "${BLUE}Access your API at:${NC}"
echo "  🌐 Swagger UI: http://178.104.126.99:5001/swagger"
echo "  ❤️  Health Check: http://178.104.126.99:5001/health"
echo ""
echo -e "${YELLOW}Test credentials:${NC}"
echo "  Username: superadmin"
echo "  Password: SuperAdmin123!"
echo ""
