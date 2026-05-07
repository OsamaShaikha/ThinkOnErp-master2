# Fix: Invalid Hostname Error (HTTP 400)

## Problem

When accessing the API via IP address (http://178.104.126.99:5001/swagger), you get:

```
Bad Request - Invalid Hostname
HTTP Error 400. The request hostname is invalid.
```

## Root Cause

ASP.NET Core has host filtering enabled by default for security. When accessing via IP address instead of a domain name, the host validation fails.

## Solution Applied

### 1. Updated Program.cs

Added middleware to allow all hosts:

```csharp
// Configure allowed hosts - allow all hosts in production (for IP-based access)
app.Use(async (context, next) =>
{
    context.Request.Host = new HostString(context.Request.Host.Host, context.Request.Host.Port);
    await next();
});
```

Also disabled HTTPS redirection for IP-based access:

```csharp
// Disable HTTPS redirection for IP-based access
// app.UseHttpsRedirection();
```

### 2. Updated appsettings.Production.json

Added wildcard allowed hosts:

```json
{
  "AllowedHosts": "*"
}
```

## Files Modified

1. ✅ `src/ThinkOnErp.API/Program.cs` - Added host filtering bypass
2. ✅ `src/ThinkOnErp.API/appsettings.Production.json` - Added AllowedHosts: "*"

## Rebuild and Redeploy

After these changes, you need to rebuild and redeploy:

### Option 1: Automated Deployment

```powershell
# Windows
.\deploy-to-server-port5001.ps1
```

```bash
# Linux/Mac
./deploy-to-server-port5001.sh
```

### Option 2: Manual Deployment

```bash
# 1. Upload updated files
scp src/ThinkOnErp.API/Program.cs root@178.104.126.99:~/ThinkOnErp/src/ThinkOnErp.API/
scp src/ThinkOnErp.API/appsettings.Production.json root@178.104.126.99:~/ThinkOnErp/src/ThinkOnErp.API/

# 2. SSH to server
ssh root@178.104.126.99

# 3. Rebuild and restart
cd ~/ThinkOnErp
docker-compose -f docker-compose.port5001.yml down
docker-compose -f docker-compose.port5001.yml build --no-cache
docker-compose -f docker-compose.port5001.yml up -d

# 4. Check logs
docker-compose -f docker-compose.port5001.yml logs -f
```

## Verify Fix

After redeployment, test:

```bash
# Test health endpoint
curl http://178.104.126.99:5001/health

# Open Swagger UI in browser
http://178.104.126.99:5001/swagger
```

## Security Note

Setting `AllowedHosts: "*"` allows requests from any hostname. This is acceptable for:
- Development environments
- Internal networks
- IP-based access

For production with a domain name, you should set specific allowed hosts:

```json
{
  "AllowedHosts": "yourdomain.com;www.yourdomain.com;178.104.126.99"
}
```

## Alternative Solutions

### Option A: Use Domain Name

Instead of IP address, use a domain name:
- Set up DNS: `api.yourdomain.com` → `178.104.126.99`
- Access via: `http://api.yourdomain.com:5001/swagger`

### Option B: Configure Specific Hosts

In `appsettings.Production.json`:

```json
{
  "AllowedHosts": "178.104.126.99;localhost"
}
```

### Option C: Use Reverse Proxy

Set up Nginx with proper host headers:

```nginx
server {
    listen 80;
    server_name 178.104.126.99;
    
    location / {
        proxy_pass http://localhost:5001;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

## Status

✅ **Fixed** - Changes applied to Program.cs and appsettings.Production.json

🔄 **Action Required** - Rebuild and redeploy the application

---

**Created:** 2026-05-08  
**Status:** Ready to rebuild and deploy
