# ThinkOnErp - Build & Push to Docker Hub
# Run this on your local Windows machine

$DOCKER_HUB_USER = "devosamaarori"
$IMAGE_NAME = "thinkonerp-api"
$TAG = "latest"

Write-Host "Building Docker image..." -ForegroundColor Cyan
docker build -t "${DOCKER_HUB_USER}/${IMAGE_NAME}:${TAG}" -f Dockerfile .

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

Write-Host "`nBuild successful! Pushing to Docker Hub..." -ForegroundColor Cyan
Write-Host "Make sure you're logged in: docker login" -ForegroundColor Yellow

docker push "${DOCKER_HUB_USER}/${IMAGE_NAME}:${TAG}"

if ($LASTEXITCODE -eq 0) {
    Write-Host "`nDone! Image pushed to: ${DOCKER_HUB_USER}/${IMAGE_NAME}:${TAG}" -ForegroundColor Green
    Write-Host "`nOn your Ubuntu server, run:" -ForegroundColor Cyan
    Write-Host "  mkdir -p ~/ThinkOnErp && cd ~/ThinkOnErp" -ForegroundColor White
    Write-Host '  curl -O https://raw.githubusercontent.com/devosamaarori/thinkonerp/main/deploy-from-dockerhub.sh' -ForegroundColor White
    Write-Host "  chmod +x deploy-from-dockerhub.sh && ./deploy-from-dockerhub.sh" -ForegroundColor White
}
