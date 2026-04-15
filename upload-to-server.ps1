#!/usr/bin/env pwsh
# Direct upload script to server using SCP
# Uploads the entire project directory to the server

param(
    [string]$ServerIP = "178.104.126.99",
    [string]$ServerUser = "root",
    [string]$RemotePath = "/root/ThinkOnErp",
    [switch]$SkipBuild,
    [switch]$DeployAfterUpload
)

Write-Host "==================================" -ForegroundColor Cyan
Write-Host "ThinkOnErp Direct Upload Script" -ForegroundColor Cyan
Write-Host "==================================" -ForegroundColor Cyan
Write-Host ""

# Check if SCP is available
if (-not (Get-Command scp -ErrorAction SilentlyContinue)) {
    Write-Host "ERROR: SCP is not available!" -ForegroundColor Red
    Write-Host "Please install OpenSSH client." -ForegroundColor Yellow
    exit 1
}

Write-Host "Server: $ServerUser@$ServerIP" -ForegroundColor Gray
Write-Host "Remote Path: $RemotePath" -ForegroundColor Gray
Write-Host ""

# Create a temporary directory for upload
$tempDir = Join-Path $env:TEMP "thinkonerp-upload"
if (Test-Path $tempDir) {
    Remove-Item $tempDir -Recurse -Force
}
New-Item -ItemType Directory -Path $tempDir | Out-Null

Write-Host "Preparing files for upload..." -ForegroundColor Yellow

# Copy necessary files (exclude unnecessary folders)
$excludeDirs = @(
    "bin", "obj", ".vs", ".git", "node_modules", 
    "TestResults", "logs", ".kiro"
)

# Get all items
Get-ChildItem -Path . -Recurse | ForEach-Object {
    $relativePath = $_.FullName.Substring((Get-Location).Path.Length + 1)
    $shouldExclude = $false
    
    foreach ($exclude in $excludeDirs) {
        if ($relativePath -like "*\$exclude\*" -or $relativePath -like "*/$exclude/*") {
            $shouldExclude = $true
            break
        }
    }
    
    if (-not $shouldExclude) {
        $targetPath = Join-Path $tempDir $relativePath
        $targetDir = Split-Path $targetPath -Parent
        
        if (-not (Test-Path $targetDir)) {
            New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
        }
        
        if ($_.PSIsContainer -eq $false) {
            Copy-Item $_.FullName $targetPath -Force
        }
    }
}

Write-Host "Files prepared. Starting upload..." -ForegroundColor Green
Write-Host ""

# Create remote directory if it doesn't exist
Write-Host "Creating remote directory..." -ForegroundColor Yellow
ssh "${ServerUser}@${ServerIP}" "mkdir -p $RemotePath"

# Upload files using SCP
Write-Host "Uploading files to server..." -ForegroundColor Yellow
Write-Host "This may take a few minutes..." -ForegroundColor Gray
Write-Host ""

scp -r "$tempDir/*" "${ServerUser}@${ServerIP}:${RemotePath}/"

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "Upload completed successfully!" -ForegroundColor Green
    
    # Clean up temp directory
    Remove-Item $tempDir -Recurse -Force
    
    # Deploy if requested
    if ($DeployAfterUpload) {
        Write-Host ""
        Write-Host "Starting deployment on server..." -ForegroundColor Yellow
        
        $deployCommands = @"
cd $RemotePath
chmod +x deploy-simple.sh
./deploy-simple.sh
"@
        
        ssh "${ServerUser}@${ServerIP}" $deployCommands
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host ""
            Write-Host "==================================" -ForegroundColor Green
            Write-Host "Deployment completed successfully!" -ForegroundColor Green
            Write-Host "==================================" -ForegroundColor Green
            Write-Host ""
            Write-Host "Your API is running at:" -ForegroundColor Cyan
            Write-Host "  http://${ServerIP}:5000" -ForegroundColor White
            Write-Host "  Swagger: http://${ServerIP}:5000/swagger" -ForegroundColor White
        } else {
            Write-Host "ERROR: Deployment failed!" -ForegroundColor Red
        }
    } else {
        Write-Host ""
        Write-Host "Files uploaded to: $RemotePath" -ForegroundColor Cyan
        Write-Host ""
        Write-Host "To deploy, SSH to your server and run:" -ForegroundColor Yellow
        Write-Host "  ssh ${ServerUser}@${ServerIP}" -ForegroundColor White
        Write-Host "  cd $RemotePath" -ForegroundColor White
        Write-Host "  ./deploy-simple.sh" -ForegroundColor White
    }
} else {
    Write-Host ""
    Write-Host "ERROR: Upload failed!" -ForegroundColor Red
    Remove-Item $tempDir -Recurse -Force
    exit 1
}
