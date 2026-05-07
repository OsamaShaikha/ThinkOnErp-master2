#!/bin/bash

# Quick rebuild and restart for port 5001 after code changes

echo "=========================================="
echo "Rebuilding ThinkOnErp API (Port 5001)"
echo "=========================================="
echo ""

SERVER_IP="178.104.126.99"
SERVER_USER="root"

echo "Uploading updated files..."
scp src/ThinkOnErp.API/Program.cs ${SERVER_USER}@${SERVER_IP}:~/ThinkOnErp/src/ThinkOnErp.API/
scp src/ThinkOnErp.API/appsettings.Production.json ${SERVER_USER}@${SERVER_IP}:~/ThinkOnErp/src/ThinkOnErp.API/

echo ""
echo "Rebuilding on server..."

ssh ${SERVER_USER}@${SERVER_IP} << 'ENDSSH'
cd ~/ThinkOnErp

echo "Stopping container..."
docker-compose -f docker-compose.port5001.yml down

echo "Rebuilding image..."
docker-compose -f docker-compose.port5001.yml build --no-cache

echo "Starting container..."
docker-compose -f docker-compose.port5001.yml up -d

echo "Waiting for API..."
sleep 10

echo "Checking status..."
docker-compose -f docker-compose.port5001.yml ps

echo ""
echo "Testing health endpoint..."
curl -f http://localhost:5001/health && echo "✓ Health check passed" || echo "⚠ Health check failed"

echo ""
echo "=========================================="
echo "Rebuild Complete!"
echo "=========================================="
echo ""
echo "Access your API at:"
echo "  http://178.104.126.99:5001/swagger"
echo ""
ENDSSH

echo ""
echo "Done! Open http://178.104.126.99:5001/swagger in your browser"
