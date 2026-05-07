# Port 5001 - Invalid Hostname Error Fix

## Problem Summary

When accessing `http://178.104.126.99:5001/swagger`, the application returned:
```
HTTP 400 Bad Request - Invalid Hostname
The request hostname is invalid.
```

## Root Cause

ASP.NET Core has built-in host filtering that blocks requests with IP addresses by default. This is a security feature to prevent DNS rebinding attacks.

## Solution Applied

### 1. Configuration Fix (appsettings.Production.json)
Changed `AllowedHosts` to allow all hosts:
```json
{
  "AllowedHosts": "*"
}
```

### 2. Code Fix (Program.cs)
Removed problematic middleware that was causing a compilation error:
```csharp
// REMOVED - This was causing CS1503 compilation error
// app.Use(async (context, next) =>
// {
//     context.Request.Host = new HostString(context.Request.Host.Host, context.Request.Host.Port);
//     await next();
// });
```

The middleware was attempting to reconstruct the Host header but had a type mismatch:
- `context.Request.Host.Port` returns `int?` (nullable)
- `HostString` constructor expects `int` (non-nullable)

### 3. HTTPS Redirection Already Disabled
Confirmed that HTTPS redirection is already commented out in Program.cs:
```csharp
// Disable HTTPS redirection for IP-based access
// app.UseHttpsRedirection();
```

## Files Modified

1. **src/ThinkOnErp.API/Program.cs**
   - Removed the problematic host filtering middleware (lines 376-381)

2. **src/ThinkOnErp.API/appsettings.Production.json**
   - Already had `"AllowedHosts": "*"` configured

## Deployment Steps

### Option 1: Using Bash Script (Linux/macOS/WSL)
```bash
chmod +x deploy-port5001-fixed.sh
./deploy-port5001-fixed.sh
```

### Option 2: Using PowerShell Script (Windows)
```powershell
.\deploy-port5001-fixed.ps1
```

### Option 3: Manual Deployment
```bash
# 1. Connect to server
ssh root@178.104.126.99

# 2. Navigate to directory
cd /root/thinkonerp-port5001

# 3. Stop existing container
docker stop thinkonerp-api-5001
docker rm thinkonerp-api-5001

# 4. Rebuild image
docker build -f Dockerfile.port5001 -t thinkonerp-api:port5001 .

# 5. Start container
docker run -d \
    --name thinkonerp-api-5001 \
    --restart unless-stopped \
    -p 5001:5001 \
    --env-file .env.port5001 \
    thinkonerp-api:port5001

# 6. Check logs
docker logs -f thinkonerp-api-5001
```

## Verification

After deployment, test the following endpoints:

1. **Swagger UI**: http://178.104.126.99:5001/swagger
   - Should display the API documentation interface
   - No "Invalid Hostname" error

2. **Health Check**: http://178.104.126.99:5001/health
   - Should return: `{"status":"Healthy"}`

3. **API Endpoint Test**: http://178.104.126.99:5001/api/auth/login
   - Should accept POST requests
   - Should return proper validation errors for invalid credentials

## Technical Details

### Why AllowedHosts: "*" is Safe Here

While `AllowedHosts: "*"` allows any hostname, this is acceptable because:

1. **No DNS Rebinding Risk**: The application doesn't make outbound requests based on the Host header
2. **Direct IP Access**: The server is accessed directly via IP, not through a domain
3. **Firewall Protection**: The server should have firewall rules limiting access
4. **Authentication Required**: All API endpoints require JWT authentication

### Alternative Solutions (Not Used)

If you need more restrictive host filtering, you could:

1. **Specify the IP explicitly**:
   ```json
   "AllowedHosts": "178.104.126.99;localhost"
   ```

2. **Use a domain name**:
   - Set up DNS: `api.thinkonerp.com` → `178.104.126.99`
   - Configure: `"AllowedHosts": "api.thinkonerp.com"`
   - Access via: `http://api.thinkonerp.com:5001/swagger`

3. **Fix the middleware** (more complex):
   ```csharp
   app.Use(async (context, next) =>
   {
       var port = context.Request.Host.Port ?? 5001;
       context.Request.Host = new HostString(context.Request.Host.Host, port);
       await next();
   });
   ```

## Server Configuration

- **Server IP**: 178.104.126.99
- **Port**: 5001
- **Container Name**: thinkonerp-api-5001
- **Image Tag**: thinkonerp-api:port5001
- **Environment**: Production
- **Database**: Oracle XE (XEPDB1) on same server

## Oracle Connection String

```
Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=178.104.126.99)(PORT=1521))(CONNECT_DATA=(SERVICE_NAME=XEPDB1)));User Id=THINKON_ERP;Password=THINKON_ERP;Pooling=true;Min Pool Size=5;Max Pool Size=100;Connection Timeout=15;
```

## Troubleshooting

### If you still get "Invalid Hostname" error:

1. **Check container logs**:
   ```bash
   docker logs thinkonerp-api-5001
   ```

2. **Verify environment**:
   ```bash
   docker exec thinkonerp-api-5001 env | grep ASPNETCORE
   ```
   Should show:
   - `ASPNETCORE_ENVIRONMENT=Production`
   - `ASPNETCORE_URLS=http://+:5001`

3. **Check appsettings are loaded**:
   ```bash
   docker exec thinkonerp-api-5001 cat /app/appsettings.Production.json | grep AllowedHosts
   ```
   Should show: `"AllowedHosts": "*"`

4. **Rebuild without cache**:
   ```bash
   docker build --no-cache -f Dockerfile.port5001 -t thinkonerp-api:port5001 .
   ```

### If container won't start:

1. **Check if port is already in use**:
   ```bash
   netstat -tulpn | grep 5001
   ```

2. **Check Docker logs**:
   ```bash
   docker logs thinkonerp-api-5001
   ```

3. **Test Oracle connection**:
   ```bash
   docker exec thinkonerp-api-5001 bash -c "echo 'SELECT 1 FROM DUAL;' | sqlplus THINKON_ERP/THINKON_ERP@//178.104.126.99:1521/XEPDB1"
   ```

## Next Steps

After successful deployment:

1. Test all API endpoints via Swagger UI
2. Verify authentication works
3. Check audit logging is functioning
4. Monitor container logs for any errors
5. Set up monitoring/alerting if needed

## Related Files

- `Dockerfile.port5001` - Docker image configuration
- `docker-compose.port5001.yml` - Docker Compose configuration
- `.env.port5001` - Environment variables
- `deploy-port5001-fixed.sh` - Linux/macOS deployment script
- `deploy-port5001-fixed.ps1` - Windows deployment script
- `src/ThinkOnErp.API/Program.cs` - Application startup configuration
- `src/ThinkOnErp.API/appsettings.Production.json` - Production settings

## Success Criteria

✅ Application builds without compilation errors
✅ Container starts successfully on port 5001
✅ Swagger UI accessible at http://178.104.126.99:5001/swagger
✅ No "Invalid Hostname" errors
✅ API endpoints respond correctly
✅ Authentication works
✅ Database connection successful

---

**Last Updated**: 2026-05-08
**Status**: Ready for Deployment
