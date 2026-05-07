#!/bin/bash

#############################################
# ThinkOnErp API - Final Deployment Script
# Fixes Redis validation and deploys
#############################################

set -e  # Exit on any error

echo ""
echo "╔════════════════════════════════════════════════════════════╗"
echo "║     ThinkOnErp API - Final Deployment                     ║"
echo "║     Redis Validation Fix + Deploy                         ║"
echo "╚════════════════════════════════════════════════════════════╝"
echo ""

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Function to print colored output
print_success() {
    echo -e "${GREEN}✓ $1${NC}"
}

print_error() {
    echo -e "${RED}✗ $1${NC}"
}

print_info() {
    echo -e "${YELLOW}ℹ $1${NC}"
}

# Check if running on Linux
if [[ "$OSTYPE" != "linux-gnu"* ]]; then
    print_error "This script must be run on Linux server (Ubuntu)"
    exit 1
fi

print_info "Running on Linux - proceeding with deployment"
echo ""

# Step 1: Stop existing container
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "Step 1: Stopping existing container..."
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
docker-compose -f docker-compose.simple.yml down 2>/dev/null || true
print_success "Container stopped"
echo ""

# Step 2: Clean up old images
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "Step 2: Cleaning up old Docker images..."
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
docker rmi thinkonerp-thinkonerp-api:latest 2>/dev/null || print_info "No old image to remove"
print_success "Cleanup complete"
echo ""

# Step 3: Rebuild Docker image
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "Step 3: Building Docker image (this may take 3-5 minutes)..."
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
if docker-compose -f docker-compose.simple.yml build --no-cache thinkonerp-api; then
    print_success "Docker image built successfully"
else
    print_error "Docker build failed!"
    echo ""
    echo "Please check the error messages above and try again."
    exit 1
fi
echo ""

# Step 4: Start the container
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "Step 4: Starting ThinkOnErp API container..."
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
if docker-compose -f docker-compose.simple.yml up -d thinkonerp-api; then
    print_success "Container started"
else
    print_error "Failed to start container!"
    exit 1
fi
echo ""

# Step 5: Wait for application startup
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "Step 5: Waiting for application to initialize..."
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
for i in {1..10}; do
    echo -n "."
    sleep 1
done
echo ""
print_success "Wait complete"
echo ""

# Step 6: Check container status
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "Step 6: Container Status"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
docker ps -a | grep thinkonerp-api || print_error "Container not found!"
echo ""

# Step 7: Show application logs
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "Step 7: Application Logs (last 50 lines)"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
docker logs thinkonerp-thinkonerp-api --tail 50
echo ""

# Step 8: Check if application started successfully
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "Step 8: Checking Application Status"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

# Check if "Application started" appears in logs
if docker logs thinkonerp-thinkonerp-api 2>&1 | grep -q "Application started"; then
    print_success "Application started successfully!"
    echo ""
    print_success "API is running on: http://178.104.126.99:5000"
    print_success "Swagger UI: http://178.104.126.99:5000/swagger"
    print_success "Health Check: http://178.104.126.99:5000/health"
else
    print_error "Application may not have started correctly"
    echo ""
    print_info "Check the logs above for errors"
    print_info "Run 'docker logs -f thinkonerp-thinkonerp-api' to see live logs"
fi
echo ""

# Final summary
echo "╔════════════════════════════════════════════════════════════╗"
echo "║                  DEPLOYMENT COMPLETE                       ║"
echo "╚════════════════════════════════════════════════════════════╝"
echo ""
echo "📋 Quick Commands:"
echo "   View live logs:    docker logs -f thinkonerp-thinkonerp-api"
echo "   Stop container:    docker-compose -f docker-compose.simple.yml down"
echo "   Restart:           docker-compose -f docker-compose.simple.yml restart"
echo "   Check status:      docker ps | grep thinkonerp"
echo ""
echo "🧪 Test API:"
echo "   curl http://localhost:5000/health"
echo "   curl http://178.104.126.99:5000/health"
echo ""
echo "📊 Database: Oracle at 178.104.126.99:1521/XEPDB1"
echo "🔐 JWT: Configured with production key"
echo "📝 Audit: Enabled with encryption"
echo "🚫 Redis: Disabled (not required)"
echo ""
