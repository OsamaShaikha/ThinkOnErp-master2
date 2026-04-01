#!/bin/bash

# ThinkOnErp API Simple Deployment Script (API Only)
# For use with existing Oracle database

set -e

echo "=========================================="
echo "ThinkOnErp API Deployment (API Only)"
echo "=========================================="

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Function to print colored output
print_info() {
    echo -e "${GREEN}[INFO]${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

print_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

print_step() {
    echo -e "${BLUE}[STEP]${NC} $1"
}

# Check if running as root
if [ "$EUID" -eq 0 ]; then 
    print_warning "Please do not run as root. Run as a regular user with sudo privileges."
    exit 1
fi

# Check if Docker is installed
print_step "Checking Docker installation..."
if ! command -v docker &> /dev/null; then
    print_error "Docker is not installed."
    echo ""
    echo "To install Docker, run:"
    echo "  curl -fsSL https://get.docker.com -o get-docker.sh"
    echo "  sudo sh get-docker.sh"
    echo "  sudo usermod -aG docker \$USER"
    echo ""
    echo "Then log out and log back in."
    exit 1
fi
print_info "Docker is installed: $(docker --version)"

# Check if Docker Compose is installed
print_step "Checking Docker Compose installation..."
if ! command -v docker-compose &> /dev/null; then
    print_error "Docker Compose is not installed."
    echo ""
    echo "To install Docker Compose, run:"
    echo "  sudo curl -L \"https://github.com/docker/compose/releases/latest/download/docker-compose-\$(uname -s)-\$(uname -m)\" -o /usr/local/bin/docker-compose"
    echo "  sudo chmod +x /usr/local/bin/docker-compose"
    exit 1
fi
print_info "Docker Compose is installed: $(docker-compose --version)"

# Check if .env file exists
print_step "Checking environment configuration..."
if [ ! -f .env ]; then
    print_warning ".env file not found. Creating from .env.example..."
    cp .env.example .env
    echo ""
    print_error "IMPORTANT: You must configure .env file before deployment!"
    echo ""
    echo "Please edit .env and update:"
    echo "  1. ORACLE_CONNECTION_STRING - Your Oracle database connection"
    echo "  2. JWT_SECRET_KEY - A secure secret key (at least 32 characters)"
    echo ""
    echo "Example:"
    echo "  nano .env"
    echo ""
    read -p "Press Enter after you've configured .env file..."
fi

# Validate .env file
print_step "Validating configuration..."
source .env

if [[ "$ORACLE_CONNECTION_STRING" == *"your-oracle-host"* ]] || [[ "$ORACLE_CONNECTION_STRING" == *"your_user"* ]]; then
    print_error "Oracle connection string is not configured!"
    echo "Please edit .env file and set ORACLE_CONNECTION_STRING"
    exit 1
fi

if [[ "$JWT_SECRET_KEY" == *"your-secret-key"* ]] || [ ${#JWT_SECRET_KEY} -lt 32 ]; then
    print_error "JWT secret key is not configured or too short!"
    echo "Please edit .env file and set a secure JWT_SECRET_KEY (at least 32 characters)"
    exit 1
fi

print_info "Configuration validated successfully"

# Test Oracle connection (optional)
echo ""
read -p "Do you want to test Oracle database connection? (y/n): " test_db
if [[ "$test_db" == "y" || "$test_db" == "Y" ]]; then
    print_step "Testing Oracle connection..."
    # This would require sqlplus or similar tool
    print_warning "Manual Oracle connection test recommended before deployment"
fi

# Stop existing containers
print_step "Stopping existing containers..."
docker-compose -f docker-compose.simple.yml down 2>/dev/null || true

# Build the application
print_step "Building Docker image..."
echo "This may take a few minutes..."
docker-compose -f docker-compose.simple.yml build --no-cache

if [ $? -ne 0 ]; then
    print_error "Docker build failed!"
    exit 1
fi

print_info "Docker image built successfully"

# Start the service
print_step "Starting ThinkOnErp API..."
docker-compose -f docker-compose.simple.yml up -d

if [ $? -ne 0 ]; then
    print_error "Failed to start service!"
    exit 1
fi

# Wait for service to be healthy
print_step "Waiting for API to be ready..."
sleep 5

# Check service status
print_info "Checking service status..."
docker-compose -f docker-compose.simple.yml ps

# Test API health
print_step "Testing API health endpoint..."
sleep 3

API_PORT=${API_PORT:-5000}
MAX_RETRIES=10
RETRY_COUNT=0

while [ $RETRY_COUNT -lt $MAX_RETRIES ]; do
    if curl -f http://localhost:$API_PORT/health &> /dev/null; then
        print_info "✓ API is healthy and responding!"
        break
    else
        RETRY_COUNT=$((RETRY_COUNT + 1))
        if [ $RETRY_COUNT -lt $MAX_RETRIES ]; then
            echo "Waiting for API to be ready... ($RETRY_COUNT/$MAX_RETRIES)"
            sleep 3
        fi
    fi
done

if [ $RETRY_COUNT -eq $MAX_RETRIES ]; then
    print_warning "API health check failed. Check logs with: docker-compose -f docker-compose.simple.yml logs"
fi

# Display deployment information
echo ""
echo "=========================================="
echo "✓ Deployment Complete!"
echo "=========================================="
echo ""
echo "Service Information:"
echo "  API URL:        http://localhost:$API_PORT"
echo "  Swagger UI:     http://localhost:$API_PORT/swagger"
echo "  Health Check:   http://localhost:$API_PORT/health"
echo ""
echo "Test Login:"
echo "  curl -X POST http://localhost:$API_PORT/api/auth/login \\"
echo "    -H \"Content-Type: application/json\" \\"
echo "    -d '{\"userName\":\"admin\",\"password\":\"Admin@123\"}'"
echo ""
echo "Useful Commands:"
echo "  View logs:      docker-compose -f docker-compose.simple.yml logs -f"
echo "  Stop service:   docker-compose -f docker-compose.simple.yml down"
echo "  Restart:        docker-compose -f docker-compose.simple.yml restart"
echo "  Rebuild:        docker-compose -f docker-compose.simple.yml up -d --build"
echo ""
echo "=========================================="

# Offer to show logs
echo ""
read -p "Do you want to view the logs now? (y/n): " show_logs
if [[ "$show_logs" == "y" || "$show_logs" == "Y" ]]; then
    docker-compose -f docker-compose.simple.yml logs -f
fi
