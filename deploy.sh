#!/bin/bash

# ThinkOnErp API Deployment Script for Ubuntu
# This script automates the deployment process

set -e

echo "=========================================="
echo "ThinkOnErp API Deployment Script"
echo "=========================================="

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
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

# Check if running as root
if [ "$EUID" -eq 0 ]; then 
    print_warning "Please do not run as root. Run as a regular user with sudo privileges."
    exit 1
fi

# Check if Docker is installed
if ! command -v docker &> /dev/null; then
    print_error "Docker is not installed. Installing Docker..."
    curl -fsSL https://get.docker.com -o get-docker.sh
    sudo sh get-docker.sh
    sudo usermod -aG docker $USER
    print_info "Docker installed. Please log out and log back in for group changes to take effect."
    exit 0
fi

# Check if Docker Compose is installed
if ! command -v docker-compose &> /dev/null; then
    print_error "Docker Compose is not installed. Installing Docker Compose..."
    sudo curl -L "https://github.com/docker/compose/releases/latest/download/docker-compose-$(uname -s)-$(uname -m)" -o /usr/local/bin/docker-compose
    sudo chmod +x /usr/local/bin/docker-compose
    print_info "Docker Compose installed successfully."
fi

# Check if .env file exists
if [ ! -f .env ]; then
    print_warning ".env file not found. Creating from .env.example..."
    cp .env.example .env
    print_warning "Please edit .env file with your configuration before continuing."
    read -p "Press Enter to continue after editing .env file..."
fi

# Ask deployment type
echo ""
echo "Select deployment type:"
echo "1) Development (with Oracle database in Docker)"
echo "2) Production (external Oracle database)"
read -p "Enter choice [1-2]: " deployment_choice

case $deployment_choice in
    1)
        print_info "Starting development deployment..."
        COMPOSE_FILE="docker-compose.yml"
        ;;
    2)
        print_info "Starting production deployment..."
        COMPOSE_FILE="docker-compose.prod.yml"
        ;;
    *)
        print_error "Invalid choice. Exiting."
        exit 1
        ;;
esac

# Stop existing containers
print_info "Stopping existing containers..."
docker-compose -f $COMPOSE_FILE down

# Build the application
print_info "Building Docker image..."
docker-compose -f $COMPOSE_FILE build --no-cache

# Start the services
print_info "Starting services..."
docker-compose -f $COMPOSE_FILE up -d

# Wait for services to be healthy
print_info "Waiting for services to be healthy..."
sleep 10

# Check service status
print_info "Checking service status..."
docker-compose -f $COMPOSE_FILE ps

# Display logs
echo ""
print_info "Deployment complete!"
echo ""
echo "=========================================="
echo "Service Information:"
echo "=========================================="
echo "API URL: http://localhost:5000"
echo "Swagger UI: http://localhost:5000/swagger (Development only)"
echo "Health Check: http://localhost:5000/health"
echo ""
echo "To view logs:"
echo "  docker-compose -f $COMPOSE_FILE logs -f thinkonerp-api"
echo ""
echo "To stop services:"
echo "  docker-compose -f $COMPOSE_FILE down"
echo ""
echo "To restart services:"
echo "  docker-compose -f $COMPOSE_FILE restart"
echo "=========================================="

# Test API health
print_info "Testing API health endpoint..."
sleep 5
if curl -f http://localhost:5000/health &> /dev/null; then
    print_info "API is healthy and responding!"
else
    print_warning "API health check failed. Check logs with: docker-compose -f $COMPOSE_FILE logs thinkonerp-api"
fi
