#!/usr/bin/env pwsh
# Quick deployment script - Upload and deploy in one command

param(
    [string]$ServerIP = "178.104.126.99",
    [string]$ServerUser = "root"
)

Write-Host "==================================" -ForegroundColor Cyan
Write-Host "Quick Deploy to Server" -ForegroundColor Cyan
Write-Host "==================================" -ForegroundColor Cyan
Write-Host ""

# Upload files
Write-Host "Step 1: Uploading files..." -ForegroundColor Yellow
& .\upload-to-server.ps1 -ServerIP $ServerIP -ServerUser $ServerUser -DeployAfterUpload

Write-Host ""
Write-Host "Done! Check your API at:" -ForegroundColor Green
Write-Host "  http://${ServerIP}:5000/swagger" -ForegroundColor Cyan
