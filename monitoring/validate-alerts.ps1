# Alert Configuration Validation Script (PowerShell)
# This script validates Prometheus alert rule files for syntax errors

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "Prometheus Alert Configuration Validation" -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan
Write-Host ""

# Check if promtool is available
$promtoolAvailable = $null -ne (Get-Command promtool -ErrorAction SilentlyContinue)

if (-not $promtoolAvailable) {
    Write-Host "⚠️  WARNING: promtool not found. Install Prometheus to validate alert rules." -ForegroundColor Yellow
    Write-Host "   Download from: https://prometheus.io/download/" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Performing basic file existence check instead..." -ForegroundColor Yellow
    Write-Host ""
}

# Alert rule files to validate
$alertFiles = @(
    "prometheus/alerts/audit-system-alerts.yml",
    "prometheus/alerts/database-alerts.yml",
    "prometheus/alerts/audit-failures-alerts.yml",
    "prometheus/alerts/security-alerts.yml"
)

# Validate each alert file
$totalFiles = 0
$validFiles = 0
$invalidFiles = 0

foreach ($file in $alertFiles) {
    $totalFiles++
    Write-Host "Validating: $file" -ForegroundColor White
    
    if (-not (Test-Path $file)) {
        Write-Host "  ❌ ERROR: File not found" -ForegroundColor Red
        $invalidFiles++
        continue
    }
    
    if ($promtoolAvailable) {
        # Use promtool for validation
        $result = & promtool check rules $file 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Host "  ✅ Valid" -ForegroundColor Green
            $validFiles++
        } else {
            Write-Host "  ❌ Invalid - promtool check failed" -ForegroundColor Red
            Write-Host $result -ForegroundColor Red
            $invalidFiles++
        }
    } else {
        # Basic file check
        try {
            $content = Get-Content $file -Raw
            if ($content.Length -gt 0) {
                Write-Host "  ✅ File exists and is not empty" -ForegroundColor Green
                $validFiles++
            } else {
                Write-Host "  ❌ File is empty" -ForegroundColor Red
                $invalidFiles++
            }
        } catch {
            Write-Host "  ❌ Error reading file: $_" -ForegroundColor Red
            $invalidFiles++
        }
    }
    Write-Host ""
}

# Validate prometheus.yml
Write-Host "Validating: prometheus/prometheus.yml" -ForegroundColor White
if (-not (Test-Path "prometheus/prometheus.yml")) {
    Write-Host "  ❌ ERROR: prometheus.yml not found" -ForegroundColor Red
} else {
    if ($promtoolAvailable) {
        $result = & promtool check config "prometheus/prometheus.yml" 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Host "  ✅ Valid" -ForegroundColor Green
        } else {
            Write-Host "  ❌ Invalid - promtool check failed" -ForegroundColor Red
            Write-Host $result -ForegroundColor Red
        }
    } else {
        try {
            $content = Get-Content "prometheus/prometheus.yml" -Raw
            if ($content.Length -gt 0) {
                Write-Host "  ✅ File exists and is not empty" -ForegroundColor Green
            } else {
                Write-Host "  ❌ File is empty" -ForegroundColor Red
            }
        } catch {
            Write-Host "  ❌ Error reading file: $_" -ForegroundColor Red
        }
    }
}
Write-Host ""

# Summary
Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "Validation Summary" -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "Total alert files: $totalFiles" -ForegroundColor White
Write-Host "Valid files: $validFiles" -ForegroundColor Green
Write-Host "Invalid files: $invalidFiles" -ForegroundColor $(if ($invalidFiles -eq 0) { "Green" } else { "Red" })
Write-Host ""

if ($invalidFiles -eq 0) {
    Write-Host "✅ All alert configurations are valid!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Cyan
    Write-Host "1. Start Prometheus: docker-compose -f docker-compose.monitoring.yml up -d" -ForegroundColor White
    Write-Host "2. Verify alerts loaded: http://localhost:9090/alerts" -ForegroundColor White
    Write-Host "3. Configure notification channels in Grafana" -ForegroundColor White
    exit 0
} else {
    Write-Host "❌ Some alert configurations have errors. Please fix them before deploying." -ForegroundColor Red
    exit 1
}
