# =====================================================
# Git Push Diagnostic Script
# =====================================================
# This script helps diagnose why Git push is failing
# =====================================================

Write-Host "=====================================================" -ForegroundColor Cyan
Write-Host "Git Push Diagnostic Tool" -ForegroundColor Cyan
Write-Host "=====================================================" -ForegroundColor Cyan
Write-Host ""

# Check if Git is installed
Write-Host "1. Checking Git installation..." -ForegroundColor Yellow
$gitVersion = git --version 2>&1
if ($LASTEXITCODE -eq 0) {
    Write-Host "   ✓ Git is installed: $gitVersion" -ForegroundColor Green
} else {
    Write-Host "   ✗ Git is not installed or not in PATH" -ForegroundColor Red
    Write-Host "   Please install Git from: https://git-scm.com/download/win" -ForegroundColor Yellow
    pause
    exit 1
}
Write-Host ""

# Check current directory is a Git repository
Write-Host "2. Checking if current directory is a Git repository..." -ForegroundColor Yellow
$isGitRepo = Test-Path ".git"
if ($isGitRepo) {
    Write-Host "   ✓ This is a Git repository" -ForegroundColor Green
} else {
    Write-Host "   ✗ This is not a Git repository" -ForegroundColor Red
    Write-Host "   Please run this script from your Git repository root" -ForegroundColor Yellow
    pause
    exit 1
}
Write-Host ""

# Check Git status
Write-Host "3. Checking Git status..." -ForegroundColor Yellow
$gitStatus = git status --short 2>&1
if ($gitStatus) {
    Write-Host "   Changes detected:" -ForegroundColor Yellow
    Write-Host $gitStatus
} else {
    Write-Host "   ✓ Working directory is clean" -ForegroundColor Green
}
Write-Host ""

# Check current branch
Write-Host "4. Checking current branch..." -ForegroundColor Yellow
$currentBranch = git branch --show-current 2>&1
if ($LASTEXITCODE -eq 0) {
    Write-Host "   Current branch: $currentBranch" -ForegroundColor Green
} else {
    Write-Host "   ✗ Could not determine current branch" -ForegroundColor Red
}
Write-Host ""

# Check remote configuration
Write-Host "5. Checking remote configuration..." -ForegroundColor Yellow
$remotes = git remote -v 2>&1
if ($LASTEXITCODE -eq 0 -and $remotes) {
    Write-Host "   Remote repositories:" -ForegroundColor Green
    Write-Host $remotes
} else {
    Write-Host "   ✗ No remote repositories configured" -ForegroundColor Red
    Write-Host "   You need to add a remote repository first:" -ForegroundColor Yellow
    Write-Host "   git remote add origin <repository-url>" -ForegroundColor Yellow
}
Write-Host ""

# Check if there are commits to push
Write-Host "6. Checking for commits to push..." -ForegroundColor Yellow
$commitsToPush = git log origin/$currentBranch..$currentBranch --oneline 2>&1
if ($LASTEXITCODE -eq 0 -and $commitsToPush) {
    Write-Host "   Commits waiting to be pushed:" -ForegroundColor Yellow
    Write-Host $commitsToPush
} elseif ($LASTEXITCODE -eq 0) {
    Write-Host "   ✓ No commits to push (already up to date)" -ForegroundColor Green
} else {
    Write-Host "   ⚠ Could not check commits (branch may not exist on remote)" -ForegroundColor Yellow
}
Write-Host ""

# Check for large files
Write-Host "7. Checking for large files (>50MB)..." -ForegroundColor Yellow
$largeFiles = Get-ChildItem -Recurse -File | Where-Object { $_.Length -gt 50MB } | Select-Object FullName, @{Name="SizeMB";Expression={[math]::Round($_.Length/1MB, 2)}}
if ($largeFiles) {
    Write-Host "   ⚠ Large files detected:" -ForegroundColor Yellow
    $largeFiles | ForEach-Object {
        Write-Host "   - $($_.FullName) ($($_.SizeMB) MB)" -ForegroundColor Yellow
    }
    Write-Host "   Consider using Git LFS for files >100MB" -ForegroundColor Yellow
} else {
    Write-Host "   ✓ No large files detected" -ForegroundColor Green
}
Write-Host ""

# Test network connectivity
Write-Host "8. Testing network connectivity..." -ForegroundColor Yellow
$remoteUrl = git config --get remote.origin.url 2>&1
if ($remoteUrl -match "github.com") {
    $testHost = "github.com"
} elseif ($remoteUrl -match "gitlab.com") {
    $testHost = "gitlab.com"
} elseif ($remoteUrl -match "dev.azure.com") {
    $testHost = "dev.azure.com"
} else {
    $testHost = $null
}

if ($testHost) {
    $pingResult = Test-Connection -ComputerName $testHost -Count 2 -Quiet 2>&1
    if ($pingResult) {
        Write-Host "   ✓ Can reach $testHost" -ForegroundColor Green
    } else {
        Write-Host "   ✗ Cannot reach $testHost" -ForegroundColor Red
        Write-Host "   Check your internet connection" -ForegroundColor Yellow
    }
} else {
    Write-Host "   ⚠ Could not determine remote host" -ForegroundColor Yellow
}
Write-Host ""

# Check credential helper
Write-Host "9. Checking credential configuration..." -ForegroundColor Yellow
$credHelper = git config --global credential.helper 2>&1
if ($credHelper) {
    Write-Host "   Credential helper: $credHelper" -ForegroundColor Green
} else {
    Write-Host "   ⚠ No credential helper configured" -ForegroundColor Yellow
    Write-Host "   Recommend setting: git config --global credential.helper manager-core" -ForegroundColor Yellow
}
Write-Host ""

# Summary and recommendations
Write-Host "=====================================================" -ForegroundColor Cyan
Write-Host "Summary and Recommendations" -ForegroundColor Cyan
Write-Host "=====================================================" -ForegroundColor Cyan
Write-Host ""

# Determine the most likely issue
$issues = @()
$recommendations = @()

if (-not $remotes) {
    $issues += "No remote repository configured"
    $recommendations += "Add remote: git remote add origin <url>"
}

if ($gitStatus) {
    $issues += "Uncommitted changes detected"
    $recommendations += "Commit changes: git add . && git commit -m 'message'"
}

if ($largeFiles) {
    $issues += "Large files detected (may cause push to fail)"
    $recommendations += "Use Git LFS or add to .gitignore"
}

if (-not $credHelper) {
    $issues += "No credential helper configured"
    $recommendations += "Configure: git config --global credential.helper manager-core"
}

if ($issues.Count -eq 0) {
    Write-Host "✓ No obvious issues detected" -ForegroundColor Green
    Write-Host ""
    Write-Host "Try pushing with verbose output:" -ForegroundColor Yellow
    Write-Host "git push origin $currentBranch --verbose" -ForegroundColor Cyan
} else {
    Write-Host "Issues detected:" -ForegroundColor Red
    $issues | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
    Write-Host ""
    Write-Host "Recommendations:" -ForegroundColor Yellow
    $recommendations | ForEach-Object { Write-Host "  - $_" -ForegroundColor Yellow }
}

Write-Host ""
Write-Host "=====================================================" -ForegroundColor Cyan
Write-Host "Next Steps" -ForegroundColor Cyan
Write-Host "=====================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "1. Review the issues and recommendations above" -ForegroundColor Yellow
Write-Host "2. See GIT_PUSH_TROUBLESHOOTING.md for detailed solutions" -ForegroundColor Yellow
Write-Host "3. Try pushing with: git push origin $currentBranch --verbose" -ForegroundColor Yellow
Write-Host ""

# Ask if user wants to try pushing now
$response = Read-Host "Would you like to try pushing now? (Y/N)"
if ($response -eq 'Y' -or $response -eq 'y') {
    Write-Host ""
    Write-Host "Attempting to push..." -ForegroundColor Yellow
    Write-Host ""
    
    git push origin $currentBranch --verbose
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host ""
        Write-Host "✓ Push successful!" -ForegroundColor Green
    } else {
        Write-Host ""
        Write-Host "✗ Push failed" -ForegroundColor Red
        Write-Host "See error message above and consult GIT_PUSH_TROUBLESHOOTING.md" -ForegroundColor Yellow
    }
}

Write-Host ""
pause
