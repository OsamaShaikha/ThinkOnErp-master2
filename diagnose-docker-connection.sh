#!/bin/bash

echo "=========================================="
echo "Docker Container Diagnostics"
echo "=========================================="
echo ""

echo "1. Checking if container is running..."
docker ps -a | grep thinkonerp-api
echo ""

echo "2. Checking container logs (last 50 lines)..."
docker logs --tail 50 thinkonerp-api
echo ""

echo "3. Checking port bindings..."
docker port thinkonerp-api
echo ""

echo "4. Checking if port is listening on host..."
netstat -tuln | grep -E ':(5000|8080)'
echo ""

echo "5. Testing connection from inside container..."
docker exec thinkonerp-api curl -s http://localhost:8080/health || echo "Failed to connect from inside container"
echo ""

echo "6. Checking firewall status..."
ufw status || echo "UFW not installed or not active"
echo ""

echo "7. Checking if port 5000 is accessible..."
curl -v http://localhost:5000/health 2>&1 | head -20
echo ""

echo "8. Checking environment variables in container..."
docker exec thinkonerp-api printenv | grep -E 'ASPNETCORE|ConnectionStrings|JWT'
echo ""

echo "=========================================="
echo "Diagnostics Complete"
echo "=========================================="
