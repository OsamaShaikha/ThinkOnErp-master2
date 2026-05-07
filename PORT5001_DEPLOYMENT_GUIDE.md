# ThinkOnErp API - Port 5001 Deployment Guide

This guide explains how to deploy ThinkOnErp API on port 5001 (instead of the default port 5000).

## 📋 Files Created

1. **Dockerfile.port5001** - Docker image configuration for port 5001
2. **docker-compose.port5001.yml** - Docker Compose orchestration
3. **.env.port5001** - Environment configuration template
4. **deploy-port5001.sh** - Linux/Mac deployment script
5. **deploy-port5001.bat** - Windows deployment script

## 🚀 Quick Start

### Windows

```cmd
# 1. Configure environment (if needed)
copy .env.port5001 .env.port5001.custom
notepad .env.port5001.custom

# 2. Run deployment
deploy-port5001.bat
```

### Linux/Mac

```bash
# 1. Make script executable
chmod +x deploy-port5001.sh

# 2. Configure environment (if needed)
cp .env.port5001 .env.port5001.custom
nano .env.port5001.custom

# 3. Run deployment
./deploy-port5001.sh
```

## 📝 Manual Deployment

If you prefer manual deployment:

```bash
# 1. Copy environment file
cp .env.port5001 .env.production

# 2. Build image
docker-compose -f docker-compose.port5001.yml build --no-cache

# 3. Start container
docker-compose -f docker-compose.port5001.yml up -d

# 4. Check status
docker-compose -f docker-compose.port5001.yml ps

# 5. View logs
docker-compose -f docker-compose.port5001.yml logs -f
```

## 🔧 Configuration

### Port Configuration

The API is configured to run on port 5001 in three places:

1. **Dockerfile.port5001**:
   ```dockerfile
   EXPOSE 5001
   ENV ASPNETCORE_URLS=http://+:5001
   ```

2. **docker-compose.port5001.yml**:
   ```yaml
   ports:
     - "5001:5001"
   environment:
     - ASPNETCORE_URLS=http://+:5001
   ```

3. **.env.port5001**:
   ```bash
   API_PORT=5001
   ```

### Database Connection

Update the Oracle connection string in `.env.port5001`:

```bash
ORACLE_CONNECTION_STRING=Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=your-host)(PORT=1521))(CONNECT_DATA=(SERVICE_NAME=XEPDB1)));User Id=THINKON_ERP;Password=THINKON_ERP;
```

### JWT Configuration

Update JWT settings in `.env.port5001`:

```bash
JWT_SECRET_KEY=your-secure-secret-key-at-least-32-characters
JWT_ISSUER=ThinkOnErpAPI
JWT_AUDIENCE=ThinkOnErpClient
JWT_EXPIRY_MINUTES=60
```

## 🌐 Access Points

Once deployed, access the API at:

- **Swagger UI**: http://localhost:5001/swagger
- **Health Check**: http://localhost:5001/health
- **Base URL**: http://localhost:5001/api

### Remote Access

If deploying on a server (e.g., 178.104.126.99):

- **Swagger UI**: http://178.104.126.99:5001/swagger
- **Health Check**: http://178.104.126.99:5001/health
- **Base URL**: http://178.104.126.99:5001/api

## 🔍 Testing

### Test Health Endpoint

```bash
curl http://localhost:5001/health
```

### Test Login

```bash
curl -X POST http://localhost:5001/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "userName": "superadmin",
    "password": "SuperAdmin123!"
  }'
```

### Test with Token

```bash
# Get token from login response
TOKEN="your-jwt-token-here"

# Test protected endpoint
curl http://localhost:5001/api/roles \
  -H "Authorization: Bearer $TOKEN"
```

## 🛠️ Management Commands

### View Logs

```bash
# Follow logs
docker-compose -f docker-compose.port5001.yml logs -f

# Last 100 lines
docker-compose -f docker-compose.port5001.yml logs --tail=100

# Specific service
docker-compose -f docker-compose.port5001.yml logs -f thinkonerp-api-5001
```

### Restart Service

```bash
docker-compose -f docker-compose.port5001.yml restart
```

### Stop Service

```bash
docker-compose -f docker-compose.port5001.yml down
```

### Rebuild and Restart

```bash
docker-compose -f docker-compose.port5001.yml down
docker-compose -f docker-compose.port5001.yml build --no-cache
docker-compose -f docker-compose.port5001.yml up -d
```

### Check Status

```bash
# Container status
docker-compose -f docker-compose.port5001.yml ps

# Container health
docker inspect thinkonerp-api-5001 | grep -A 10 Health

# Resource usage
docker stats thinkonerp-api-5001
```

## 🔥 Firewall Configuration

If deploying on a server, open port 5001:

### Ubuntu/Debian

```bash
sudo ufw allow 5001/tcp
sudo ufw reload
sudo ufw status
```

### CentOS/RHEL

```bash
sudo firewall-cmd --permanent --add-port=5001/tcp
sudo firewall-cmd --reload
sudo firewall-cmd --list-ports
```

### Windows

```powershell
# Run as Administrator
New-NetFirewallRule -DisplayName "ThinkOnErp API 5001" -Direction Inbound -LocalPort 5001 -Protocol TCP -Action Allow
```

## 🐛 Troubleshooting

### Port Already in Use

```bash
# Find process using port 5001
# Linux/Mac
sudo lsof -i :5001

# Windows
netstat -ano | findstr :5001

# Kill process
# Linux/Mac
sudo kill -9 <PID>

# Windows
taskkill /PID <PID> /F
```

### Container Not Starting

```bash
# Check logs
docker-compose -f docker-compose.port5001.yml logs

# Check container status
docker ps -a | grep thinkonerp-api-5001

# Inspect container
docker inspect thinkonerp-api-5001
```

### Health Check Failing

```bash
# Check if API is listening
docker exec thinkonerp-api-5001 netstat -tuln | grep 5001

# Test from inside container
docker exec thinkonerp-api-5001 curl http://localhost:5001/health

# Check application logs
docker-compose -f docker-compose.port5001.yml logs -f thinkonerp-api-5001
```

### Database Connection Issues

```bash
# Test Oracle connectivity from container
docker exec thinkonerp-api-5001 ping 178.104.126.99

# Check environment variables
docker exec thinkonerp-api-5001 env | grep ORACLE
```

## 🔄 Running Both Port 5000 and 5001

You can run both versions simultaneously:

```bash
# Start port 5000 version
docker-compose -f docker-compose.simple.yml up -d

# Start port 5001 version
docker-compose -f docker-compose.port5001.yml up -d

# Check both are running
docker ps | grep thinkonerp
```

Access points:
- Port 5000: http://localhost:5000/swagger
- Port 5001: http://localhost:5001/swagger

## 📊 Comparison with Port 5000

| Feature | Port 5000 | Port 5001 |
|---------|-----------|-----------|
| Dockerfile | `Dockerfile` | `Dockerfile.port5001` |
| Compose File | `docker-compose.simple.yml` | `docker-compose.port5001.yml` |
| Container Name | `thinkonerp-api` | `thinkonerp-api-5001` |
| Port | 5000 | 5001 |
| Environment | `.env.production` | `.env.port5001` |
| Deploy Script | `deploy-simple.sh` | `deploy-port5001.sh` |

## 🚀 Production Deployment

For production deployment on server 178.104.126.99:

```bash
# 1. SSH to server
ssh root@178.104.126.99

# 2. Navigate to project
cd ~/ThinkOnErp

# 3. Upload files (from local machine)
scp Dockerfile.port5001 root@178.104.126.99:~/ThinkOnErp/
scp docker-compose.port5001.yml root@178.104.126.99:~/ThinkOnErp/
scp .env.port5001 root@178.104.126.99:~/ThinkOnErp/
scp deploy-port5001.sh root@178.104.126.99:~/ThinkOnErp/

# 4. On server, make script executable
chmod +x deploy-port5001.sh

# 5. Deploy
./deploy-port5001.sh
```

## ✅ Success Checklist

Your deployment is successful when:

- [ ] Container shows "healthy" in `docker ps`
- [ ] Swagger UI loads at http://localhost:5001/swagger
- [ ] Health endpoint returns 200 OK
- [ ] Login API returns JWT token
- [ ] Protected endpoints work with token
- [ ] Database queries execute successfully
- [ ] Logs show no errors

## 📞 Quick Reference

| Item | Value |
|------|-------|
| **Container Name** | thinkonerp-api-5001 |
| **Port** | 5001 |
| **Swagger UI** | http://localhost:5001/swagger |
| **Health Check** | http://localhost:5001/health |
| **Compose File** | docker-compose.port5001.yml |
| **Dockerfile** | Dockerfile.port5001 |
| **Environment** | .env.port5001 |
| **Deploy Script (Linux)** | deploy-port5001.sh |
| **Deploy Script (Windows)** | deploy-port5001.bat |

## 🎯 Next Steps

1. ✅ Deploy on port 5001
2. ✅ Test all endpoints
3. ✅ Configure firewall
4. ✅ Set up monitoring
5. ✅ Configure SSL/HTTPS (if needed)
6. ✅ Set up automated backups
7. ✅ Configure log rotation

---

**Created:** 2026-05-08  
**Version:** 1.0  
**Status:** Ready to use
