@echo off
REM ThinkOnErp API - Deploy on Port 5001 (Windows)
REM This script deploys the ThinkOnErp API on port 5001

echo ==========================================
echo ThinkOnErp API - Port 5001 Deployment
echo ==========================================
echo.

REM Check if Docker is running
docker info >nul 2>&1
if errorlevel 1 (
    echo Error: Docker is not running
    echo Please start Docker Desktop first
    pause
    exit /b 1
)

REM Check if .env.port5001 exists
if not exist .env.port5001 (
    echo Error: .env.port5001 file not found
    echo Please create .env.port5001 with your configuration
    pause
    exit /b 1
)

REM Copy environment file
echo Copying environment configuration...
copy /Y .env.port5001 .env.production

REM Stop existing container if running
echo Stopping existing container (if any)...
docker-compose -f docker-compose.port5001.yml down 2>nul

REM Build the image
echo Building Docker image...
docker-compose -f docker-compose.port5001.yml build --no-cache

REM Start the container
echo Starting container on port 5001...
docker-compose -f docker-compose.port5001.yml up -d

REM Wait for container to be ready
echo Waiting for API to be ready...
timeout /t 10 /nobreak >nul

REM Check container status
docker ps | findstr thinkonerp-api-5001 >nul
if errorlevel 1 (
    echo Container failed to start
    echo Checking logs...
    docker-compose -f docker-compose.port5001.yml logs
    pause
    exit /b 1
) else (
    echo Container is running
)

echo.
echo ==========================================
echo Deployment Complete!
echo ==========================================
echo.
echo API is running on port 5001:
echo   - Swagger UI: http://localhost:5001/swagger
echo   - Health Check: http://localhost:5001/health
echo   - Base URL: http://localhost:5001
echo.
echo Useful commands:
echo   - View logs: docker-compose -f docker-compose.port5001.yml logs -f
echo   - Stop: docker-compose -f docker-compose.port5001.yml down
echo   - Restart: docker-compose -f docker-compose.port5001.yml restart
echo   - Status: docker-compose -f docker-compose.port5001.yml ps
echo.
pause
