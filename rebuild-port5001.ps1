# Quick rebuild and restart for port 5001 after code changes

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "Rebuilding ThinkOnErp API (Port 5001)" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host ""

$SERVER_IP = "178.104.126.99"
$SERVER_USER = "root"

Write-Host "Uploading updated files..." -ForegroundColor Yellow
scp src/ThinkOnErp.API/Program.cs "${SERVER_USER}@${SERVER_IP}:~/ThinkOnErp/src/ThinkOnErp.API/"
scp src/ThinkOnErp.API/appsettings.Production.json "${SERVER_USER}@${SERVER_IP}:~/ThinkOnErp/src/ThinkOnErp.API/"

Write-Host ""
Write-Host "Rebuilding on server..." -ForegroundColor Yellow

$rebuildScript = @"
cd ~/ThinkOnErp

echo 'Stopping container...'
docker-compose -f docker-compose.port5001.yml down

echo 'Rebuilding image...'
docker-compose -f docker-compose.port5001.yml build --no-cache

echo 'Starting container...'
docker-compose -f docker-compose.port5001.yml up -d

echo 'Waiting for API...'
sleep 10

echo 'Checking status...'
docker-compose -f docker-compose.port5001.yml ps

echo ''
echo 'Testing health endpoint...'
curl -f http://localhost:5001/health && echo '✓ Health check passed' || echo '⚠ Health check failed'

echo ''
echo '=========================================='
echo 'Rebuild Complete!'
echo '=========================================='
echo ''
echo 'Access your API at:'
echo '  http://178.104.126.99:5001/swagger'
echo ''
"@

ssh "${SERVER_USER}@${SERVER_IP}" $rebuildScript

Write-Host ""
Write-Host "Done! Open http://178.104.126.99:5001/swagger in your browser" -ForegroundColor Green

Read-Host "Press Enter to exit"
