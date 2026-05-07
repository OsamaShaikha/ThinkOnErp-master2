# ThinkOnErp - Build and Push to Docker Hub
# Run this script on your local Windows machine

param(
    [Parameter(Mandatory=$true)]
    [string]$DockerHubUsername,
    
    [Parameter(Mandatory=$false)]
    [string]$Version = "v1.0"
)

$ErrorActionPreference = "Stop"

Write-Host "=========================================="
Write-Host "ThinkOnErp - Build and Push to Docker Hub"
Write-Host "=========================================="
Write-Host ""

# Check if Docker is running
try {
    docker version | Out-Null
} catch {
    Write-Host "❌ Docker is not running. Please start Docker Desktop." -ForegroundColor Red
    exit 1
}

# Login to Docker Hub
Write-Host "🔐 Logging in to Docker Hub..." -ForegroundColor Cyan
docker login

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Docker Hub login failed" -ForegroundColor Red
    exit 1
}

Write-Host "✅ Logged in successfully" -ForegroundColor Green
Write-Host ""

# Build Oracle Image
Write-Host "🏗️  Building Oracle Database Image..." -ForegroundColor Cyan
Write-Host "   Image: $DockerHubUsername/thinkonerp-oracle:$Version" -ForegroundColor Yellow
Write-Host "   This will take 5-10 minutes (Oracle base image is ~2GB)..." -ForegroundColor Yellow
Write-Host ""

docker build -t "${DockerHubUsername}/thinkonerp-oracle:${Version}" -f Dockerfile.oracle .

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Oracle image build failed" -ForegroundColor Red
    exit 1
}

Write-Host "✅ Oracle image built successfully" -ForegroundColor Green
Write-Host ""

# Build API Image
Write-Host "🏗️  Building API Image..." -ForegroundColor Cyan
Write-Host "   Image: $DockerHubUsername/thinkonerp-api:$Version" -ForegroundColor Yellow
Write-Host "   This will take 2-5 minutes..." -ForegroundColor Yellow
Write-Host ""

docker build -t "${DockerHubUsername}/thinkonerp-api:${Version}" .

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ API image build failed" -ForegroundColor Red
    exit 1
}

Write-Host "✅ API image built successfully" -ForegroundColor Green
Write-Host ""

# Push Oracle Image
Write-Host "📤 Pushing Oracle Image to Docker Hub..." -ForegroundColor Cyan
Write-Host "   This will take 5-15 minutes depending on your internet speed..." -ForegroundColor Yellow
Write-Host ""

docker push "${DockerHubUsername}/thinkonerp-oracle:${Version}"

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Oracle image push failed" -ForegroundColor Red
    exit 1
}

Write-Host "✅ Oracle image pushed successfully" -ForegroundColor Green
Write-Host ""

# Push API Image
Write-Host "📤 Pushing API Image to Docker Hub..." -ForegroundColor Cyan
Write-Host "   This will take 2-5 minutes..." -ForegroundColor Yellow
Write-Host ""

docker push "${DockerHubUsername}/thinkonerp-api:${Version}"

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ API image push failed" -ForegroundColor Red
    exit 1
}

Write-Host "✅ API image pushed successfully" -ForegroundColor Green
Write-Host ""

# Summary
Write-Host "=========================================="
Write-Host "✅ All Images Built and Pushed Successfully!"
Write-Host "=========================================="
Write-Host ""
Write-Host "📦 Images on Docker Hub:" -ForegroundColor Cyan
Write-Host "   Oracle: $DockerHubUsername/thinkonerp-oracle:$Version" -ForegroundColor Yellow
Write-Host "   API:    $DockerHubUsername/thinkonerp-api:$Version" -ForegroundColor Yellow
Write-Host ""
Write-Host "🌐 View on Docker Hub:" -ForegroundColor Cyan
Write-Host "   https://hub.docker.com/r/$DockerHubUsername/thinkonerp-oracle" -ForegroundColor Yellow
Write-Host "   https://hub.docker.com/r/$DockerHubUsername/thinkonerp-api" -ForegroundColor Yellow
Write-Host ""
Write-Host "🚀 Next Steps:" -ForegroundColor Cyan
Write-Host "   1. SSH to your server: ssh root@178.104.126.99" -ForegroundColor White
Write-Host "   2. Download deployment script:" -ForegroundColor White
Write-Host "      wget https://raw.githubusercontent.com/YourRepo/master2/deploy-from-dockerhub.sh" -ForegroundColor Gray
Write-Host "   3. Make it executable: chmod +x deploy-from-dockerhub.sh" -ForegroundColor White
Write-Host "   4. Run it: ./deploy-from-dockerhub.sh" -ForegroundColor White
Write-Host ""
Write-Host "   OR manually create docker-compose.yml on server and run:" -ForegroundColor White
Write-Host "      docker-compose pull" -ForegroundColor Gray
Write-Host "      docker-compose up -d" -ForegroundColor Gray
Write-Host ""
Write-Host "=========================================="

# Show image sizes
Write-Host ""
Write-Host "📊 Image Sizes:" -ForegroundColor Cyan
docker images | Select-String -Pattern "thinkonerp"
Write-Host ""
