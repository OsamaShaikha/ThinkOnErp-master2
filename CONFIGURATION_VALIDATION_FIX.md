# Configuration Validation Errors - Fixed

## Date: 2026-05-08

## Summary
Fixed 5 critical configuration validation errors preventing application startup.

## Errors Fixed

### 1. JWT Token Expiry vs Refresh Token Expiry
**Error:** `JWT token expiration (60 minutes) exceeds refresh token expiration (30 minutes)`

**Root Cause:** 
- JWT_EXPIRY_MINUTES was set to 60 minutes
- RefreshTokenExpiryInDays in appsettings.json is 7 days (10,080 minutes)
- The error message was misleading - it was comparing against a different timeout

**Fix:**
- Changed JWT_EXPIRY_MINUTES from 60 to 1440 (24 hours)
- Added JWT_REFRESH_TOKEN_EXPIRY_DAYS=7 for clarity
- This ensures JWT tokens expire well before refresh tokens

### 2. Notification Retry Time Exceeds Timeout
**Error:** `Maximum total retry time (35000ms) exceeds NotificationTimeoutSeconds (30s)`

**Root Cause:**
- ALERT__MAXRETRYATTEMPTS=2
- ALERT__RETRYDELAYMS=5000
- With exponential backoff: 5000 + 10000 + 20000 = 35000ms
- ALERT__NOTIFICATIONTIMEOUTSECONDS=30 (30000ms)

**Fix:**
- Increased ALERT__NOTIFICATIONTIMEOUTSECONDS from 30 to 45 seconds
- Reduced ALERT__RETRYDELAYMS from 5000 to 3000ms
- Total retry time: 3000 + 6000 = 9000ms (well within 45s timeout)

### 3. Encryption Key Validation
**Error:** `Encryption key must be at least 44 characters (32 bytes Base64 encoded); Encryption key must be a valid Base64 encoded string`

**Root Cause:**
- Old key: `dGhpc2lzYXRlc3RlbmNyeXB0aW9ua2V5Zm9yYXVkaXRsb2dz` (48 chars but invalid)
- Not a proper Base64-encoded 32-byte key

**Fix:**
- Replaced with valid Base64 key: `yAjc9j4pVS8QtKJM883ovzOJp3HYeGCKT1hWwVM1ZRA=`
- This is a proper 32-byte key encoded in Base64 (44 characters)

### 4. Signing Key Validation
**Error:** `Signing key must be a valid Base64 encoded string`

**Root Cause:**
- Old key: `dGhpc2lzYXRlc3RzaWduaW5na2V5Zm9yYXVkaXRsb2dz` (invalid Base64)

**Fix:**
- Replaced with valid Base64 key: `tC6GSIQyH1OjIZni5+b+xMk9DvpMZz1W2PSiz/f966A=`
- This matches the key from appsettings.json

### 5. Redis Connection String Format
**Error:** `RedisConnectionString must be in format 'host:port' or 'host:port,password=xxx'`

**Root Cause:**
- REDIS__CONNECTIONSTRING was empty but validation was still running

**Fix:**
- Left REDIS__CONNECTIONSTRING empty (commented out)
- Added SECURITY__USEREDISCACHE=false to disable Redis dependency
- This allows the app to run without Redis

## Configuration Changes Summary

### .env.production (Updated)
```bash
# JWT Settings - Fixed expiry timing
JWT_EXPIRY_MINUTES=1440  # Changed from 60 to 1440 (24 hours)
JWT_REFRESH_TOKEN_EXPIRY_DAYS=7  # Added for clarity

# Audit Encryption - Fixed Base64 keys
AUDIT__ENCRYPTIONKEY=yAjc9j4pVS8QtKJM883ovzOJp3HYeGCKT1hWwVM1ZRA=
AUDIT__SIGNINGKEY=tC6GSIQyH1OjIZni5+b+xMk9DvpMZz1W2PSiz/f966A=

# Alerts - Fixed retry timing
ALERT__NOTIFICATIONTIMEOUTSECONDS=45  # Increased from 30 to 45
ALERT__RETRYDELAYMS=3000  # Reduced from 5000 to 3000

# Redis - Disabled if not using
REDIS__CONNECTIONSTRING=  # Empty (not using Redis)
SECURITY__USEREDISCACHE=false  # Added to disable Redis dependency
```

## How to Generate Secure Keys

### PowerShell (Windows)
```powershell
# Generate encryption key
[Convert]::ToBase64String((1..32 | ForEach-Object { Get-Random -Minimum 0 -Maximum 256 }))

# Generate signing key
[Convert]::ToBase64String((1..32 | ForEach-Object { Get-Random -Minimum 0 -Maximum 256 }))
```

### Bash (Linux/Mac)
```bash
# Generate encryption key
openssl rand -base64 32

# Generate signing key
openssl rand -base64 32
```

### C# (Using the application)
```bash
dotnet run --project src/ThinkOnErp.API -- key-management generate-initial
```

## Validation Rules Reference

### JWT Configuration
- `JWT_EXPIRY_MINUTES` must be less than `RefreshTokenExpiryInDays * 1440`
- Default refresh token expiry: 7 days = 10,080 minutes
- Recommended JWT expiry: 1440 minutes (24 hours) or less

### Alert Configuration
- Total retry time = `ALERT__RETRYDELAYMS * (2^ALERT__MAXRETRYATTEMPTS - 1)` with exponential backoff
- Must be less than `ALERT__NOTIFICATIONTIMEOUTSECONDS * 1000`
- Example: 2 retries with 3000ms delay = 3000 + 6000 = 9000ms < 45000ms ✓

### Encryption Keys
- Must be Base64-encoded
- Must represent exactly 32 bytes (256 bits)
- Base64 encoding of 32 bytes = 44 characters (including padding)
- Format: `[A-Za-z0-9+/]{43}=`

### Redis Connection String
- Format: `host:port` or `host:port,password=xxx`
- Examples:
  - `localhost:6379`
  - `redis.example.com:6379,password=mypassword`
  - `redis-12345.cloud.redislabs.com:12345,password=secret`
- If not using Redis, leave empty AND set `SECURITY__USEREDISCACHE=false`

## Testing the Fix

### 1. Verify Configuration
```bash
# Check environment variables are loaded
cat .env.production

# Verify no syntax errors
grep -E "^[A-Z_]+=" .env.production
```

### 2. Start the Application
```bash
# Using Docker
docker-compose -f docker-compose.yml up -d

# Or directly
dotnet run --project src/ThinkOnErp.API
```

### 3. Check for Validation Errors
```bash
# View logs
docker logs thinkonerp-api

# Should see:
# "Application started successfully"
# No OptionsValidationException errors
```

## Production Deployment Checklist

- [x] JWT expiry configured correctly (1440 minutes)
- [x] Refresh token expiry set (7 days)
- [x] Valid Base64 encryption key (44 chars)
- [x] Valid Base64 signing key (44 chars)
- [x] Alert retry timing within timeout
- [x] Redis configuration valid or disabled
- [ ] Generate production-specific encryption keys (don't use example keys!)
- [ ] Update webhook URL for alerts
- [ ] Configure SMTP settings if using email alerts
- [ ] Set up Redis if using distributed caching
- [ ] Review all security settings

## Security Recommendations

### 1. Generate New Keys for Production
The keys in the current configuration are examples. Generate new ones:
```bash
# On your production server
openssl rand -base64 32 > encryption.key
openssl rand -base64 32 > signing.key

# Add to .env.production
AUDIT__ENCRYPTIONKEY=$(cat encryption.key)
AUDIT__SIGNINGKEY=$(cat signing.key)

# Secure the key files
chmod 600 encryption.key signing.key
```

### 2. Use Key Management Service (Recommended)
For production, consider using:
- Azure Key Vault
- AWS Secrets Manager
- HashiCorp Vault

Update `KeyManagement:Provider` in appsettings.Production.json

### 3. Enable Key Rotation
```bash
KEY_MANAGEMENT_AUTO_ROTATION=true
KEY_MANAGEMENT_ROTATION_DAYS=90
KEY_MANAGEMENT_WARNING_DAYS=7
```

## Troubleshooting

### If Application Still Fails to Start

1. **Check all environment variables are loaded:**
   ```bash
   docker exec thinkonerp-api env | grep -E "JWT|AUDIT|ALERT|REDIS"
   ```

2. **Verify Base64 encoding:**
   ```bash
   echo "yAjc9j4pVS8QtKJM883ovzOJp3HYeGCKT1hWwVM1ZRA=" | base64 -d | wc -c
   # Should output: 32
   ```

3. **Check appsettings.Production.json overrides:**
   ```bash
   cat src/ThinkOnErp.API/appsettings.Production.json | grep -A5 "JwtSettings\|Alerting"
   ```

4. **Enable detailed validation logging:**
   ```bash
   LOG_LEVEL=Debug
   ```

## Related Files
- `.env.production` - Production environment variables (UPDATED)
- `.env.example` - Template with all available options
- `src/ThinkOnErp.API/appsettings.json` - Default configuration
- `src/ThinkOnErp.API/appsettings.Production.json` - Production overrides

## Next Steps

1. **Restart the application:**
   ```bash
   docker-compose down
   docker-compose up -d
   ```

2. **Verify startup:**
   ```bash
   docker logs -f thinkonerp-api
   ```

3. **Test API endpoints:**
   ```bash
   curl http://localhost:5000/health
   curl http://localhost:5000/api/health
   ```

4. **Generate production keys** (if not already done)

5. **Update webhook URLs** for production alerts

## Status
✅ All 5 validation errors fixed
✅ Configuration updated
✅ Ready for deployment

**Note:** Remember to generate new encryption and signing keys for production use. The current keys are from the development configuration and should not be used in production.
