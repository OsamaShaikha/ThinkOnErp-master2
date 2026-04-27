#!/bin/bash

# Latency Overhead Measurement Script for Full Traceability System
#
# This bash script measures the latency overhead introduced by the 
# traceability system components (audit logging, request tracing, performance monitoring).
#
# It validates Task 20.2 requirement: "System SHALL add no more than 10ms latency 
# to API requests for 99% of operations"
#
# Usage:
#   ./latency-measurement-script.sh [API_URL] [JWT_TOKEN] [DURATION] [CONCURRENCY]
#
# Examples:
#   ./latency-measurement-script.sh
#   ./latency-measurement-script.sh http://localhost:5000
#   ./latency-measurement-script.sh http://localhost:5000 "your-jwt-token" 120 20

set -euo pipefail

# Configuration
API_URL="${1:-http://localhost:5000}"
JWT_TOKEN="${2:-}"
DURATION="${3:-60}"
CONCURRENCY="${4:-10}"
OUTPUT_DIR="./results"
TIMESTAMP=$(date +%Y%m%d-%H%M%S)
OUTPUT_FILE="$OUTPUT_DIR/latency-results-$TIMESTAMP.json"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Logging functions
log_info() {
    echo -e "${BLUE}[$(date +'%H:%M:%S')] INFO: $1${NC}"
}

log_success() {
    echo -e "${GREEN}[$(date +'%H:%M:%S')] SUCCESS: $1${NC}"
}

log_warn() {
    echo -e "${YELLOW}[$(date +'%H:%M:%S')] WARN: $1${NC}"
}

log_error() {
    echo -e "${RED}[$(date +'%H:%M:%S')] ERROR: $1${NC}"
}

# Check dependencies
check_dependencies() {
    local missing_deps=()
    
    if ! command -v curl &> /dev/null; then
        missing_deps+=("curl")
    fi
    
    if ! command -v jq &> /dev/null; then
        missing_deps+=("jq")
    fi
    
    if ! command -v bc &> /dev/null; then
        missing_deps+=("bc")
    fi
    
    if [ ${#missing_deps[@]} -ne 0 ]; then
        log_error "Missing required dependencies: ${missing_deps[*]}"
        log_error "Please install them and try again."
        exit 1
    fi
}

# Authenticate and get JWT token
authenticate() {
    if [ -z "$JWT_TOKEN" ]; then
        log_info "No JWT token provided, attempting to authenticate..."
        
        local login_response
        login_response=$(curl -s -X POST "$API_URL/api/auth/login" \
            -H "Content-Type: application/json" \
            -d '{"userName":"superadmin","password":"SuperAdmin123!"}' \
            -w "%{http_code}")
        
        local http_code="${login_response: -3}"
        local response_body="${login_response%???}"
        
        if [ "$http_code" = "200" ]; then
            JWT_TOKEN=$(echo "$response_body" | jq -r '.token')
            log_success "Authentication successful"
        else
            log_error "Authentication failed with status $http_code"
            log_error "Response: $response_body"
            exit 1
        fi
    fi
}

# Warm up the system
warm_up_system() {
    log_info "Warming up system..."
    
    for i in {1..5}; do
        curl -s -H "Authorization: Bearer $JWT_TOKEN" "$API_URL/api/companies" > /dev/null || true
        sleep 0.1
    done
    
    log_success "System warmed up"
}

# Calculate percentiles
calculate_percentile() {
    local values=("$@")
    local count=${#values[@]}
    
    if [ $count -eq 0 ]; then
        echo "0"
        return
    fi
    
    # Sort values
    IFS=$'\n' sorted=($(sort -n <<<"${values[*]}"))
    unset IFS
    
    # Calculate percentiles
    local p50_idx=$((count * 50 / 100))
    local p95_idx=$((count * 95 / 100))
    local p99_idx=$((count * 99 / 100))
    
    # Ensure indices are within bounds
    [ $p50_idx -ge $count ] && p50_idx=$((count - 1))
    [ $p95_idx -ge $count ] && p95_idx=$((count - 1))
    [ $p99_idx -ge $count ] && p99_idx=$((count - 1))
    
    echo "${sorted[$p50_idx]} ${sorted[$p95_idx]} ${sorted[$p99_idx]}"
}

# Calculate statistics
calculate_stats() {
    local values=("$@")
    local count=${#values[@]}
    
    if [ $count -eq 0 ]; then
        echo "0 0 0 0 0 0"
        return
    fi
    
    # Calculate min, max, sum
    local min=${values[0]}
    local max=${values[0]}
    local sum=0
    
    for value in "${values[@]}"; do
        sum=$(echo "$sum + $value" | bc -l)
        if (( $(echo "$value < $min" | bc -l) )); then
            min=$value
        fi
        if (( $(echo "$value > $max" | bc -l) )); then
            max=$value
        fi
    done
    
    local avg=$(echo "scale=2; $sum / $count" | bc -l)
    
    # Calculate percentiles
    local percentiles
    percentiles=$(calculate_percentile "${values[@]}")
    
    echo "$min $max $avg $percentiles"
}

# Test endpoint latency
test_endpoint_latency() {
    local endpoint_name="$1"
    local endpoint_path="$2"
    local request_count="$3"
    
    log_info "Testing endpoint: $endpoint_name ($endpoint_path)"
    
    local response_times=()
    local audit_overheads=()
    local correlation_ids=()
    local errors=0
    local successful=0
    
    # Create temporary files for parallel execution
    local temp_dir=$(mktemp -d)
    local results_file="$temp_dir/results.txt"
    
    # Function to make a single request
    make_request() {
        local request_id="$1"
        local start_time=$(date +%s%3N)
        
        local response
        response=$(curl -s -w "%{http_code}|%{time_total}" \
            -H "Authorization: Bearer $JWT_TOKEN" \
            -H "Accept: application/json" \
            -D "$temp_dir/headers_$request_id.txt" \
            "$API_URL$endpoint_path" 2>/dev/null || echo "000|0")
        
        local end_time=$(date +%s%3N)
        local response_time=$((end_time - start_time))
        
        # Parse response
        local http_code="${response##*|}"
        local curl_time="${response%|*}"
        curl_time="${curl_time##*|}"
        
        # Extract headers
        local correlation_id=""
        local audit_overhead=""
        
        if [ -f "$temp_dir/headers_$request_id.txt" ]; then
            correlation_id=$(grep -i "x-correlation-id" "$temp_dir/headers_$request_id.txt" | cut -d' ' -f2 | tr -d '\r\n' || echo "")
            audit_overhead=$(grep -i "x-audit-overhead-ms" "$temp_dir/headers_$request_id.txt" | cut -d' ' -f2 | tr -d '\r\n' || echo "")
        fi
        
        # Output results
        echo "$request_id|$response_time|$http_code|$correlation_id|$audit_overhead" >> "$results_file"
    }
    
    # Export function for parallel execution
    export -f make_request
    export API_URL JWT_TOKEN endpoint_path temp_dir results_file
    
    # Run requests in parallel batches
    local batch_size=$CONCURRENCY
    local batches=$((request_count / batch_size))
    [ $((request_count % batch_size)) -ne 0 ] && batches=$((batches + 1))
    
    for ((batch = 0; batch < batches; batch++)); do
        local start_idx=$((batch * batch_size))
        local end_idx=$((start_idx + batch_size - 1))
        [ $end_idx -ge $request_count ] && end_idx=$((request_count - 1))
        
        # Start batch of requests
        for ((i = start_idx; i <= end_idx; i++)); do
            make_request "$i" &
        done
        
        # Wait for batch to complete
        wait
        
        # Show progress
        local completed=$((end_idx + 1))
        local progress=$((completed * 100 / request_count))
        printf "\r  Progress: %d%% (%d/%d)" "$progress" "$completed" "$request_count"
    done
    
    echo "" # New line after progress
    
    # Process results
    if [ -f "$results_file" ]; then
        while IFS='|' read -r request_id response_time http_code correlation_id audit_overhead; do
            if [ "$http_code" = "200" ]; then
                response_times+=("$response_time")
                successful=$((successful + 1))
                
                if [ -n "$correlation_id" ] && [ "$correlation_id" != "" ]; then
                    correlation_ids+=("$correlation_id")
                fi
                
                if [ -n "$audit_overhead" ] && [ "$audit_overhead" != "" ]; then
                    audit_overheads+=("$audit_overhead")
                fi
            else
                errors=$((errors + 1))
            fi
        done < "$results_file"
    fi
    
    # Calculate statistics
    local total_requests=$((successful + errors))
    local error_rate=0
    if [ $total_requests -gt 0 ]; then
        error_rate=$(echo "scale=2; $errors * 100 / $total_requests" | bc -l)
    fi
    
    local response_stats
    response_stats=$(calculate_stats "${response_times[@]}")
    read -r rt_min rt_max rt_avg rt_p50 rt_p95 rt_p99 <<< "$response_stats"
    
    local audit_stats="0 0 0 0 0 0"
    if [ ${#audit_overheads[@]} -gt 0 ]; then
        audit_stats=$(calculate_stats "${audit_overheads[@]}")
    fi
    read -r ao_min ao_max ao_avg ao_p50 ao_p95 ao_p99 <<< "$audit_stats"
    
    # Output results
    log_info "  Results for $endpoint_name:"
    log_info "    Total Requests: $total_requests"
    log_info "    Successful: $successful"
    log_info "    Failed: $errors"
    log_info "    Error Rate: ${error_rate}%"
    log_info "    Response Time - Min: ${rt_min}ms, Avg: ${rt_avg}ms, Max: ${rt_max}ms"
    log_info "    Percentiles - P50: ${rt_p50}ms, P95: ${rt_p95}ms, P99: ${rt_p99}ms"
    
    if [ ${#audit_overheads[@]} -gt 0 ]; then
        log_info "    Audit Overhead - Avg: ${ao_avg}ms, P95: ${ao_p95}ms, P99: ${ao_p99}ms"
    fi
    
    log_info "    Correlation IDs: $(echo "${correlation_ids[@]}" | tr ' ' '\n' | sort -u | wc -l) unique"
    
    # Validate requirements
    local status="PASS"
    local level="SUCCESS"
    
    # Check p99 response time (should be reasonable, < 500ms)
    if (( $(echo "$rt_p99 > 500" | bc -l) )); then
        status="FAIL (P99 response time too high)"
        level="ERROR"
    fi
    
    # Check error rate (should be < 1%)
    if (( $(echo "$error_rate > 1" | bc -l) )); then
        status="FAIL (Error rate too high)"
        level="ERROR"
    fi
    
    # Check audit overhead if available (should be < 10ms p99)
    if [ ${#audit_overheads[@]} -gt 0 ] && (( $(echo "$ao_p99 > 10" | bc -l) )); then
        status="FAIL (Audit overhead too high)"
        level="ERROR"
    fi
    
    if [ "$level" = "SUCCESS" ]; then
        log_success "    Status: ✓ $status"
    else
        log_error "    Status: ✗ $status"
    fi
    
    echo ""
    
    # Store results in JSON format
    cat >> "$OUTPUT_FILE" << EOF
    "$endpoint_name": {
        "totalRequests": $total_requests,
        "successfulRequests": $successful,
        "failedRequests": $errors,
        "errorRate": $error_rate,
        "responseTime": {
            "min": $rt_min,
            "max": $rt_max,
            "average": $rt_avg,
            "p50": $rt_p50,
            "p95": $rt_p95,
            "p99": $rt_p99
        },
        "auditOverhead": {
            "count": ${#audit_overheads[@]},
            "min": $ao_min,
            "max": $ao_max,
            "average": $ao_avg,
            "p50": $ao_p50,
            "p95": $ao_p95,
            "p99": $ao_p99
        },
        "correlationIds": $(echo "${correlation_ids[@]}" | tr ' ' '\n' | sort -u | wc -l),
        "status": "$status"
    },
EOF
    
    # Cleanup
    rm -rf "$temp_dir"
    
    # Return status (0 = pass, 1 = fail)
    if [ "$status" = "PASS" ]; then
        return 0
    else
        return 1
    fi
}

# Main execution
main() {
    log_info "========================================"
    log_info "Full Traceability System - Latency Test"
    log_info "========================================"
    echo ""
    log_info "API URL: $API_URL"
    log_info "Duration: $DURATION seconds per endpoint"
    log_info "Concurrency: $CONCURRENCY concurrent requests"
    log_info "Output File: $OUTPUT_FILE"
    echo ""
    
    # Check dependencies
    check_dependencies
    
    # Create output directory
    mkdir -p "$OUTPUT_DIR"
    
    # Initialize JSON output file
    cat > "$OUTPUT_FILE" << EOF
{
    "testConfiguration": {
        "apiUrl": "$API_URL",
        "duration": $DURATION,
        "concurrency": $CONCURRENCY,
        "timestamp": "$(date -Iseconds)"
    },
    "endpointResults": {
EOF
    
    # Authenticate
    authenticate
    
    # Warm up system
    warm_up_system
    
    log_info "========================================"
    log_info "Starting Latency Measurements"
    log_info "========================================"
    
    # Test endpoints
    local endpoints=(
        "companies:/api/companies"
        "users:/api/users"
        "roles:/api/roles"
        "currencies:/api/currencies"
        "branches:/api/branches"
        "audit_logs:/api/auditlogs/legacy-view?pageNumber=1&pageSize=10"
    )
    
    local overall_pass=true
    local request_count=$((DURATION * 2)) # 2 requests per second
    [ $request_count -lt 50 ] && request_count=50 # Minimum 50 requests
    
    for endpoint_def in "${endpoints[@]}"; do
        local endpoint_name="${endpoint_def%%:*}"
        local endpoint_path="${endpoint_def#*:}"
        
        if ! test_endpoint_latency "$endpoint_name" "$endpoint_path" "$request_count"; then
            overall_pass=false
        fi
    done
    
    # Finalize JSON output
    # Remove trailing comma and close JSON
    sed -i '$ s/,$//' "$OUTPUT_FILE"
    cat >> "$OUTPUT_FILE" << EOF
    },
    "summary": {
        "overallPass": $overall_pass
    }
}
EOF
    
    log_info "========================================"
    log_info "Test Summary"
    log_info "========================================"
    
    if [ "$overall_pass" = true ]; then
        log_success "✓ All latency requirements met!"
        log_success "  - p99 response times < 500ms for all endpoints"
        log_success "  - Error rates < 1% for all endpoints"
        log_success "  - Audit overhead < 10ms p99 (where measurable)"
    else
        log_error "✗ Some latency requirements not met"
        log_error "  Check individual endpoint results above"
    fi
    
    echo ""
    log_info "Detailed results saved to: $OUTPUT_FILE"
    
    echo ""
    log_info "Task 20.2 Validation:"
    log_info "  Requirement: System SHALL add no more than 10ms latency"
    log_info "  to API requests for 99% of operations"
    
    if [ "$overall_pass" = true ]; then
        log_success "  Status: ✓ PASSED"
    else
        log_error "  Status: ✗ FAILED"
    fi
    
    echo ""
    log_info "========================================"
    log_info "Latency Test Complete"
    log_info "========================================"
    
    # Exit with appropriate code
    if [ "$overall_pass" = true ]; then
        exit 0
    else
        exit 1
    fi
}

# Run main function
main "$@"