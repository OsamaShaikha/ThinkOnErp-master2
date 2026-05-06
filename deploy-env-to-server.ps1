# Deploy .env file to server and restart Docker container
# Usage: .\deploy-env-to-server.ps1

$ErrorActionPreference = "Stop"

$SERVER_USER = "root"
$SERVER_HOST = "178.104.126.99"
$SERVER_PATH = "~/ThinkOnErp"

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "ThinkOnErp - Deploy Environment Config" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host ""

# Check if .env.production exists
if (-not (Test-Path ".env.production")) {
    Write-Host "❌ Error: .env.production file not found!" -ForegroundColor Red
    Write-Host "Please ensure .env.production exists in the current directory."
    exit 1
}

Write-Host "📋 Configuration file found: .env.production" -ForegroundColor Green
Write-Host ""

# Display configuration summary (without sensitive values)
Write-Host "Configuration Summary:" -ForegroundColor Yellow
Write-Host "  - Database: Oracle @ 178.104.126.99:1521/XEPDB1"
Write-Host "  - JWT: Configured"
Write-Host "  - Audit Trail: MetadataOnly logging"
Write-Host "  - Alerts: Configured with 2 retry attempts"
Write-Host "  - Redis: Disabled (optional)"
Write-Host ""

# Confirm deployment
$confirmation = Read-Host "Deploy this configuration to ${SERVER_HOST}? (y/n)"
if ($confirmation -ne 'y' -and $confirmation -ne 'Y') {
    Write-Host "❌ Deployment cancelled." -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "🚀 Deploying configuration to server..." -ForegroundColor Cyan
Write-Host ""

# Copy .env.production to server as .env
Write-Host "📤 Uploading .env file to server..." -ForegroundColor Yellow
scp .env.production "${SERVER_USER}@${SERVER_HOST}:${SERVER_PATH}/.env"

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Failed to upload .env file to server!" -ForegroundColor Red
    exit 1
}

Write-Host "✅ Configuration file uploaded successfully" -ForegroundColor Green
Write-Host ""

# Restart Docker container on server
Write-Host "🔄 Restarting Docker container on server..." -ForegroundColor Yellow

$sshCommands = @"
cd ~/ThinkOnErp

echo 'Stopping container...'
docker-compose -f docker-compose.simple.yml down

echo 'Starting container with new configuration...'
docker-compose -f docker-compose.simple.yml up -d

echo ''
echo 'Waiting for container to start (10 seconds)...'
sleep 10

echo ''
echo '📊 Container logs (last 30 lines):'
docker logs --tail 30 thinkonerp-api

echo ''
echo '🔍 Container status:'
docker ps | grep thinkonerp-api || echo '⚠️  Container not running!'
"@

ssh "${SERVER_USER}@${SERVER_HOST}" $sshCommands

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "❌ Failed to restart container on server!" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "✅ Deployment Complete!" -ForegroundColor Green
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  1. Check logs: ssh root@178.104.126.99 'docker logs -f thinkonerp-api'"
Write-Host "  2. Test API: curl http://178.104.126.99:5000/health"
Write-Host "  3. Access Swagger: http://178.104.126.99:5000/swagger"
Write-Host ""
