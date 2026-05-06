@echo off
cd /d "%~dp0"
echo ==========================================
echo ThinkOnErp - Deploy Environment Config
echo ==========================================
echo.
echo Working directory: %CD%
echo.

if not exist ".env.production" (
    echo ERROR: .env.production file not found!
    echo Looking in: %CD%
    pause
    exit /b 1
)

echo Configuration file found: .env.production
echo.
echo Server: 178.104.126.99
echo Username: root
echo Password: ThinkOnErp!@123
echo.
echo STEP 1: Upload .env file to server
echo ----------------------------------------
echo Running: scp .env.production root@178.104.126.99:~/ThinkOnErp/.env
echo.
echo When prompted for password, enter: ThinkOnErp!@123
echo.

scp .env.production root@178.104.126.99:~/ThinkOnErp/.env

if errorlevel 1 (
    echo.
    echo ERROR: Failed to upload file!
    echo.
    echo Make sure you have:
    echo   1. OpenSSH client installed (Windows 10/11 has it built-in)
    echo   2. Network access to 178.104.126.99
    echo.
    pause
    exit /b 1
)

echo.
echo SUCCESS: File uploaded!
echo.
echo STEP 2: Restart Docker container on server
echo ----------------------------------------
echo.
echo Connecting to server...
echo When prompted for password, enter: ThinkOnErp!@123
echo.

ssh root@178.104.126.99 "cd ~/ThinkOnErp && docker-compose -f docker-compose.simple.yml down && docker-compose -f docker-compose.simple.yml up -d && sleep 5 && docker logs --tail 30 thinkonerp-api"

echo.
echo ==========================================
echo Deployment Complete!
echo ==========================================
echo.
echo Test your API:
echo   http://178.104.126.99:5000/swagger
echo.
echo Check logs:
echo   ssh root@178.104.126.99 "docker logs -f thinkonerp-api"
echo.
pause
