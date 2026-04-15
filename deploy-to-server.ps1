#!/usr/bin/env pwsh
# Automated deployment script for ThinkOnErp API
# This script commits changes, pushes to Git, and deploys to server

param(
    [string]$ServerIP = "",
    [string]$ServerUser = "root",
    [string]$CommitMessage = "Update: $(Get-Date -Format 'yyyy-MM-dd HH:mm')",
    [string]$Branch = "main",
    [switch]$SkipGitPush,
    [switch]$SimpleDeployment
)

Write-Host "==================================" -ForegroundColor Cyan
Write-Host "ThinkOnErp Deployment Script" -ForegroundColor Cyan
Write-Host "==================================" -ForegroundColor Cyan
Write-Host ""

# Function to check if command exists
function Test-Command {
    param($Command)
    $null -ne (Get-Command $Command -ErrorAction SilentlyContinue)
}

# Check prerequisites
Write-Host "Checking prerequisites..." -ForegroundColor Yellow
if (-not (Test-Command git)) {
    Write-Host "ERROR: Git is not installed!" -ForegroundColor Red
    exit 1
}

if (-not (Test-Command ssh)) {
    Write-Host "ERROR: SSH is not available!" -ForegroundColor Red
    exit 1
}

# Get server IP if not provided
if ([string]::IsNullOrEmpty($ServerIP)) {
    $ServerIP = Read-Host "Enter your server IP address"
}

# Step 1: Git operations (unless skipped)
if (-not $SkipGitPush) {
    Write-Host ""
    Write-Host "Step 1: Committing and pushing changes to Git..." -ForegroundColor Yellow
    
    # Check if there are changes
    $status = git status --porcelain
    if ($status) {
        Write-Host "Changes detected. Committing..." -ForegroundColor Green
        
        # Add all changes
        git add .
        
        # Commit
        git commit -m $CommitMessage
        
        # Push to remote
        Write-Host "Pushing to remote repository..." -ForegroundColor Green
        git push origin $Branch
        
        if ($LASTEXITCODE -ne 0) {
            Write-Host "ERROR: Failed to push to Git!" -ForegroundColor Red
            exit 1
        }
        
        Write-Host "Successfully pushed to Git!" -ForegroundColor Green
    } else {
        Write-Host "No changes to commit." -ForegroundColor Gray
    }
} else {
    Write-Host "Skipping Git push (--SkipGitPush flag set)" -ForegroundColor Gray
}

# Step 2: Deploy to server
Write-Host ""
Write-Host "Step 2: Deploying to server $ServerIP..." -ForegroundColor Yellow

# Get repository URL
$repoUrl = git config --get remote.origin.url
if ([string]::IsNullOrEmpty($repoUrl)) {
    Write-Host "ERROR: Could not get Git repository URL!" -ForegroundColor Red
    exit 1
}

Write-Host "Repository: $repoUrl" -ForegroundColor Gray

# Determine deployment script
$deployScript = if ($SimpleDeployment) { "deploy-simple.sh" } else { "deploy.sh" }

# Create deployment commands
$deployCommands = @"
set -e
echo '==================================='
echo 'Starting deployment on server...'
echo '==================================='

# Check if directory exists
if [ -d "ThinkOnErp" ]; then
    echo 'Repository exists. Pulling latest changes...'
    cd ThinkOnErp
    git pull origin $Branch
else
    echo 'Cloning repository...'
    git clone $repoUrl ThinkOnErp
    cd ThinkOnErp
fi

# Make deployment script executable
chmod +x $deployScript

# Run deployment
echo 'Running deployment script...'
./$deployScript

echo '==================================='
echo 'Deployment completed successfully!'
echo '==================================='
"@

# Execute on server
Write-Host "Connecting to server..." -ForegroundColor Green
Write-Host "You may be prompted for your SSH password..." -ForegroundColor Gray
Write-Host ""

ssh "${ServerUser}@${ServerIP}" $deployCommands

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "==================================" -ForegroundColor Green
    Write-Host "Deployment completed successfully!" -ForegroundColor Green
    Write-Host "==================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "Your API should be running at:" -ForegroundColor Cyan
    Write-Host "  http://${ServerIP}:5160" -ForegroundColor White
    Write-Host "  https://${ServerIP}:7136" -ForegroundColor White
} else {
    Write-Host ""
    Write-Host "ERROR: Deployment failed!" -ForegroundColor Red
    exit 1
}
