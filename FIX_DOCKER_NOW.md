# 🔧 Fix Docker Container Crash - FINAL SOLUTION

## Problem Summary
The `.env` file on your server is **corrupted with typos**, causing Docker to read invalid configuration from `appsettings.json` instead.

## ✅ SOLUTION: Use the Automated Script

The `server-setup-env.sh` script is already on GitHub (master2 branch). Just pull and run it!

---

## 📋 Step-by-Step Instructions

### Step 1: SSH to Your Server

Open **PowerShell** or **Command Prompt** and run:

```powershell
ssh root@178.104.126.99
```

**Password:** `ThinkOnErp!@123`

---

### Step 2: Pull Latest Code from GitHub

Once connected to the server, run:

```bash
cd ~/ThinkOnErp
git pull origin master2
```

This will download the `server-setup-env.sh` script.

---

### Step 3: Run the Automated Setup Script

```bash
bash server-setup-env.sh
```

**That's it!** The script will:
1. ✅ Delete the corrupted `.env` file
2. ✅ Create a fresh `.env` file with correct configuration
3. ✅ Restart the Docker container
4. ✅ Show you the logs

---

## 🎯 What You Should See

After running the script, you should see output like:

```
==========================================
ThinkOnErp - Server Environment Setup
==========================================

✅ In correct directory: /root/ThinkOnErp

📝 Creating .env file...
✅ .env file created successfully

🔄 Restarting Docker container...
[+] Running 1/1
 ✔ Container thinkonerp-api  Started

⏳ Waiting for container to start (10 seconds)...

📊 Container logs (last 30 lines):
[2026-05-07 XX:XX:XX] [INF] Starting ThinkOnErp API
[2026-05-07 XX:XX:XX] [INF] Archival service initialized
Now listening on: http://[::]:8080

==========================================
✅ Setup Complete!
==========================================
```

---

## 🧪 Test Your API

### Test 1: Health Check
```bash
curl http://localhost:8080/health
```

**Expected:** `Healthy` or similar response

### Test 2: Access Swagger UI

Open your browser and go to:

**http://178.104.126.99:5000/swagger**

You should see the full Swagger documentation!

### Test 3: Test Login API

```bash
curl -X POST http://178.104.126.99:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"superadmin","password":"Admin@123"}'
```

**Expected:** JSON response with JWT token

---

## 🔍 Troubleshooting

### If git pull fails:

```bash
cd ~/ThinkOnErp
git fetch origin
git reset --hard origin/master2
```

### If the script doesn't exist after pull:

```bash
ls -la server-setup-env.sh
```

If it's not there, the file might not be committed yet. In that case, create it manually:

```bash
cat > server-setup-env.sh << 'SCRIPT_END'
#!/bin/bash
set -e

echo "Creating .env file..."
cat > .env << 'EOF'
ORACLE_CONNECTION_STRING=User Id=THINKON_ERP;Password=THINKON_ERP;Data Source=178.104.126.99:1521/XEPDB1
JWT_SECRET_KEY=YourSuperSecretKeyForJWTTokenGenerationAndValidation123!
JWT_ISSUER=ThinkOnErpAPI
JWT_AUDIENCE=ThinkOnErpClient
JWT_EXPIRY_MINUTES=60
AUDIT__PAYLOADLOGGINGLEVEL=MetadataOnly
AUDIT__ENCRYPTIONKEY=dGhpc2lzYXRlc3RlbmNyeXB0aW9ua2V5Zm9yYXVkaXRsb2dz
AUDIT__SIGNINGKEY=dGhpc2lzYXRlc3RzaWduaW5na2V5Zm9yYXVkaXRsb2dz
ALERT__WEBHOOKURL=https://example.com/webhook
ALERT__NOTIFICATIONTIMEOUTSECONDS=30
ALERT__MAXRETRYATTEMPTS=2
ALERT__RETRYDELAYMS=5000
REDIS__CONNECTIONSTRING=
LOG_LEVEL=Information
API_PORT=5000
OPENTELEMETRY__OTLPENDPOINT=
EOF

echo "Restarting Docker..."
docker-compose -f docker-compose.simple.yml down
docker-compose -f docker-compose.simple.yml up -d
sleep 10
docker logs --tail 30 thinkonerp-api
SCRIPT_END

chmod +x server-setup-env.sh
bash server-setup-env.sh
```

### If container still crashes:

Check the logs for specific errors:

```bash
docker logs thinkonerp-api
```

### Verify .env file is correct:

```bash
cat ~/ThinkOnErp/.env | head -20
```

Make sure there are **NO typos** in variable names like:
- ❌ `...dGhpc2lzYXRlc3RlbmNyeXB0aW9ua2V5Zm9yYXVkaXRsb2dzAUDIT__SIGNINGKEY=...` (WRONG - has extra "AUDIT")
- ✅ `AUDIT__ENCRYPTIONKEY=dGhpc2lzYXRlc3RlbmNyeXB0aW9ua2V5Zm9yYXVkaXRsb2dz` (CORRECT)

---

## 📝 Configuration Details

The `.env` file contains:

| Variable | Value | Purpose |
|----------|-------|---------|
| `ORACLE_CONNECTION_STRING` | `User Id=THINKON_ERP;...` | Database connection |
| `JWT_SECRET_KEY` | `YourSuperSecretKey...` | JWT token signing |
| `AUDIT__PAYLOADLOGGINGLEVEL` | `MetadataOnly` | Audit logging level |
| `AUDIT__ENCRYPTIONKEY` | Base64 string | Encrypt sensitive audit data |
| `AUDIT__SIGNINGKEY` | Base64 string | Sign audit logs for integrity |
| `ALERT__WEBHOOKURL` | `https://example.com/webhook` | Alert notifications |
| `ALERT__NOTIFICATIONTIMEOUTSECONDS` | `30` | Notification timeout |
| `ALERT__MAXRETRYATTEMPTS` | `2` | Max retry attempts |
| `ALERT__RETRYDELAYMS` | `5000` | Delay between retries |
| `REDIS__CONNECTIONSTRING` | (empty) | Optional Redis cache |
| `LOG_LEVEL` | `Information` | Logging verbosity |
| `API_PORT` | `5000` | External API port |
| `OPENTELEMETRY__OTLPENDPOINT` | (empty) | Optional telemetry endpoint |

---

## ✅ Success Indicators

Your deployment is successful when you see:

1. ✅ Container status: `Up` (not restarting)
2. ✅ Logs show: `Now listening on: http://[::]:8080`
3. ✅ No validation errors in logs
4. ✅ Swagger UI loads at `http://178.104.126.99:5000/swagger`
5. ✅ Login API returns JWT token

---

## 🎉 Next Steps After Success

1. **Test all APIs** using Swagger UI
2. **Update production URLs** if needed (webhook, telemetry)
3. **Configure Redis** for caching (optional)
4. **Set up monitoring** with OpenTelemetry (optional)
5. **Enable HTTPS** with SSL certificate (recommended for production)

---

## 📞 Quick Reference

- **Server IP:** 178.104.126.99
- **SSH User:** root
- **SSH Password:** ThinkOnErp!@123
- **Project Path:** ~/ThinkOnErp
- **Git Branch:** master2
- **API Port:** 5000
- **Container Port:** 8080
- **Swagger URL:** http://178.104.126.99:5000/swagger
- **Test User:** superadmin / Admin@123

---

## 🔧 Manual Fix (If Script Fails)

If the automated script doesn't work, manually create the `.env` file:

```bash
cd ~/ThinkOnErp
rm -f .env

cat > .env << 'EOF'
ORACLE_CONNECTION_STRING=User Id=THINKON_ERP;Password=THINKON_ERP;Data Source=178.104.126.99:1521/XEPDB1
JWT_SECRET_KEY=YourSuperSecretKeyForJWTTokenGenerationAndValidation123!
JWT_ISSUER=ThinkOnErpAPI
JWT_AUDIENCE=ThinkOnErpClient
JWT_EXPIRY_MINUTES=60
AUDIT__PAYLOADLOGGINGLEVEL=MetadataOnly
AUDIT__ENCRYPTIONKEY=dGhpc2lzYXRlc3RlbmNyeXB0aW9ua2V5Zm9yYXVkaXRsb2dz
AUDIT__SIGNINGKEY=dGhpc2lzYXRlc3RzaWduaW5na2V5Zm9yYXVkaXRsb2dz
ALERT__WEBHOOKURL=https://example.com/webhook
ALERT__NOTIFICATIONTIMEOUTSECONDS=30
ALERT__MAXRETRYATTEMPTS=2
ALERT__RETRYDELAYMS=5000
REDIS__CONNECTIONSTRING=
LOG_LEVEL=Information
API_PORT=5000
OPENTELEMETRY__OTLPENDPOINT=
EOF

docker-compose -f docker-compose.simple.yml down
docker-compose -f docker-compose.simple.yml up -d
docker logs -f thinkonerp-api
```

Press `Ctrl+C` to stop watching logs (container keeps running).

---

**Good luck! 🚀**
