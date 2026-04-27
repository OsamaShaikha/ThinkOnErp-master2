#!/bin/bash

echo "=========================================="
echo "Quick Fix for Connection Issues"
echo "=========================================="
echo ""

# Step 1: Check if .env file exists
if [ ! -f .env ]; then
    echo "❌ .env file not found! Creating one..."
    cat > .env << 'EOF'
ORACLE_CONNECTION_STRING=Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=178.104.126.99)(PORT=1521))(CONNECT_DATA=(SERVICE_NAME=XEPDB1)));User Id=THINKON_ERP;Password=THINKON_ERP;
JWT_SECRET_KEY=A7fK9xP2LmQ8vR4TzW6nB1CjD5eH3YUs
JWT_ISSUER=ThinkOnErpAPI
JWT_AUDIENCE=ThinkOnErpClient
JWT_EXPIRY_MINUTES=60
API_PORT=5000
LOG_LEVEL=Information
EOF
    echo "✅ .env file created"
else
    echo "✅ .env file exists"
fi
echo ""

# Step 2: Stop existing container
echo "Stopping existing container..."
docker-compose -f docker-compose.simple.yml down
echo ""

# Step 3: Check firewall
echo "Checking firewall..."
if command -v ufw &> /dev/null; then
    echo "Opening port 5000 in firewall..."
    sudo ufw allow 5000/tcp
    echo "✅ Firewall rule added"
else
    echo "⚠️  UFW not found, skipping firewall configuration"
fi
echo ""

# Step 4: Start container
echo "Starting container..."
docker-compose -f docker-compose.simple.yml up -d
echo ""

# Step 5: Wait for container to start
echo "Waiting 10 seconds for container to start..."
sleep 10
echo ""

# Step 6: Check container status
echo "Checking container status..."
docker ps | grep thinkonerp-api
echo ""

# Step 7: Check logs
echo "Checking logs (last 30 lines)..."
docker logs --tail 30 thinkonerp-api
echo ""

# Step 8: Test connection
echo "Testing connection..."
echo "From localhost:"
curl -s http://localhost:5000/health || echo "❌ Failed"
echo ""
echo "From server IP:"
curl -s http://178.104.126.99:5000/health || echo "❌ Failed"
echo ""

echo "=========================================="
echo "Quick Fix Complete"
echo "=========================================="
echo ""
echo "If still not working, run: ./diagnose-docker-connection.sh"
