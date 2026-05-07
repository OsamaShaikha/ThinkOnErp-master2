# Deploy ThinkOnErp API to Port 5001 (Fixed Version)
# PowerShell script for Windows

$ErrorActionPreference = "Stop"

$SERVER_IP = "178.104.126.99"
$SERVER_USER = "root"
$SERVER_PASSWORD = "ThinkOnErp!@123"
$REMOTE_DIR = "/root/thinkonerp-port5001"

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "ThinkOnErp Port 5001 Deployment (Fixed)" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Server: $SERVER_IP"
Write-Host "Remote Directory: $REMOTE_DIR"
Write-Host ""

# Check if plink and pscp are available (PuTTY tools)
$plinkPath = Get-Command plink -ErrorAction SilentlyContinue
$pscpPath = Get-Command pscp -ErrorAction SilentlyContinue

if (-not $plinkPath -or -not $pscpPath) {
    Write-Host "Error: PuTTY tools (plink and pscp) are not installed." -ForegroundColor Red
    Write-Host "Download from: https://www.chiark.greenend.org.uk/~sgtatham/putty/latest.html" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Alternative: Use WSL and run the bash script instead:" -ForegroundColor Yellow
    Write-Host "  wsl bash deploy-port5001-fixed.sh" -ForegroundColor Yellow
    exit 1
}

Write-Host "Step 1: Creating remote directory..." -ForegroundColor Green
echo y | plink -ssh -pw $SERVER_PASSWORD ${SERVER_USER}@${SERVER_IP} "mkdir -p $REMOTE_DIR"

Write-Host ""
Write-Host "Step 2: Uploading project files..." -ForegroundColor Green
Write-Host "This may take a few minutes..."

# Upload source files
Write-Host "  Uploading src directory..."
pscp -r -pw $SERVER_PASSWORD src ${SERVER_USER}@${SERVER_IP}:${REMOTE_DIR}/

# Upload solution file
Write-Host "  Uploading solution file..."
pscp -pw $SERVER_PASSWORD ThinkOnErp.sln ${SERVER_USER}@${SERVER_IP}:${REMOTE_DIR}/

# Upload Docker files
Write-Host "  Uploading Docker configuration..."
pscp -pw $SERVER_PASSWORD Dockerfile.port5001 ${SERVER_USER}@${SERVER_IP}:${REMOTE_DIR}/
pscp -pw $SERVER_PASSWORD docker-compose.port5001.yml ${SERVER_USER}@${SERVER_IP}:${REMOTE_DIR}/
pscp -pw $SERVER_PASSWORD .env.port5001 ${SERVER_USER}@${SERVER_IP}:${REMOTE_DIR}/

if (Test-Path .dockerignore) {
    pscp -pw $SERVER_PASSWORD .dockerignore ${SERVER_USER}@${SERVER_IP}:${REMOTE_DIR}/
}

Write-Host ""
Write-Host "Step 3: Stopping existing container (if running)..." -ForegroundColor Green
plink -ssh -pw $SERVER_PASSWORD ${SERVER_USER}@${SERVER_IP} @"
cd $REMOTE_DIR
docker stop thinkonerp-api-5001 2>/dev/null || true
docker rm thinkonerp-api-5001 2>/dev/null || true
"@

Write-Host ""
Write-Host "Step 4: Building Docker image..." -ForegroundColor Green
plink -ssh -pw $SERVER_PASSWORD ${SERVER_USER}@${SERVER_IP} @"
cd $REMOTE_DIR
docker build -f Dockerfile.port5001 -t thinkonerp-api:port5001 .
"@

Write-Host ""
Write-Host "Step 5: Starting container on port 5001..." -ForegroundColor Green
plink -ssh -pw $SERVER_PASSWORD ${SERVER_USER}@${SERVER_IP} @"
cd $REMOTE_DIR
docker run -d \
    --name thinkonerp-api-5001 \
    --restart unless-stopped \
    -p 5001:5001 \
    --env-file .env.port5001 \
    thinkonerp-api:port5001
"@

Write-Host ""
Write-Host "Step 6: Waiting for application to start..." -ForegroundColor Green
Start-Sleep -Seconds 10

Write-Host ""
Write-Host "Step 7: Checking container status..." -ForegroundColor Green
plink -ssh -pw $SERVER_PASSWORD ${SERVER_USER}@${SERVER_IP} @"
echo 'Container Status:'
docker ps -a | grep thinkonerp-api-5001 || echo 'Container not found!'
echo ''
echo 'Recent Logs:'
docker logs --tail 50 thinkonerp-api-5001
"@

Write-Host ""
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "Deployment Complete!" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Access your API at:" -ForegroundColor Green
Write-Host "  Swagger UI: http://${SERVER_IP}:5001/swagger" -ForegroundColor Yellow
Write-Host "  Health Check: http://${SERVER_IP}:5001/health" -ForegroundColor Yellow
Write-Host ""
Write-Host "To view logs:" -ForegroundColor Green
Write-Host "  ssh root@$SERVER_IP" -ForegroundColor Yellow
Write-Host "  docker logs -f thinkonerp-api-5001" -ForegroundColor Yellow
Write-Host ""
Write-Host "To restart:" -ForegroundColor Green
Write-Host "  ssh root@$SERVER_IP" -ForegroundColor Yellow
Write-Host "  docker restart thinkonerp-api-5001" -ForegroundColor Yellow
Write-Host ""
