#!/bin/bash
#
# Run spike load test for Full Traceability System (50,000 req/min burst)
#
# This script runs the spike load test that validates the system can handle
# sudden traffic bursts to 50,000 requests per minute and recover gracefully.
#
# Usage:
#   ./run-spike-load-test.sh
#   ./run-spike-load-test.sh http://your-api-url:port
#   ./run-spike-load-test.sh http://your-api-url:port "your-jwt-token"
#   ./run-spike-load-test.sh http://your-api-url:port "" spike-results.json

# Default configuration
API_URL="${1:-http://localhost:5000}"
JWT_TOKEN="${2:-}"
OUTPUT_FILE="${3:-}"

# Color output functions
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

print_header() {
    echo ""
    echo -e "${CYAN}========================================${NC}"
    echo -e "${CYAN}$1${NC}"
    echo -e "${CYAN}========================================${NC}"
    echo ""
}

print_success() {
    echo -e "${GREEN}✓ $1${NC}"
}

print_warning() {
    echo -e "${YELLOW}⚠ $1${NC}"
}

print_error() {
    echo -e "${RED}✗ $1${NC}"
}

# Check if k6 is installed
print_header "Spike Load Test - Prerequisites Check"

echo "Checking for k6 installation..."
if ! command -v k6 &> /dev/null; then
    print_error "k6 is not installed or not in PATH"
    echo ""
    echo "Please install k6 from: https://k6.io/docs/getting-started/installation/"
    echo ""
    echo "Installation options:"
    echo "  - macOS: brew install k6"
    echo "  - Linux: See https://k6.io/docs/getting-started/installation/"
    echo "  - Docker: docker pull grafana/k6:latest"
    exit 1
fi

K6_VERSION=$(k6 version | head -n 1)
print_success "k6 is installed: $K6_VERSION"

# Check if API is accessible
echo ""
echo "Checking API accessibility at $API_URL..."

if curl -s -f -o /dev/null -m 5 "$API_URL/api/monitoring/health"; then
    print_success "API is accessible and healthy"
else
    print_warning "Could not reach API health endpoint (this is optional)"
    echo "  Attempting to continue with load test..."
fi

# Display test configuration
print_header "Spike Load Test Configuration"

echo "API URL:           $API_URL"
if [ -n "$JWT_TOKEN" ]; then
    echo "JWT Token:         ***PROVIDED***"
else
    echo "JWT Token:         Will authenticate with default credentials"
fi

if [ -n "$OUTPUT_FILE" ]; then
    echo "Output File:       $OUTPUT_FILE"
else
    echo "Output File:       Console only"
fi

echo ""
echo "Test Profile:"
echo "  - Baseline:      5 minutes at 10,000 req/min"
echo "  - Spike:         3.5 minutes at 50,000 req/min"
echo "  - Recovery:      5 minutes at 10,000 req/min"
echo "  - Total Duration: ~17 minutes"
echo ""
echo "Expected Behavior:"
echo "  ✓ System handles spike without crashing"
echo "  ✓ Error rate during spike <5%"
echo "  ✓ Queue backpressure prevents memory exhaustion"
echo "  ✓ System recovers to normal performance after spike"

# Confirm before starting
echo ""
read -p "Ready to start spike load test? (y/N): " -n 1 -r
echo ""
if [[ ! $REPLY =~ ^[Yy]$ ]]; then
    echo "Test cancelled by user"
    exit 0
fi

# Build k6 command
print_header "Starting Spike Load Test"

K6_ARGS=("run")

# Add environment variables
if [ -n "$API_URL" ]; then
    K6_ARGS+=("--env" "API_URL=$API_URL")
fi

if [ -n "$JWT_TOKEN" ]; then
    K6_ARGS+=("--env" "JWT_TOKEN=$JWT_TOKEN")
fi

# Add output file if specified
if [ -n "$OUTPUT_FILE" ]; then
    K6_ARGS+=("--out" "json=$OUTPUT_FILE")
    echo "Results will be saved to: $OUTPUT_FILE"
fi

# Add the test script
K6_ARGS+=("spike-load-test-50k-rpm.js")

echo ""
echo "Executing: k6 ${K6_ARGS[*]}"
echo ""
print_warning "⏱ Test starting... (this will take approximately 17 minutes)"
echo ""

# Run the test
TEST_START_TIME=$(date +%s)

k6 "${K6_ARGS[@]}"
EXIT_CODE=$?

TEST_END_TIME=$(date +%s)
TEST_DURATION=$((TEST_END_TIME - TEST_START_TIME))
TEST_DURATION_MIN=$((TEST_DURATION / 60))
TEST_DURATION_SEC=$((TEST_DURATION % 60))

# Display results summary
print_header "Spike Load Test Complete"

printf "Test Duration: %02d:%02d\n" $TEST_DURATION_MIN $TEST_DURATION_SEC
echo ""

if [ $EXIT_CODE -eq 0 ]; then
    print_success "All thresholds passed!"
    echo ""
    echo "Key Validations:"
    print_success "System handled spike load (50,000 req/min)"
    print_success "Error rate remained acceptable (<5%)"
    print_success "System recovered to normal performance"
    print_success "Queue backpressure mechanisms worked correctly"
else
    print_warning "Some thresholds failed (exit code: $EXIT_CODE)"
    echo ""
    echo "Review the test output above for details on which thresholds failed."
    echo ""
    echo "Common issues:"
    echo "  - High error rate during spike (>5%)"
    echo "  - System did not recover after spike"
    echo "  - Queue depth exceeded memory limits"
    echo "  - Database connection pool exhaustion"
fi

echo ""
echo "Next Steps:"
echo "  1. Review the detailed metrics above"
echo "  2. Check application logs for errors or warnings"
echo "  3. Monitor database performance during spike"
echo "  4. Verify queue depth remained manageable"
echo "  5. Document results in task completion summary"

if [ -n "$OUTPUT_FILE" ]; then
    echo ""
    echo "Detailed results saved to: $OUTPUT_FILE"
fi

echo ""
exit $EXIT_CODE
