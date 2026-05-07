# 🚀 Docker Hub Quick Start Guide

## For the Impatient - Get Running in 15 Minutes

---

## 📍 You Are Here

You want to:
1. Build Docker images on your Windows machine
2. Upload them to Docker Hub
3. Deploy on your cloud server (178.104.126.99)

---

## ⚡ Quick Steps

### On Your Windows Machine (5-10 minutes)

```powershell
# 1. Open PowerShell in project directory
cd D:\ThinkOnErp

# 2. Run the build and push script
.\build-and-push-to-dockerhub.ps1 -DockerHubUsername "YOUR_USERNAME"

# Enter Docker Hub password when prompted
```

**Replace `YOUR_USERNAME` with your actual Docker Hub username!**

---

### On Your Cloud Server (5 minutes)

```bash
# 1. SSH to server
ssh root@178.104.126.99
# Password: ThinkOnErp!@123

# 2. Download and run deployment script
curl -o deploy.sh https://raw.githubusercontent.com/OsamaShaikha/ThinkOnErp-master2/master2/deploy-from-dockerhub.sh
chmod +x deploy.sh
./deploy.sh

# Enter your Docker Hub username when prompted
```

---

## ✅ Verify It's Working

### Check Containers

```bash
docker ps
```

Should show:
- `thinkonerp-oracle` (healthy)
- `thinkonerp-api` (healthy)

### Test API

Open browser: **http://178.104.126.99:5000/swagger**

### Test Login

```bash
curl -X POST http://178.104.126.99:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"superadmin","password":"Admin@123"}'
```

Should return JWT token!

---

## 🔧 If Something Goes Wrong

### Oracle Takes Too Long to Start

```bash
# Watch Oracle logs
docker logs -f thinkonerp-oracle

# Wait for: "DATABASE IS READY TO USE!"
# This takes 2-3 minutes on first start
```

### API Won't Start

```bash
# Check API logs
docker logs -f thinkonerp-api

# Common issue: Oracle not ready yet
# Solution: Wait 2-3 minutes, then restart API
docker-compose restart thinkonerp-api
```

### Can't Access Swagger

```bash
# Check if port 5000 is open
netstat -tulpn | grep 5000

# Open firewall if needed
ufw allow 5000/tcp
```

---

## 📝 Manual Deployment (If Script Doesn't Work)

### On Server:

```bash
# 1. Create directory
mkdir -p ~/ThinkOnErp && cd ~/ThinkOnErp

# 2. Create .env file
cat > .env << 'EOF'
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

# 3. Create docker-compose.yml (see DOCKER_HUB_COMPLETE_GUIDE.md)

# 4. Pull and start
docker-compose pull
docker-compose up -d
```

---

## 🔄 Update Deployment

### Build New Version on Windows:

```powershell
.\build-and-push-to-dockerhub.ps1 -DockerHubUsername "YOUR_USERNAME" -Version "v1.1"
```

### Update on Server:

```bash
cd ~/ThinkOnErp

# Update version in docker-compose.yml
sed -i 's/:v1.0/:v1.1/g' docker-compose.yml

# Pull and restart
docker-compose pull
docker-compose up -d
```

---

## 📚 Full Documentation

For detailed information, see:
- **DOCKER_HUB_COMPLETE_GUIDE.md** - Complete step-by-step guide
- **DOCKER_HUB_DEPLOYMENT.md** - Original deployment guide

---

## 🎯 What You Get

✅ **Oracle Database** with:
- All ThinkOnErp tables
- All stored procedures
- Test data (superadmin, moe, user1)
- Sequences and indexes

✅ **ThinkOnErp API** with:
- .NET 8 Web API
- Swagger UI
- JWT authentication
- Audit logging
- Health checks

✅ **Docker Compose** orchestrating:
- Automatic startup
- Health monitoring
- Log management
- Network isolation
- Data persistence

---

## 🆘 Need Help?

1. **Check logs:** `docker-compose logs -f`
2. **Check status:** `docker ps`
3. **Restart:** `docker-compose restart`
4. **Start fresh:** `docker-compose down && docker-compose up -d`

---

## 📞 Quick Reference

| Item | Value |
|------|-------|
| **Server IP** | 178.104.126.99 |
| **Swagger** | http://178.104.126.99:5000/swagger |
| **Test User** | superadmin / Admin@123 |
| **Oracle User** | THINKON_ERP / THINKON_ERP |
| **Oracle Service** | XEPDB1 |
| **API Port** | 5000 |
| **Oracle Port** | 1521 |

---

**🎉 That's it! Your system should be running now!**
