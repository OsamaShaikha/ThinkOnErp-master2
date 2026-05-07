# 🐳 Docker Hub Deployment - START HERE

## 📍 You Are Here

You want to deploy your ThinkOnErp API + Oracle Database to your cloud server using Docker Hub.

---

## ⚡ Quick Decision Tree

### I want to deploy RIGHT NOW (15 minutes)
→ Read: **DOCKER_HUB_QUICK_START.md**

### I want detailed step-by-step instructions
→ Read: **DOCKER_HUB_COMPLETE_GUIDE.md** ⭐ RECOMMENDED

### I want to understand the complete solution
→ Read: **DOCKER_HUB_DEPLOYMENT_SUMMARY.md**

### I just need the commands to copy-paste
→ Use scripts:
- Windows: `build-and-push-to-dockerhub.ps1`
- Server: `deploy-from-dockerhub.sh`

---

## 📚 What's Available

### Documentation (Choose ONE to start)

1. **DOCKER_HUB_QUICK_START.md** (⚡ Fast)
   - 5-minute read
   - Copy-paste commands
   - Minimal explanation
   - Best for: Experienced users

2. **DOCKER_HUB_COMPLETE_GUIDE.md** (⭐ Recommended)
   - 15-minute read
   - Complete step-by-step instructions
   - Troubleshooting included
   - Best for: First-time deployment

3. **DOCKER_HUB_DEPLOYMENT_SUMMARY.md** (📖 Overview)
   - 10-minute read
   - Architecture and design
   - Configuration reference
   - Best for: Understanding the system

### Automation Scripts

1. **build-and-push-to-dockerhub.ps1** (Windows)
   - Builds both images
   - Pushes to Docker Hub
   - Interactive prompts
   - Usage: `.\build-and-push-to-dockerhub.ps1 -DockerHubUsername "yourusername"`

2. **deploy-from-dockerhub.sh** (Linux Server)
   - Deploys everything on server
   - Creates all config files
   - Starts services
   - Usage: `./deploy-from-dockerhub.sh`

### Docker Files

1. **Dockerfile.oracle** - Custom Oracle image with your database
2. **docker-compose.dockerhub.yml** - Orchestration for both services
3. **Database/init-database.sh** - Oracle initialization script

---

## 🎯 Recommended Path

### For First-Time Deployment:

```
1. Read this file (2 min) ✓ You are here
2. Read DOCKER_HUB_COMPLETE_GUIDE.md (15 min)
3. Run build-and-push-to-dockerhub.ps1 on Windows (10-15 min)
4. Run deploy-from-dockerhub.sh on server (5 min)
5. Verify at http://178.104.126.99:5000/swagger (1 min)

Total: ~35 minutes
```

### For Quick Deployment:

```
1. Read DOCKER_HUB_QUICK_START.md (5 min)
2. Run build-and-push-to-dockerhub.ps1 (10-15 min)
3. Run deploy-from-dockerhub.sh (5 min)

Total: ~20 minutes
```

---

## 🚀 What You'll Get

After deployment, you'll have:

✅ **Oracle Database** running in Docker with:
- All ThinkOnErp tables
- All stored procedures
- Test data (superadmin, moe, user1)
- Sequences and indexes

✅ **ThinkOnErp API** running in Docker with:
- .NET 8 Web API
- Swagger UI
- JWT authentication
- Audit logging
- Health checks

✅ **Easy Updates:**
- Build new version locally
- Push to Docker Hub
- Pull and restart on server
- 5-minute update process

---

## 📋 Prerequisites

Before you start, make sure you have:

### On Your Windows Machine:
- [ ] Docker Desktop installed and running
- [ ] Docker Hub account (free at https://hub.docker.com)
- [ ] PowerShell
- [ ] Project source code

### On Your Cloud Server (178.104.126.99):
- [ ] Docker installed
- [ ] Docker Compose installed
- [ ] SSH access (root/ThinkOnErp!@123)
- [ ] At least 4GB RAM
- [ ] At least 20GB free disk space

---

## 🎓 Understanding the Approach

### Traditional Deployment:
1. Copy source code to server
2. Install dependencies on server
3. Build on server
4. Configure on server
5. Run on server

**Problems:** Slow, inconsistent, hard to update

### Docker Hub Deployment:
1. Build images on your machine
2. Push to Docker Hub
3. Pull on server
4. Run on server

**Benefits:** Fast, consistent, easy to update

---

## 💡 Key Concepts

### Docker Image
A packaged application with all dependencies.
- **Oracle Image:** Oracle XE + your database schema
- **API Image:** .NET 8 + your application code

### Docker Hub
Like GitHub, but for Docker images.
- Upload images from your machine
- Download images on server
- Version control with tags (v1.0, v1.1, etc.)

### Docker Compose
Orchestrates multiple containers.
- Starts Oracle first
- Waits for Oracle to be healthy
- Then starts API
- Manages networking and volumes

---

## 🔍 Quick Troubleshooting

### "Docker is not running"
→ Start Docker Desktop on Windows

### "Permission denied"
→ Run PowerShell as Administrator

### "Image not found on Docker Hub"
→ Check your Docker Hub username is correct

### "Container keeps restarting"
→ Check logs: `docker logs thinkonerp-api`

### "Can't access Swagger UI"
→ Check firewall: `ufw allow 5000/tcp`

---

## 📞 Quick Reference

| Item | Value |
|------|-------|
| **Server IP** | 178.104.126.99 |
| **Swagger UI** | http://178.104.126.99:5000/swagger |
| **Test User** | superadmin / Admin@123 |
| **Documentation** | DOCKER_HUB_COMPLETE_GUIDE.md |
| **Quick Start** | DOCKER_HUB_QUICK_START.md |
| **Windows Script** | build-and-push-to-dockerhub.ps1 |
| **Server Script** | deploy-from-dockerhub.sh |

---

## 🎯 Next Steps

1. **Choose your path** (Quick or Complete)
2. **Read the guide** (5-15 minutes)
3. **Run the scripts** (15-20 minutes)
4. **Verify deployment** (1 minute)
5. **Celebrate!** 🎉

---

## 📚 All Documentation Files

- **DOCKER_HUB_README.md** - This file (start here)
- **DOCKER_HUB_QUICK_START.md** - Fast deployment guide
- **DOCKER_HUB_COMPLETE_GUIDE.md** - Complete step-by-step guide ⭐
- **DOCKER_HUB_DEPLOYMENT_SUMMARY.md** - Overview and architecture
- **DEPLOYMENT_FILES_INDEX.md** - Index of all deployment files

---

## ✅ Ready to Start?

### Option 1: Quick Deployment (20 minutes)
```powershell
# Read the quick start guide
notepad DOCKER_HUB_QUICK_START.md

# Then run the script
.\build-and-push-to-dockerhub.ps1 -DockerHubUsername "YOUR_USERNAME"
```

### Option 2: Complete Guide (35 minutes)
```powershell
# Read the complete guide
notepad DOCKER_HUB_COMPLETE_GUIDE.md

# Follow the instructions step by step
```

---

**🎉 Let's deploy your application to the cloud!**

---

**Created:** 2026-05-08  
**Version:** 1.0  
**Status:** Ready to use
