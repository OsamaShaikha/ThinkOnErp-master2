# Quick Deployment Instructions

## The `.env.production` file has been created with all the required configuration.

## Deploy to Server - Choose ONE method:

### Method 1: Using SCP (Recommended)

Open PowerShell or Command Prompt and run:

```powershell
# Navigate to project root
cd D:\ThinkOnErp

# Copy .env file to server
scp .env.production root@178.104.126.99:~/ThinkOnErp/.env

# SSH to server and restart container
ssh root@178.104.126.99
```

Then on the server, run:
```bash
cd ~/ThinkOnErp
docker-compose -f docker-compose.simple.yml down
docker-compose -f docker-compose.simple.yml up -d
docker logs -f thinkonerp-api
```

### Method 2: Manual Copy-Paste

1. **Copy the .env content:**
   - Open `D:\ThinkOnErp\.env.production` in Notepad
   - Copy all content (Ctrl+A, Ctrl+C)

2. **SSH to server:**
   ```bash
   ssh root@178.104.126.99
   ```

3. **Create .env file on server:**
   ```bash
   cd ~/ThinkOnErp
   nano .env
   ```

4. **Paste the content** (Right-click in terminal)

5. **Save and exit:**
   - Press `Ctrl+X`
   - Press `Y` to confirm
   - Press `Enter`

6. **Restart container:**
   ```bash
   docker-compose -f docker-compose.simple.yml down
   docker-compose -f docker-compose.simple.yml up -d
   docker logs -f thinkonerp-api
   ```

## What to Look For

After restarting, you should see in the logs:
```
[INF] Starting ThinkOnErp API
[INF] Archival service initialized with schedule: 0 2 * * *
Now listening on: http://[::]:8080
```

## Test the API

```bash
# From your Windows machine:
curl http://178.104.126.99:5000/health

# Or open in browser:
http://178.104.126.99:5000/swagger
```

## If You See Errors

Check the logs:
```bash
ssh root@178.104.126.99 'docker logs --tail 100 thinkonerp-api'
```
