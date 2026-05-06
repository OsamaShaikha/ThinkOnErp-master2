# 🚀 Deploy .env Configuration NOW

## Quick Copy-Paste Commands

### Step 1: Upload .env File

Open **PowerShell** or **Command Prompt** and run:

```powershell
cd D:\ThinkOnErp
scp .env.production root@178.104.126.99:~/ThinkOnErp/.env
```

**When prompted for password, enter:** `ThinkOnErp!@123`

---

### Step 2: Restart Docker Container

After the file uploads successfully, run:

```powershell
ssh root@178.104.126.99
```

**When prompted for password, enter:** `ThinkOnErp!@123`

---

### Step 3: On the Server

Once connected to the server, copy and paste these commands:

```bash
cd ~/ThinkOnErp
docker-compose -f docker-compose.simple.yml down
docker-compose -f docker-compose.simple.yml up -d
docker logs -f thinkonerp-api
```

---

## What You Should See

After running the commands, you should see logs like:

```
[2026-05-06 XX:XX:XX] [INF] Starting ThinkOnErp API
[2026-05-06 XX:XX:XX] [INF] Archival service initialized with schedule: 0 2 * * *
Now listening on: http://[::]:8080
```

**Press `Ctrl+C` to stop watching logs** (container keeps running)

---

## Test the API

Open your browser and go to:

**http://178.104.126.99:5000/swagger**

You should see the Swagger UI with all API endpoints!

---

## Troubleshooting

### If SCP doesn't work:

**Option A: Use WinSCP (GUI tool)**
1. Download: https://winscp.net/eng/download.php
2. Install and open WinSCP
3. Connect with:
   - Host: `178.104.126.99`
   - Username: `root`
   - Password: `ThinkOnErp!@123`
4. Navigate to `/root/ThinkOnErp/`
5. Upload `.env.production` and rename it to `.env`
6. Then SSH to server and run Step 3 commands

**Option B: Manual Copy-Paste**
1. Open `.env.production` in Notepad
2. Copy all content (Ctrl+A, Ctrl+C)
3. SSH to server: `ssh root@178.104.126.99`
4. Run: `nano ~/ThinkOnErp/.env`
5. Paste content (Right-click)
6. Save: `Ctrl+X`, then `Y`, then `Enter`
7. Run Step 3 commands

---

## Server Credentials Summary

- **Server IP:** 178.104.126.99
- **Username:** root
- **Password:** ThinkOnErp!@123
- **Project Path:** ~/ThinkOnErp
- **API Port:** 5000
- **Swagger URL:** http://178.104.126.99:5000/swagger

---

## Need Help?

Check container status:
```bash
ssh root@178.104.126.99 "docker ps | grep thinkonerp"
```

View recent logs:
```bash
ssh root@178.104.126.99 "docker logs --tail 50 thinkonerp-api"
```

Check if .env file exists:
```bash
ssh root@178.104.126.99 "ls -la ~/ThinkOnErp/.env"
```
