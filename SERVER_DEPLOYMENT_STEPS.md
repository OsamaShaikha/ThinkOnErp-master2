# 🚀 Server Deployment Steps (Using Git)

## ✅ Code has been pushed to GitHub!

The OpenTelemetry fix and server setup script are now on GitHub.

---

## 📋 Run These Commands on Your Server

### Step 1: SSH to Server

```bash
ssh root@178.104.126.99
```
**Password:** `ThinkOnErp!@123`

---

### Step 2: Pull Latest Code

```bash
cd ~/ThinkOnErp
git pull origin master2
```

---

### Step 3: Run Setup Script

```bash
chmod +x server-setup-env.sh
bash server-setup-env.sh
```

**This script will:**
- ✅ Create `.env` file with all required configuration
- ✅ Stop the Docker container
- ✅ Start the Docker container with new configuration
- ✅ Show you the logs

---

## 🎯 Expected Output

You should see:

```
[2026-05-06 XX:XX:XX] [INF] Starting ThinkOnErp API
[2026-05-06 XX:XX:XX] [INF] Archival service initialized with schedule: 0 2 * * *
Now listening on: http://[::]:8080
```

---

## ✅ Test the API

Open in browser:
**http://178.104.126.99:5000/swagger**

Or test with curl:
```bash
curl http://178.104.126.99:5000/health
```

Expected response:
```json
{"status":"Healthy"}
```

---

## 🔍 Troubleshooting

### Check container status:
```bash
docker ps | grep thinkonerp-api
```

### View logs:
```bash
docker logs -f thinkonerp-api
```

### Check .env file:
```bash
cat ~/ThinkOnErp/.env | head -20
```

### Restart container manually:
```bash
cd ~/ThinkOnErp
docker-compose -f docker-compose.simple.yml down
docker-compose -f docker-compose.simple.yml up -d
```

---

## 📝 What Was Fixed

1. **OpenTelemetry URI Validation** - Fixed crash when OTLP endpoint is not configured
2. **Environment Configuration** - Added all required settings:
   - Database connection
   - JWT authentication
   - Audit trail settings
   - Alert configuration
   - Redis (optional)

---

## 🔐 Security Note

The `.env` file is NOT in Git (it's in `.gitignore`). It's created directly on the server by the setup script. This keeps your credentials safe.

---

## ✨ Summary

**What you need to do:**
1. SSH to server
2. Run: `cd ~/ThinkOnErp && git pull origin master2`
3. Run: `bash server-setup-env.sh`
4. Test: Open `http://178.104.126.99:5000/swagger`

That's it! 🎉
