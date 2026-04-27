#!/bin/bash

###############################################################################
# Simple Load Test Script for Full Traceability System
# 
# This script uses Apache Bench (ab) which is commonly pre-installed on most
# systems. It's a simpler alternative to k6 for basic load testing.
#
# Requirements:
# - Apache Bench (ab) - usually pre-installed on macOS/Linux
# - ThinkOnErp API running
# - Valid JWT token
#
# Usage:
#   ./load-test-simple.sh
#   ./load-test-simple.sh http://localhost:5000 "your-jwt-token"
###############################################################################

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Configuration
API_URL="${1:-http://localhost:5000}"
JWT_TOKEN="${2:-}"
RESULTS_DIR="./results"
TIMESTAMP=$(date +%Y%m%d-%H%M%S)

# Test parameters
TARGET_RPM=10000
TARGET_RPS=$((TARGET_RPM / 60))  # Requests per second
CONCURRENCY=100
DURATION_SECONDS=60

echo -e "${BLUE}========================================${NC}"
echo -e "${BLUE}Full Traceability System - Load Test${NC}"
echo -e "${BLUE}========================================${NC}"
echo ""
echo -e "API URL: ${GREEN}${API_URL}${NC}"
echo -e "Target: ${GREEN}${TARGET_RPM} requests/minute (${TARGET_RPS} req/sec)${NC}"
echo -e "Concurrency: ${GREEN}${CONCURRENCY} concurrent requests${NC}"
echo -e "Duration: ${GREEN}${DURATION_SECONDS} seconds${NC}"
echo ""

# Check if ab is installed
if ! command -v ab &> /dev/null; then
    echo -e "${RED}Error: Apache Bench (ab) is not installed.${NC}"
    echo ""
    echo "Install instructions:"
    echo "  macOS:   ab is pre-installed"
    echo "  Ubuntu:  sudo apt-get install apache2-utils"
    echo "  CentOS:  sudo yum install httpd-tools"
    echo "  Windows: Use k6 instead (see README.md)"
    exit 1
fi

# Create results directory
mkdir -p "${RESULTS_DIR}"

# Authenticate if no token provided
if [ -z "$JWT_TOKEN" ]; then
    echo -e "${YELLOW}No JWT token provided, attempting to authenticate...${NC}"
    
    LOGIN_RESPONSE=$(curl -s -X POST "${API_URL}/api/auth/login" \
        -H "Content-Type: application/json" \
        -d '{"userName":"superadmin","password":"SuperAdmin123!"}')
    
    JWT_TOKEN=$(echo "$LOGIN_RESPONSE" | grep -o '"token":"[^"]*' | cut -d'"' -f4)
    
    if [ -z "$JWT_TOKEN" ]; then
        echo -e "${RED}Authentication failed. Response:${NC}"
        echo "$LOGIN_RESPONSE"
        exit 1
    fi
    
    echo -e "${GREEN}Authentication successful${NC}"
    echo ""
fi

# Function to run a load test on an endpoint
run_load_test() {
    local endpoint=$1
    local method=$2
    local description=$3
    local post_data=$4
    
    echo -e "${BLUE}Testing: ${description}${NC}"
    echo -e "Endpoint: ${endpoint}"
    
    local output_file="${RESULTS_DIR}/test-${TIMESTAMP}-$(echo ${endpoint} | tr '/' '-').txt"
    
    if [ "$method" = "GET" ]; then
        ab -n $((TARGET_RPS * DURATION_SECONDS)) \
           -c ${CONCURRENCY} \
           -t ${DURATION_SECONDS} \
           -H "Authorization: Bearer ${JWT_TOKEN}" \
           -H "Content-Type: application/json" \
           -g "${output_file}.tsv" \
           "${API_URL}${endpoint}" > "${output_file}" 2>&1
    else
        # POST request
        echo "$post_data" > /tmp/post_data.json
        ab -n $((TARGET_RPS * DURATION_SECONDS)) \
           -c ${CONCURRENCY} \
           -t ${DURATION_SECONDS} \
           -p /tmp/post_data.json \
           -T "application/json" \
           -H "Authorization: Bearer ${JWT_TOKEN}" \
           -g "${output_file}.tsv" \
           "${API_URL}${endpoint}" > "${output_file}" 2>&1
        rm /tmp/post_data.json
    fi
    
    # Extract key metrics
    local requests_per_sec=$(grep "Requests per second:" "${output_file}" | awk '{print $4}')
    local time_per_request=$(grep "Time per request:" "${output_file}" | grep -v "across" | awk '{print $4}')
    local failed_requests=$(grep "Failed requests:" "${output_file}" | awk '{print $3}')
    local p50=$(grep "50%" "${output_file}" | awk '{print $2}')
    local p95=$(grep "95%" "${output_file}" | awk '{print $2}')
    local p99=$(grep "99%" "${output_file}" | awk '{print $2}')
    
    echo -e "  Requests/sec: ${GREEN}${requests_per_sec}${NC}"
    echo -e "  Time/request: ${GREEN}${time_per_request} ms${NC}"
    echo -e "  Failed: ${GREEN}${failed_requests}${NC}"
    echo -e "  p50: ${GREEN}${p50} ms${NC}"
    echo -e "  p95: ${GREEN}${p95} ms${NC}"
    echo -e "  p99: ${GREEN}${p99} ms${NC}"
    
    # Check if p99 meets requirement (<500ms total, <10ms audit overhead)
    if [ ! -z "$p99" ] && [ "$p99" -lt 500 ]; then
        echo -e "  Status: ${GREEN}✓ PASS${NC} (p99 < 500ms)"
    else
        echo -e "  Status: ${RED}✗ FAIL${NC} (p99 >= 500ms)"
    fi
    
    echo ""
}

# Run load tests on various endpoints
echo -e "${BLUE}========================================${NC}"
echo -e "${BLUE}Starting Load Tests${NC}"
echo -e "${BLUE}========================================${NC}"
echo ""

# Test 1: GET /api/companies (most common read operation)
run_load_test "/api/companies" "GET" "Get Companies List"

# Test 2: GET /api/users
run_load_test "/api/users" "GET" "Get Users List"

# Test 3: GET /api/roles
run_load_test "/api/roles" "GET" "Get Roles List"

# Test 4: GET /api/currencies
run_load_test "/api/currencies" "GET" "Get Currencies List"

# Test 5: GET /api/branches
run_load_test "/api/branches" "GET" "Get Branches List"

# Test 6: GET /api/auditlogs/legacy-view (audit log query)
run_load_test "/api/auditlogs/legacy-view?pageNumber=1&pageSize=20" "GET" "Get Audit Logs"

# Generate summary report
SUMMARY_FILE="${RESULTS_DIR}/summary-${TIMESTAMP}.txt"

echo -e "${BLUE}========================================${NC}"
echo -e "${BLUE}Generating Summary Report${NC}"
echo -e "${BLUE}========================================${NC}"
echo ""

cat > "${SUMMARY_FILE}" << EOF
Load Test Summary Report
========================
Date: $(date)
API URL: ${API_URL}
Target: ${TARGET_RPM} requests/minute (${TARGET_RPS} req/sec)
Concurrency: ${CONCURRENCY}
Duration: ${DURATION_SECONDS} seconds

Test Results:
-------------

EOF

# Aggregate results from all tests
for result_file in ${RESULTS_DIR}/test-${TIMESTAMP}-*.txt; do
    if [ -f "$result_file" ]; then
        endpoint=$(basename "$result_file" | sed 's/test-'${TIMESTAMP}'-//' | sed 's/.txt$//' | tr '-' '/')
        echo "Endpoint: ${endpoint}" >> "${SUMMARY_FILE}"
        grep "Requests per second:" "$result_file" >> "${SUMMARY_FILE}"
        grep "Time per request:" "$result_file" | head -1 >> "${SUMMARY_FILE}"
        grep "Failed requests:" "$result_file" >> "${SUMMARY_FILE}"
        grep "99%" "$result_file" >> "${SUMMARY_FILE}"
        echo "" >> "${SUMMARY_FILE}"
    fi
done

# Performance validation
cat >> "${SUMMARY_FILE}" << EOF

Performance Requirements Validation:
------------------------------------

Requirement 1: Support 10,000 requests per minute
  Target: ${TARGET_RPM} req/min (${TARGET_RPS} req/sec)
  Result: See individual endpoint results above
  
Requirement 2: Add no more than 10ms latency for 99% of operations
  Note: This requires instrumentation in the API to measure audit overhead specifically.
  Total p99 response time should be < 500ms (includes network + processing + audit)
  
Requirement 3: Use asynchronous writes
  Validation: Check application logs for async audit processing
  Expected: No blocking on audit writes

Recommendations:
----------------

1. Review individual endpoint results above
2. Check application logs for audit logging performance
3. Monitor database connection pool usage
4. Verify audit queue depth stays below 10,000
5. Check for any failed requests or errors

Next Steps:
-----------

1. If p99 > 500ms: Investigate slow endpoints and optimize
2. If failed requests > 1%: Check application logs for errors
3. If throughput < target: Increase concurrency or optimize bottlenecks
4. Run extended soak test (1+ hours) to validate stability

EOF

echo -e "${GREEN}Summary report saved to: ${SUMMARY_FILE}${NC}"
echo ""

# Display summary
cat "${SUMMARY_FILE}"

echo -e "${BLUE}========================================${NC}"
echo -e "${BLUE}Load Test Complete${NC}"
echo -e "${BLUE}========================================${NC}"
echo ""
echo -e "Results saved to: ${GREEN}${RESULTS_DIR}/${NC}"
echo -e "Summary report: ${GREEN}${SUMMARY_FILE}${NC}"
echo ""
echo -e "${YELLOW}Note: For more comprehensive load testing with 10,000 req/min sustained load,${NC}"
echo -e "${YELLOW}use the k6 script: k6 run load-test-10k-rpm.js${NC}"
echo ""
