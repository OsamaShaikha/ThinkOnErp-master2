#!/bin/bash

# ThinkOnErp API - Deploy on Port 5001
# This script deploys the ThinkOnErp API on port 5001

set -e

echo "=========================================="
echo "ThinkOnErp API - Port 5001 Deployment"
echo "=========================================="
echo ""

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Check if Docker is installed
if ! command -v docker &> /dev/null; then
    echo -e "${RED}Error: Docker is not installed${NC}"
    echo "Please install Docker first: https://docs.docker.com/get-docker/"
    exit 1
fi

# Check if Docker Compose is installed
if ! command -v docker-compose &> /dev/null; then
    echo -e "${RED}Error: Docker Compose is not installed${NC}"
    echo "Please install Docker Compose first: https://docs.docker.com/compose/install/"
    exit 1
fi

# Check if .env.port5001 exists
if [ ! -f .env.port5001 ]; then
    echo -e "${RED}Error: .env.port5001 file not found${NC}"
    echo "Please create .env.port5001 with your configuration"
    exit 1
fi

# Copy environment file
echo -e "${YELLOW}Copying environment configuration...${NC}"
cp .env.port5001 .env.production

# Stop existing container if running
echo -e "${YELLOW}Stopping existing container (if any)...${NC}"
docker-compose -f docker-compose.port5001.yml down 2>/dev/null || true

# Build the image
echo -e "${YELLOW}Building Docker image...${NC}"
docker-compose -f docker-compose.port5001.yml build --no-cache

# Start the container
echo -e "${YELLOW}Starting container on port 5001...${NC}"
docker-compose -f docker-compose.port5001.yml up -d

# Wait for container to be healthy
echo -e "${YELLOW}Waiting for API to be ready...${NC}"
sleep 10

# Check container status
if docker ps | grep -q thinkonerp-api-5001; then
    echo -e "${GREEN}✓ Container is running${NC}"
else
    echo -e "${RED}✗ Container failed to start${NC}"
    echo "Checking logs..."
    docker-compose -f docker-compose.port5001.yml logs
    exit 1
fi

# Test health endpoint
echo -e "${YELLOW}Testing health endpoint...${NC}"
if curl -f http://localhost:5001/health &> /dev/null; then
    echo -e "${GREEN}✓ Health check passed${NC}"
else
    echo -e "${RED}✗ Health check failed${NC}"
    echo "The API might still be starting up. Check logs with:"
    echo "docker-compose -f docker-compose.port5001.yml logs -f"
fi

echo ""
echo "=========================================="
echo -e "${GREEN}Deployment Complete!${NC}"
echo "=========================================="
echo ""
echo "API is running on port 5001:"
echo "  - Swagger UI: http://localhost:5001/swagger"
echo "  - Health Check: http://localhost:5001/health"
echo "  - Base URL: http://localhost:5001"
echo ""
echo "Useful commands:"
echo "  - View logs: docker-compose -f docker-compose.port5001.yml logs -f"
echo "  - Stop: docker-compose -f docker-compose.port5001.yml down"
echo "  - Restart: docker-compose -f docker-compose.port5001.yml restart"
echo "  - Status: docker-compose -f docker-compose.port5001.yml ps"
echo ""
