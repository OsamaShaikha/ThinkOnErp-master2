# Deploy ThinkOnErp API to Server on Port 5001

## 🎯 Quick Deployment Guide

Deploy your ThinkOnErp API to server **178.104.126.99** on port **5001**.

---

## 🚀 **Option 1: Automated Deployment (Recommended)**

### **Windows (PowerShell)**

```powershell
# Run the deployment script
.\deploy-to-server-port5001.ps1
```

**When prompted for password, enter:** `ThinkOnErp!@123`

### **Linux/Mac (Bash)**

```bash
# Make script executable
chmod +x deploy-to-server-port5001.sh

# Run deployment
./deploy-to-server-port5001.sh
```

**When prompted for password, enter:** `ThinkOnErp!@123`

---

## 📋 **Option 2: Manual Deployment**

### **Step 1: Upload Files to Server**

```bash
# Upload Dockerfile
scp Dockerfile.port5001 root@178.104.126.99:~/ThinkOnErp/

# Upload docker-compose
scp docker-compose.port5001.yml root@178.104.126.99:~/ThinkOnErp/

# Upload environment file
scp .env.port5001 root@178.104.126.99:~/ThinkOnErp/.env.production
```

**Password:** `ThinkOnErp!@123`

### **Step 2: SSH to Server**

```bash
ssh root@178.104.126.99
# Password: ThinkOnErp!@123
```

### **Step 3: Deploy on Server**

```bash
cd ~/ThinkOnErp

# Stop existing container (if any)
docker-compose -f docker-compose.port5001.yml down

# Build image
docker-compose -f docker-compose.port5001.yml build --no-cache

# Start container
docker-compose -f docker-compose.port5001.yml up -d

# Check status
docker-compose -f docker-compose.port5001.yml ps

# View logs
docker-compose -f docker-compose.port5001.yml logs -f
```

---

## 🌐 **Access Your API**

Once deployed, access your API at:

### **Swagger UI**
```
http://178.104.126.99:5001/swagger
```

### **Health Check**
```
http://178.104.126.99:5001/health
```

### **API Base URL**
```
http://178.104.126.99:5001/api
```

---

## 🔐 **Test Credentials**

Use these credentials to test the API:

| Username | Password | Role |
|----------|----------|------|
| superadmin | SuperAdmin123! | SuperAdmin |
| moe | Admin@123 | Admin |
| user1 | User@123 | User |

---

## 🧪 **Test the Deployment**

### **1. Test Health Endpoint**

```bash
curl http://178.104.126.99:5001/health
```

**Expected Response:**
```json
{
  "status": "Healthy",
  "timestamp": "2026-05-08T..."
}
```

### **2. Test Login**

```bash
curl -X POST http://178.104.126.99:5001/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "userName": "superadmin",
    "password": "SuperAdmin123!"
  }'
```

**Expected Response:**
```json
{
  "success": true,
  "statusCode": 200,
  "message": "Login successful",
  "data": {
    "accessToken": "eyJhbGc...",
    "expiresAt": "2026-05-08T...",
    "tokenType": "Bearer"
  }
}
```

### **3. Test Protected Endpoint**

```bash
# Get token from login response
TOKEN="your-jwt-token-here"

# Test roles endpoint
curl http://178.104.126.99:5001/api/roles \
  -H "Authorization: Bearer $TOKEN"
```

---

## 🛠️ **Management Commands**

### **View Logs**

```bash
ssh root@178.104.126.99
cd ~/ThinkOnErp
docker-compose -f docker-compose.port5001.yml logs -f
```

### **Restart Container**

```bash
ssh root@178.104.126.99
cd ~/ThinkOnErp
docker-compose -f docker-compose.port5001.yml restart
```

### **Stop Container**

```bash
ssh root@178.104.126.99
cd ~/ThinkOnErp
docker-compose -f docker-compose.port5001.yml down
```

### **Check Status**

```bash
ssh root@178.104.126.99
cd ~/ThinkOnErp
docker-compose -f docker-compose.port5001.yml ps
docker logs thinkonerp-api-5001
```

### **Rebuild and Restart**

```bash
ssh root@178.104.126.99
cd ~/ThinkOnErp
docker-compose -f docker-compose.port5001.yml down
docker-compose -f docker-compose.port5001.yml build --no-cache
docker-compose -f docker-compose.port5001.yml up -d
```

---

## 🔥 **Firewall Configuration**

Port 5001 should be open on the server. If not, run:

```bash
ssh root@178.104.126.99

# Open port 5001
sudo ufw allow 5001/tcp
sudo ufw reload
sudo ufw status
```

---

## 🐛 **Troubleshooting**

### **Container Not Starting**

```bash
# Check logs
ssh root@178.104.126.99
cd ~/ThinkOnErp
docker-compose -f docker-compose.port5001.yml logs --tail=100

# Check container status
docker ps -a | grep thinkonerp-api-5001

# Inspect container
docker inspect thinkonerp-api-5001
```

### **Health Check Failing**

```bash
# Test from inside container
ssh root@178.104.126.99
docker exec thinkonerp-api-5001 curl http://localhost:5001/health

# Check if port is listening
docker exec thinkonerp-api-5001 netstat -tuln | grep 5001

# Check application logs
docker logs thinkonerp-api-5001 --tail=50
```

### **Database Connection Issues**

```bash
# Test Oracle connectivity
ssh root@178.104.126.99
docker exec thinkonerp-api-5001 ping 178.104.126.99

# Check environment variables
docker exec thinkonerp-api-5001 env | grep ORACLE
```

### **Port Already in Use**

```bash
# Check what's using port 5001
ssh root@178.104.126.99
sudo lsof -i :5001

# Or
netstat -tuln | grep 5001

# Stop the conflicting service
docker stop <container-name>
```

---

## 📊 **Configuration Details**

### **Server Information**
- **IP Address:** 178.104.126.99
- **User:** root
- **Password:** ThinkOnErp!@123
- **Project Path:** ~/ThinkOnErp

### **API Configuration**
- **Port:** 5001
- **Container Name:** thinkonerp-api-5001
- **Environment:** Production
- **Health Check:** http://localhost:5001/health

### **Database Configuration**
- **Host:** 178.104.126.99
- **Port:** 1521
- **Service:** XEPDB1
- **User:** THINKON_ERP
- **Password:** THINKON_ERP

### **JWT Configuration**
- **Secret Key:** A7fK9xP2LmQ8vR4TzW6nB1CjD5eH3YUs
- **Issuer:** ThinkOnErpAPI
- **Audience:** ThinkOnErpClient
- **Expiry:** 60 minutes

---

## ✅ **Deployment Checklist**

- [ ] Files uploaded to server
- [ ] Docker image built successfully
- [ ] Container started and running
- [ ] Health check returns 200 OK
- [ ] Swagger UI accessible
- [ ] Login API works
- [ ] Protected endpoints work with token
- [ ] Database queries execute successfully
- [ ] Firewall allows port 5001
- [ ] Logs show no errors

---

## 🔄 **Update Workflow**

When you make code changes:

### **1. On Your Local Machine**

```bash
# Make your code changes
# Then run deployment script
.\deploy-to-server-port5001.ps1
```

### **2. On Server (Automatic)**

The script will:
- Upload new files
- Rebuild Docker image
- Restart container
- Verify deployment

**Total time: ~5 minutes**

---

## 📞 **Quick Reference**

| Item | Value |
|------|-------|
| **Server IP** | 178.104.126.99 |
| **SSH User** | root |
| **SSH Password** | ThinkOnErp!@123 |
| **API Port** | 5001 |
| **Container Name** | thinkonerp-api-5001 |
| **Swagger URL** | http://178.104.126.99:5001/swagger |
| **Health URL** | http://178.104.126.99:5001/health |
| **Project Path** | ~/ThinkOnErp |
| **Compose File** | docker-compose.port5001.yml |
| **Dockerfile** | Dockerfile.port5001 |
| **Environment** | .env.production |

---

## 🎉 **Success!**

Your ThinkOnErp API is now running on:

**🌐 http://178.104.126.99:5001/swagger**

Test it with:
- Username: `superadmin`
- Password: `SuperAdmin123!`

---

**Created:** 2026-05-08  
**Version:** 1.0  
**Status:** Ready to deploy
