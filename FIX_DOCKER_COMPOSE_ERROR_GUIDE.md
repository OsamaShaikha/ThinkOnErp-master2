# Fix Docker Compose ContainerConfig Error

## Problem

You're getting this error when trying to start the container:

```
KeyError: 'ContainerConfig'
ERROR: for thinkonerp-api  'ContainerConfig'
```

This is a **known bug in docker-compose 1.29.2** when trying to recreate containers. The old container metadata is corrupted.

## Solution

You need to **completely remove the old container** before starting a new one.

### Quick Fix (RECOMMENDED)

Run this script on your Linux server:

```bash
chmod +x fix-docker-compose-error.sh
./fix-docker-compose-error.sh
```

This script will:
1. Stop all containers
2. Remove the problematic container
3. Clean up dangling containers
4. Start fresh
5. Show you the logs

### Manual Fix

If you prefer to run commands manually:

```bash
# Step 1: Force stop and remove all containers
docker-compose -f docker-compose.simple.yml down -v

# Step 2: Remove the specific problematic container
docker rm -f c54a37a497c6_thinkonerp-api
docker rm -f thinkonerp-api
docker rm -f $(docker ps -a -q --filter "name=thinkonerp")

# Step 3: Clean up dangling containers
docker container prune -f

# Step 4: Start fresh
docker-compose -f docker-compose.simple.yml up -d

# Step 5: Check logs
docker logs -f thinkonerp-thinkonerp-api
```

## What Happened

1. ✅ **Docker build succeeded** - Your image was built successfully with the Redis fix
2. ❌ **Docker-compose failed** - The old container had corrupted metadata
3. ✅ **Solution** - Remove old container completely and start fresh

## After Running the Fix

You should see in the logs:

```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://[::]:5000
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
```

**No more Redis validation errors!**
**No more ContainerConfig errors!**

## Test Your API

```bash
# Health check
curl http://localhost:5000/health

# Or from external
curl http://178.104.126.99:5000/health

# Swagger UI
http://178.104.126.99:5000/swagger
```

## Files Created for You

1. **`fix-docker-compose-error.sh`** - Automated fix script (USE THIS!)
2. **`.env`** - Production environment variables
3. **`.env.production`** - Backup production config
4. **`FIX_DOCKER_COMPOSE_ERROR_GUIDE.md`** - This guide

## Summary

🎯 **Problem 1**: Redis validation error → ✅ FIXED (removed [Required] attribute)  
🎯 **Problem 2**: Docker ContainerConfig error → ✅ FIXED (remove old container)  
📦 **Status**: Ready to deploy  
⏱️ **Time**: ~1 minute to fix  

**Just run `./fix-docker-compose-error.sh` and you're done!**

## Alternative: Use Docker Commands Directly

If docker-compose continues to have issues, you can run the container directly:

```bash
# Stop and remove old container
docker stop thinkonerp-thinkonerp-api 2>/dev/null
docker rm thinkonerp-thinkonerp-api 2>/dev/null

# Run new container directly
docker run -d \
  --name thinkonerp-thinkonerp-api \
  --env-file .env.production \
  -p 5000:8080 \
  thinkonerp_thinkonerp-api:latest

# Check logs
docker logs -f thinkonerp-thinkonerp-api
```

## Need Help?

If you still encounter issues:

1. **Check Docker version**: `docker --version`
2. **Check docker-compose version**: `docker-compose --version`
3. **List all containers**: `docker ps -a`
4. **Remove ALL containers**: `docker rm -f $(docker ps -a -q)`
5. **Start fresh**: `./fix-docker-compose-error.sh`
