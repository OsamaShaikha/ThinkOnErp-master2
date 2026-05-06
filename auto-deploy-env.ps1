# Automated .env deployment to server
# This script uses sshpass equivalent for Windows (plink from PuTTY)

$SERVER_USER = "root"
$SERVER_HOST = "178.104.126.99"
$SERVER_PASSWORD = "ThinkOnErp!@123"
$SERVER_PATH = "~/ThinkOnErp"

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "ThinkOnErp - Auto Deploy Environment" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host ""

# Check if .env.production exists
if (-not (Test-Path ".env.production")) {
    Write-Host "❌ Error: .env.production file not found!" -ForegroundColor Red
    exit 1
}

Write-Host "✅ Configuration file found" -ForegroundColor Green
Write-Host ""

# Method 1: Try using pscp (PuTTY SCP) if available
$pscpPath = Get-Command pscp -ErrorAction SilentlyContinue

if ($pscpPath) {
    Write-Host "📤 Uploading .env file using pscp..." -ForegroundColor Yellow
    echo y | pscp -pw $SERVER_PASSWORD .env.production "${SERVER_USER}@${SERVER_HOST}:${SERVER_PATH}/.env"
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ File uploaded successfully" -ForegroundColor Green
        
        Write-Host ""
        Write-Host "🔄 Restarting Docker container..." -ForegroundColor Yellow
        
        $commands = @"
cd ~/ThinkOnErp && docker-compose -f docker-compose.simple.yml down && docker-compose -f docker-compose.simple.yml up -d && sleep 5 && docker logs --tail 30 thinkonerp-api
"@
        
        echo y | plink -pw $SERVER_PASSWORD "${SERVER_USER}@${SERVER_HOST}" $commands
        
        Write-Host ""
        Write-Host "✅ Deployment complete!" -ForegroundColor Green
        Write-Host ""
        Write-Host "Test the API: http://178.104.126.99:5000/swagger" -ForegroundColor Cyan
        exit 0
    }
}

# Method 2: Manual instructions if pscp not available
Write-Host "⚠️  PuTTY tools (pscp/plink) not found" -ForegroundColor Yellow
Write-Host ""
Write-Host "Please use one of these methods:" -ForegroundColor Cyan
Write-Host ""
Write-Host "METHOD 1: Using WinSCP (Recommended)" -ForegroundColor Yellow
Write-Host "  1. Download WinSCP: https://winscp.net/download/WinSCP-5.21.7-Setup.exe"
Write-Host "  2. Connect to: $SERVER_HOST"
Write-Host "  3. Username: $SERVER_USER"
Write-Host "  4. Password: $SERVER_PASSWORD"
Write-Host "  5. Upload .env.production to /root/ThinkOnErp/.env"
Write-Host "  6. Run commands in terminal (see below)"
Write-Host ""
Write-Host "METHOD 2: Using PowerShell SSH" -ForegroundColor Yellow
Write-Host "  Run these commands:" -ForegroundColor White
Write-Host ""
Write-Host "  # Upload file" -ForegroundColor Gray
Write-Host "  scp .env.production root@178.104.126.99:~/ThinkOnErp/.env" -ForegroundColor White
Write-Host "  # Password: ThinkOnErp!@123" -ForegroundColor Gray
Write-Host ""
Write-Host "  # Connect to server" -ForegroundColor Gray
Write-Host "  ssh root@178.104.126.99" -ForegroundColor White
Write-Host "  # Password: ThinkOnErp!@123" -ForegroundColor Gray
Write-Host ""
Write-Host "  # On server, run:" -ForegroundColor Gray
Write-Host "  cd ~/ThinkOnErp" -ForegroundColor White
Write-Host "  docker-compose -f docker-compose.simple.yml down" -ForegroundColor White
Write-Host "  docker-compose -f docker-compose.simple.yml up -d" -ForegroundColor White
Write-Host "  docker logs -f thinkonerp-api" -ForegroundColor White
Write-Host ""
