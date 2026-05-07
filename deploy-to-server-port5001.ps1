# ThinkOnErp API - Deploy to Server 178.104.126.99 on Port 5001
# PowerShell script for Windows

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "ThinkOnErp API - Server Deployment (Port 5001)" -ForegroundColor Cyan
Write-Host "Server: 178.104.126.99" -ForegroundColor Cyan
Write-Host "Port: 5001" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host ""

# Server details
$SERVER_IP = "178.104.126.99"
$SERVER_USER = "root"
$SERVER_PATH = "~/ThinkOnErp"

Write-Host "Step 1: Uploading files to server..." -ForegroundColor Blue
Write-Host ""

# Upload files using SCP
Write-Host "Uploading Dockerfile.port5001..." -ForegroundColor Yellow
scp Dockerfile.port5001 "${SERVER_USER}@${SERVER_IP}:${SERVER_PATH}/"

Write-Host "Uploading docker-compose.port5001.yml..." -ForegroundColor Yellow
scp docker-compose.port5001.yml "${SERVER_USER}@${SERVER_IP}:${SERVER_PATH}/"

Write-Host "Uploading .env.port5001..." -ForegroundColor Yellow
scp .env.port5001 "${SERVER_USER}@${SERVER_IP}:${SERVER_PATH}/.env.production"

Write-Host "Files uploaded successfully" -ForegroundColor Green
Write-Host ""

Write-Host "Step 2: Deploying on server..." -ForegroundColor Blue
Write-Host ""

# Create deployment script
$deployScript = @"
cd ~/ThinkOnErp

echo 'Stopping existing container (if any)...'
docker-compose -f docker-compose.port5001.yml down 2>/dev/null || true

echo 'Building Docker image...'
docker-compose -f docker-compose.port5001.yml build --no-cache

echo 'Starting container on port 5001...'
docker-compose -f docker-compose.port5001.yml up -d

echo 'Waiting for API to be ready...'
sleep 15

# Check container status
if docker ps | grep -q thinkonerp-api-5001; then
    echo '✓ Container is running'
else
    echo '✗ Container failed to start'
    echo 'Checking logs...'
    docker-compose -f docker-compose.port5001.yml logs --tail=50
    exit 1
fi

# Test health endpoint
echo 'Testing health endpoint...'
if curl -f http://localhost:5001/health &> /dev/null; then
    echo '✓ Health check passed'
else
    echo '⚠ Health check failed (API might still be starting)'
fi

echo ''
echo '=========================================='
echo 'Deployment Complete!'
echo '=========================================='
echo ''
echo 'API is running on:'
echo '  - Swagger UI: http://178.104.126.99:5001/swagger'
echo '  - Health Check: http://178.104.126.99:5001/health'
echo '  - Base URL: http://178.104.126.99:5001'
echo ''
echo 'Container name: thinkonerp-api-5001'
echo ''
"@

# Execute deployment on server
ssh "${SERVER_USER}@${SERVER_IP}" $deployScript

Write-Host ""
Write-Host "==========================================" -ForegroundColor Green
Write-Host "Deployment to server completed!" -ForegroundColor Green
Write-Host "==========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Access your API at:" -ForegroundColor Blue
Write-Host "  🌐 Swagger UI: http://178.104.126.99:5001/swagger"
Write-Host "  ❤️  Health Check: http://178.104.126.99:5001/health"
Write-Host ""
Write-Host "Test credentials:" -ForegroundColor Yellow
Write-Host "  Username: superadmin"
Write-Host "  Password: SuperAdmin123!"
Write-Host ""

Read-Host "Press Enter to exit"
