# Deploy Port 5001 - Quick Start Guide

## ✅ Problem Fixed

The "Invalid Hostname" error has been resolved by:
1. Removing problematic middleware from `Program.cs`
2. Keeping `AllowedHosts: "*"` in `appsettings.Production.json`
3. HTTPS redirection already disabled

## 🚀 Deploy Now

### Windows Users:
```powershell
.\deploy-port5001-fixed.ps1
```

### Linux/macOS/WSL Users:
```bash
chmod +x deploy-port5001-fixed.sh
./deploy-port5001-fixed.sh
```

## 📋 What the Script Does

1. ✅ Creates remote directory on server
2. ✅ Uploads all project files (src, solution, Docker files)
3. ✅ Stops existing container (if running)
4. ✅ Builds new Docker image with fixes
5. ✅ Starts container on port 5001
6. ✅ Shows container status and logs

## 🎯 After Deployment

Test these URLs in your browser:

1. **Swagger UI**: http://178.104.126.99:5001/swagger
2. **Health Check**: http://178.104.126.99:5001/health

Both should work without "Invalid Hostname" errors!

## 📊 Server Details

- **IP**: 178.104.126.99
- **Port**: 5001
- **User**: root
- **Password**: ThinkOnErp!@123
- **Container**: thinkonerp-api-5001

## 🔍 View Logs

```bash
ssh root@178.104.126.99
docker logs -f thinkonerp-api-5001
```

## 🔄 Restart Container

```bash
ssh root@178.104.126.99
docker restart thinkonerp-api-5001
```

## 📚 Detailed Documentation

See `PORT5001_INVALID_HOSTNAME_FIX.md` for:
- Technical details
- Troubleshooting steps
- Alternative solutions
- Complete configuration reference

---

**Ready to deploy? Run the script above!** 🚀
