# Production Environment Configuration Deployment

## Overview

This guide explains how to deploy the production environment configuration (`.env` file) to your server to fix the application startup configuration errors.

## Files Created

1. **`.env.production`** - Production-ready environment configuration
2. **`deploy-env-to-server.sh`** - Bash deployment script (Linux/Mac)
3. **`deploy-env-to-server.ps1`** - PowerShell deployment script (Windows)

## Configuration Details

The `.env.production` file includes all required settings:

### Database Connection
- Oracle database at `178.104.126.99:1521/XEPDB1`
- User: `THINKON_ERP`

### JWT Authentication
- Secret key for token generation
- Issuer: `ThinkOnErpAPI`
- Audience: `ThinkOnErpClient`
- Token expiry: 60 minutes

### Audit Trail Settings
- **PayloadLoggingLevel**: `MetadataOnly` (logs metadata without full payloads)
- **EncryptionKey**: Base64-encoded 32-byte key for encrypting sensitive audit data
- **SigningKey**: Base64-encoded key for signing audit logs

### Alert & Notification Settings
- **WebhookUrl**: Placeholder (update with your actual webhook URL)
- **NotificationTimeoutSeconds**: 30 seconds
- **MaxRetryAttempts**: 2 (reduced to fit within timeout)
- **RetryDelayMs**: 5000 (5 seconds between retries)

### Redis Cache
- **ConnectionString**: Empty (Redis is optional, application works without it)

## Deployment Methods

### Method 1: Automated Deployment (Recommended)

#### On Windows:
```powershell
# Make sure you're in the project root directory
cd D:\ThinkOnErp

# Run the PowerShell deployment script
.\deploy-env-to-server.ps1
```

#### On Linux/Mac:
```bash
# Make the script executable
chmod +x deploy-env-to-server.sh

# Run the deployment script
./deploy-env-to-server.sh
```

The script will:
1. ✅ Verify `.env.production` exists
2. 📋 Show configuration summary
3. 📤 Upload `.env` file to server
4. 🔄 Restart Docker container
5. 📊 Show container logs
6. ✅ Confirm deployment success

### Method 2: Manual Deployment

If you prefer manual deployment or the scripts don't work:

#### Step 1: Copy .env file to server
```bash
scp .env.production root@178.104.126.99:~/ThinkOnErp/.env
```

#### Step 2: SSH to server and restart container
```bash
ssh root@178.104.126.99
cd ~/ThinkOnErp
docker-compose -f docker-compose.simple.yml down
docker-compose -f docker-compose.simple.yml up -d
docker logs -f thinkonerp-api
```

## Verification

After deployment, verify the application is running:

### 1. Check Container Status
```bash
ssh root@178.104.126.99 'docker ps | grep thinkonerp-api'
```

Expected output:
```
CONTAINER ID   IMAGE                        STATUS         PORTS
xxxxxxxxxxxxx  thinkonerp_thinkonerp-api   Up X minutes   0.0.0.0:5000->8080/tcp
```

### 2. Check Application Logs
```bash
ssh root@178.104.126.99 'docker logs --tail 50 thinkonerp-api'
```

Expected output should include:
```
[INF] Starting ThinkOnErp API
[INF] Archival service initialized with schedule: 0 2 * * *
Now listening on: http://[::]:8080
```

### 3. Test Health Endpoint
```bash
curl http://178.104.126.99:5000/health
```

Expected response:
```json
{"status":"Healthy"}
```

### 4. Access Swagger UI
Open in browser: `http://178.104.126.99:5000/swagger`

## Troubleshooting

### Issue: Configuration validation errors still appear

**Solution**: Check that the `.env` file was uploaded correctly:
```bash
ssh root@178.104.126.99 'cat ~/ThinkOnErp/.env | head -20'
```

### Issue: Container not starting

**Solution**: Check Docker logs for specific errors:
```bash
ssh root@178.104.126.99 'docker logs thinkonerp-api'
```

### Issue: Database connection errors

**Solution**: Verify database is accessible from server:
```bash
ssh root@178.104.126.99 'telnet 178.104.126.99 1521'
```

### Issue: Port 5000 not accessible

**Solution**: Check firewall rules:
```bash
ssh root@178.104.126.99 'sudo ufw status'
# If port 5000 is blocked, allow it:
ssh root@178.104.126.99 'sudo ufw allow 5000/tcp'
```

## Security Recommendations

### 1. Update JWT Secret Key
The default JWT secret key should be changed for production:
```bash
# Generate a strong random key
openssl rand -base64 64
```

Update in `.env.production`:
```
JWT_SECRET_KEY=<your-generated-key>
```

### 2. Update Encryption Keys
Generate proper Base64-encoded keys for audit encryption:
```bash
# Generate encryption key (32 bytes)
openssl rand -base64 32

# Generate signing key (32 bytes)
openssl rand -base64 32
```

Update in `.env.production`:
```
AUDIT__ENCRYPTIONKEY=<your-encryption-key>
AUDIT__SIGNINGKEY=<your-signing-key>
```

### 3. Configure Real Webhook URL
Update the alert webhook URL with your actual notification endpoint:
```
ALERT__WEBHOOKURL=https://your-actual-webhook-url.com/alerts
```

### 4. Enable Redis (Optional)
If you want to use Redis for caching:
```
REDIS__CONNECTIONSTRING=your-redis-host:6379,password=your-redis-password
```

## Configuration Updates

To update configuration after initial deployment:

1. Edit `.env.production` locally
2. Run deployment script again:
   ```powershell
   .\deploy-env-to-server.ps1
   ```
3. Container will automatically restart with new configuration

## Related Files

- `.env.example` - Template with all available configuration options
- `docker-compose.simple.yml` - Docker Compose configuration
- `DEPLOYMENT_SIMPLE.md` - General deployment guide

## Support

If you encounter issues:
1. Check container logs: `docker logs thinkonerp-api`
2. Verify `.env` file exists on server: `ls -la ~/ThinkOnErp/.env`
3. Check Docker Compose configuration: `cat ~/ThinkOnErp/docker-compose.simple.yml`
4. Verify network connectivity: `curl http://localhost:8080/health` (from server)
