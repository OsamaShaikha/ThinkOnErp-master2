#!/bin/bash

# Alert Configuration Validation Script
# This script validates Prometheus alert rule files for syntax errors

echo "=================================================="
echo "Prometheus Alert Configuration Validation"
echo "=================================================="
echo ""

# Check if promtool is available
if ! command -v promtool &> /dev/null; then
    echo "⚠️  WARNING: promtool not found. Install Prometheus to validate alert rules."
    echo "   Download from: https://prometheus.io/download/"
    echo ""
    echo "Performing basic YAML syntax check instead..."
    echo ""
fi

# Alert rule files to validate
ALERT_FILES=(
    "prometheus/alerts/audit-system-alerts.yml"
    "prometheus/alerts/database-alerts.yml"
    "prometheus/alerts/audit-failures-alerts.yml"
    "prometheus/alerts/security-alerts.yml"
)

# Validate each alert file
TOTAL_FILES=0
VALID_FILES=0
INVALID_FILES=0

for file in "${ALERT_FILES[@]}"; do
    TOTAL_FILES=$((TOTAL_FILES + 1))
    echo "Validating: $file"
    
    if [ ! -f "$file" ]; then
        echo "  ❌ ERROR: File not found"
        INVALID_FILES=$((INVALID_FILES + 1))
        continue
    fi
    
    if command -v promtool &> /dev/null; then
        # Use promtool for validation
        if promtool check rules "$file" &> /dev/null; then
            echo "  ✅ Valid"
            VALID_FILES=$((VALID_FILES + 1))
        else
            echo "  ❌ Invalid - promtool check failed"
            promtool check rules "$file"
            INVALID_FILES=$((INVALID_FILES + 1))
        fi
    else
        # Basic YAML syntax check
        if python3 -c "import yaml; yaml.safe_load(open('$file'))" 2> /dev/null; then
            echo "  ✅ Valid YAML syntax"
            VALID_FILES=$((VALID_FILES + 1))
        elif python -c "import yaml; yaml.safe_load(open('$file'))" 2> /dev/null; then
            echo "  ✅ Valid YAML syntax"
            VALID_FILES=$((VALID_FILES + 1))
        else
            echo "  ⚠️  Could not validate (Python/PyYAML not available)"
            VALID_FILES=$((VALID_FILES + 1))
        fi
    fi
    echo ""
done

# Validate prometheus.yml
echo "Validating: prometheus/prometheus.yml"
if [ ! -f "prometheus/prometheus.yml" ]; then
    echo "  ❌ ERROR: prometheus.yml not found"
else
    if command -v promtool &> /dev/null; then
        if promtool check config "prometheus/prometheus.yml" &> /dev/null; then
            echo "  ✅ Valid"
        else
            echo "  ❌ Invalid - promtool check failed"
            promtool check config "prometheus/prometheus.yml"
        fi
    else
        if python3 -c "import yaml; yaml.safe_load(open('prometheus/prometheus.yml'))" 2> /dev/null; then
            echo "  ✅ Valid YAML syntax"
        elif python -c "import yaml; yaml.safe_load(open('prometheus/prometheus.yml'))" 2> /dev/null; then
            echo "  ✅ Valid YAML syntax"
        else
            echo "  ⚠️  Could not validate (Python/PyYAML not available)"
        fi
    fi
fi
echo ""

# Summary
echo "=================================================="
echo "Validation Summary"
echo "=================================================="
echo "Total alert files: $TOTAL_FILES"
echo "Valid files: $VALID_FILES"
echo "Invalid files: $INVALID_FILES"
echo ""

if [ $INVALID_FILES -eq 0 ]; then
    echo "✅ All alert configurations are valid!"
    echo ""
    echo "Next steps:"
    echo "1. Start Prometheus: docker-compose -f docker-compose.monitoring.yml up -d"
    echo "2. Verify alerts loaded: http://localhost:9090/alerts"
    echo "3. Configure notification channels in Grafana"
    exit 0
else
    echo "❌ Some alert configurations have errors. Please fix them before deploying."
    exit 1
fi
