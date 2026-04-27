#!/bin/bash
#
# Run the 1-hour sustained load test for the Full Traceability System.
#
# This script runs the sustained load test that validates the system can maintain
# 10,000 requests per minute for 1 hour without performance degradation.
#
# Usage:
#   ./run-sustained-load-test.sh
#   ./run-sustained-load-test.sh http://your-api:5000
#   ./run-sustained-load-test.sh http://your-api:5000 "your-jwt-token"
#   ./run-sustained-load-test.sh http://your-api:5000 "" ./test-results
#

# Default parameters
API_URL="${1:-http://localhost:5000}"
JWT_TOKEN="${2:-}"
OUTPUT_DIR="${3:-./results}"
SAVE_RESULTS=true

# Color codes
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
MAGENTA='\033[0;35m'
NC='\033[0m' # No Color

# Output functions
print_success() {
    echo -e "${GREEN}✅ $1${NC}"
}

print_info() {
    echo -e "${CYAN}ℹ️  $1${NC}"
}

print_warning() {
    echo -e "${YELLOW}⚠️  $1${NC}"
}

print_error() {
    echo -e "${RED}❌ $1${NC}"
}

print_header() {
    echo ""
    echo -e "${MAGENTA}═══════════════════════════════════════════════════════════${NC}"
    echo -e "${MAGENTA} $1${NC}"
    echo -e "${MAGENTA}═══════════════════════════════════════════════════════════${NC}"
    echo ""
}

# Check if k6 is installed
print_header "Sustained Load Test - Pre-flight Checks"

print_info "Checking for k6 installation..."
if ! command -v k6 &> /dev/null; then
    print_error "k6 is not installed or not in PATH"
    print_info "Please install k6 from: https://k6.io/docs/getting-started/installation/"
    echo ""
    print_info "Installation options:"
    print_info "  - macOS: brew install k6"
    print_info "  - Linux: See https://k6.io/docs/getting-started/installation/"
    print_info "  - Docker: docker pull grafana/k6:latest"
    exit 1
fi
K6_VERSION=$(k6 version)
print_success "k6 is installed: $K6_VERSION"

# Check if API is accessible
print_info "Checking API accessibility at $API_URL..."
if curl -s -f -o /dev/null --max-time 5 "$API_URL/health"; then
    print_success "API is accessible"
else
    print_warning "Could not reach API health endpoint at $API_URL/health"
    print_warning "The API might not be running or the URL might be incorrect"
    print_info "Continuing anyway - k6 will handle connection errors..."
fi

# Create output directory if it doesn't exist
if [ "$SAVE_RESULTS" = true ]; then
    if [ ! -d "$OUTPUT_DIR" ]; then
        mkdir -p "$OUTPUT_DIR"
        print_success "Created output directory: $OUTPUT_DIR"
    fi
fi

# Display test information
print_header "Sustained Load Test Configuration"

print_info "API URL: $API_URL"
if [ -n "$JWT_TOKEN" ]; then
    print_info "JWT Token: ***PROVIDED***"
else
    print_info "JWT Token: Will authenticate with default credentials"
fi
print_info "Output Directory: $OUTPUT_DIR"
print_info "Save Results: $SAVE_RESULTS"
echo ""
print_info "Test Parameters:"
print_info "  - Target Load: 10,000 requests/minute"
print_info "  - Sustained Duration: 60 minutes"
print_info "  - Total Duration: ~75 minutes"
print_info "  - Expected Requests: ~600,000"
echo ""
print_warning "This test will run for approximately 75 minutes"
print_warning "Ensure you have sufficient database capacity for ~600,000 audit log entries"
echo ""

# Confirm before starting
read -p "Do you want to proceed? (yes/no): " confirmation
if [ "$confirmation" != "yes" ]; then
    print_info "Test cancelled by user"
    exit 0
fi

# Prepare k6 command
print_header "Starting Sustained Load Test"

TIMESTAMP=$(date +"%Y%m%d-%H%M%S")
K6_ARGS=()

# Add environment variables
export API_URL
if [ -n "$JWT_TOKEN" ]; then
    export JWT_TOKEN
fi

# Add output file if requested
if [ "$SAVE_RESULTS" = true ]; then
    OUTPUT_FILE="$OUTPUT_DIR/sustained-load-$TIMESTAMP.json"
    LOG_FILE="$OUTPUT_DIR/sustained-load-$TIMESTAMP.log"
    K6_ARGS+=("--out" "json=$OUTPUT_FILE")
    print_info "Results will be saved to: $OUTPUT_FILE"
    print_info "Console output will be saved to: $LOG_FILE"
fi

# Add the test script
K6_ARGS+=("sustained-load-test-1hour.js")

print_info "Starting k6 load test..."
print_info "Command: k6 run ${K6_ARGS[*]}"
echo ""
print_warning "Test is now running. Do not interrupt unless absolutely necessary."
echo ""

# Run k6 and capture output
START_TIME=$(date +%s)

if [ "$SAVE_RESULTS" = true ]; then
    k6 run "${K6_ARGS[@]}" 2>&1 | tee "$LOG_FILE"
else
    k6 run "${K6_ARGS[@]}"
fi

EXIT_CODE=$?
END_TIME=$(date +%s)
DURATION=$((END_TIME - START_TIME))
DURATION_FORMATTED=$(printf '%02d:%02d:%02d' $((DURATION/3600)) $((DURATION%3600/60)) $((DURATION%60)))

# Display results
print_header "Test Completed"

print_info "Test Duration: $DURATION_FORMATTED"
print_info "End Time: $(date '+%Y-%m-%d %H:%M:%S')"

if [ $EXIT_CODE -eq 0 ]; then
    print_success "Test completed successfully!"
    echo ""
    print_info "Next Steps:"
    print_info "  1. Review the test results above"
    print_info "  2. Check for any threshold violations"
    print_info "  3. Analyze performance degradation metrics"
    print_info "  4. Verify database integrity"
    print_info "  5. Document results in task completion summary"
    
    if [ "$SAVE_RESULTS" = true ]; then
        echo ""
        print_info "Results saved to:"
        print_info "  - JSON: $OUTPUT_FILE"
        print_info "  - Log: $LOG_FILE"
    fi
else
    print_error "Test failed with exit code: $EXIT_CODE"
    echo ""
    print_info "Troubleshooting:"
    print_info "  1. Check the error messages above"
    print_info "  2. Verify API is running and accessible"
    print_info "  3. Check database connectivity"
    print_info "  4. Review application logs for errors"
    print_info "  5. Consult SUSTAINED_LOAD_TEST_GUIDE.md for detailed troubleshooting"
    
    if [ "$SAVE_RESULTS" = true ]; then
        echo ""
        print_info "Logs saved to: $LOG_FILE"
    fi
fi

echo ""
print_info "For detailed analysis and troubleshooting, see:"
print_info "  - SUSTAINED_LOAD_TEST_GUIDE.md"
print_info "  - README.md"

exit $EXIT_CODE
