# Configuration Migration Guide for Full Traceability System

## Overview

This guide provides step-by-step instructions for migrating existing ThinkOnErp installations to include the Full Traceability System. The migration process is designed to be non-disruptive and can be performed incrementally with minimal downtime.

**Target Audience**: System administrators, DevOps engineers, and technical staff responsible for ThinkOnErp deployments.

**Estimated Migration Time**: 2-4 hours (depending on environment complexity)

---

## Table of Contents

1. [Pre-Migration Checklist](#pre-migration-checklist)
2. [Database Migration](#database-migration)
3. [Configuration Updates](#configuration-updates)
4. [Service Registration](#service-registration)
5. [Security Key Generation](#security-key-generation)
6. [External Dependencies](#external-dependencies)
7. [Validation and Testing](#validation-and-testing)
8. [Rollback Procedures](#rollback-procedures)
9. [Performance Tuning](#performance-tuning)
10. [Troubleshooting](#troubleshooting)

---

## Pre-Migration Checklist

Before starting the migration, ensure you have:

- [ ] **Full database backup** (including SYS_AUDIT_LOG table)
- [ ] **Application configuration backup** (appsettings.json, environment variables)
- [ ] **Read access** to production logs for baseline performance metrics
- [ ] **Write access** to database for schema changes
- [ ] **Downtime window scheduled** (recommended: 30 minutes for database changes)
- [ ] **Rollback plan documented** and tested in staging environment
- [ ] **Monitoring tools configured** to track system health during migration
- [ ] **Stakeholder notification** sent for planned maintenance window

### System Requirements

- **Database**: Oracle 11g or higher with SYS_AUDIT_LOG table
- **.NET Runtime**: .NET 8.0 or higher
- **Memory**: Minimum 2GB additional RAM for audit queue and background services
- **Disk Space**: Minimum 10GB for audit logs (adjust based on retention policies)
- **Optional**: Redis 6.0+ for caching and security monitoring
- **Optional**: SMTP server for email alerts
- **Optional**: S3/Azure Blob Storage for long-term archival

---

## Database Migration

### Step 1: Verify Existing Schema

First, verify that the SYS_AUDIT_LOG table exists and check its current structure:

```sql
-- Check if SYS_AUDIT_LOG exists
SELECT table_name FROM user_tables WHERE table_name = 'SYS_AUDIT_LOG';

-- View current columns
SELECT column_name, data_type, data_length, nullable
FROM user_tab_columns
WHERE table_name = 'SYS_AUDIT_LOG'
ORDER BY column_id;
```

### Step 2: Run Database Migration Script

Execute the traceability system database migration script:

```bash
# Navigate to database scripts directory
cd Database/Scripts

# Run the migration script (requires DBA privileges)
sqlplus THINKON_ERP/THINKON_ERP@XEPDB1 @13_Extend_SYS_AUDIT_LOG_For_Traceability.sql
```

**What this script does**:
- Adds new columns to SYS_AUDIT_LOG (CORRELATION_ID, BRANCH_ID, HTTP_METHOD, etc.)
- Creates foreign key constraints
- Creates performance indexes
- Creates supporting tables (SYS_AUDIT_STATUS_TRACKING, SYS_AUDIT_LOG_ARCHIVE, etc.)
- Adds table and column comments

### Step 3: Verify Migration Success

```sql
-- Verify new columns were added
SELECT column_name FROM user_tab_columns 
WHERE table_name = 'SYS_AUDIT_LOG' 
AND column_name IN ('CORRELATION_ID', 'BRANCH_ID', 'HTTP_METHOD', 'ENDPOINT_PATH');

-- Verify indexes were created
SELECT index_name FROM user_indexes 
WHERE table_name = 'SYS_AUDIT_LOG' 
AND index_name LIKE 'IDX_AUDIT_LOG_%';

-- Verify supporting tables were created
SELECT table_name FROM user_tables 
WHERE table_name IN ('SYS_AUDIT_STATUS_TRACKING', 'SYS_AUDIT_LOG_ARCHIVE', 
                     'SYS_PERFORMANCE_METRICS', 'SYS_SLOW_QUERIES', 
                     'SYS_SECURITY_THREATS', 'SYS_FAILED_LOGINS');
```

### Step 4: Run Additional Migration Scripts

Execute the performance metrics and retention policy scripts:

```bash
# Create performance metrics tables
sqlplus THINKON_ERP/THINKON_ERP@XEPDB1 @15_Create_Performance_Metrics_Tables.sql

# Verify all tables are created
sqlplus THINKON_ERP/THINKON_ERP@XEPDB1 @tables.sql
```

---

## Configuration Updates

### Step 1: Backup Existing Configuration

```bash
# Backup current appsettings.json
cp src/ThinkOnErp.API/appsettings.json src/ThinkOnErp.API/appsettings.json.backup.$(date +%Y%m%d_%H%M%S)

# Backup environment-specific configurations
cp src/ThinkOnErp.API/appsettings.Production.json src/ThinkOnErp.API/appsettings.Production.json.backup.$(date +%Y%m%d_%H%M%S)
```

### Step 2: Add Traceability Configuration Sections

Add the following configuration sections to your `appsettings.json`. You can start with minimal configuration and enable features incrementally.

#### Minimal Configuration (Required)

```json
{
  "AuditLogging": {
    "Enabled": true,
    "BatchSize": 50,
    "BatchWindowMs": 100,
    "MaxQueueSize": 10000,
    "SensitiveFields": ["password", "token", "refreshToken", "creditCard", "ssn"],
    "MaskingPattern": "***MASKED***"
  },
  "RequestTracing": {
    "Enabled": true,
    "LogPayloads": true,
    "PayloadLoggingLevel": "Full",
    "MaxPayloadSize": 10240,
    "ExcludedPaths": ["/health", "/metrics", "/swagger"],
    "CorrelationIdHeader": "X-Correlation-ID"
  }
}
```

#### Recommended Configuration (Production)

For production environments, add these additional sections:

```json
{
  "PerformanceMonitoring": {
    "Enabled": true,
    "SlowRequestThresholdMs": 1000,
    "SlowQueryThresholdMs": 500,
    "TrackMemoryMetrics": true,
    "MetricsAggregation": {
      "Enabled": true,
      "IntervalMinutes": 60
    }
  },
  "SecurityMonitoring": {
    "Enabled": true,
    "FailedLoginThreshold": 5,
    "FailedLoginWindowMinutes": 5,
    "EnableSqlInjectionDetection": true,
    "EnableXssDetection": true
  },
  "Archival": {
    "Enabled": true,
    "Schedule": "0 2 * * *",
    "BatchSize": 1000,
    "CompressionEnabled": true,
    "RetentionPolicies": {
      "Authentication": 365,
      "DataChange": 1095,
      "Financial": 2555,
      "PersonalData": 1095,
      "Security": 730
    }
  }
}
```

### Step 3: Update Connection Strings

If using Redis for caching or security monitoring:

```json
{
  "ConnectionStrings": {
    "OracleDb": "your-existing-connection-string",
    "Redis": "localhost:6379,abortConnect=false,connectTimeout=5000,syncTimeout=5000"
  }
}
```

### Step 4: Configure Environment-Specific Settings

Create or update `appsettings.Production.json`:

```json
{
  "AuditLogging": {
    "Enabled": true,
    "LogSuccessfulOperations": false,
    "EnableFileSystemFallback": true
  },
  "RequestTracing": {
    "LogPayloads": false,
    "PayloadLoggingLevel": "MetadataOnly"
  },
  "PerformanceMonitoring": {
    "Enabled": true,
    "TrackMemoryMetrics": true
  },
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "Microsoft.AspNetCore": "Warning"
      }
    }
  }
}
```

---

## Service Registration

### Step 1: Verify DependencyInjection.cs

The traceability services are already registered in `src/ThinkOnErp.Infrastructure/DependencyInjection.cs`. Verify the following services are registered:

```csharp
// Audit logging services
services.AddScoped<IAuditRepository, AuditRepository>();
services.AddScoped<ISensitiveDataMasker, SensitiveDataMasker>();
services.AddSingleton<IAuditLogger, AuditLogger>();
services.AddHostedService<AuditLogger>();

// Performance monitoring
services.AddSingleton<IPerformanceMonitor, PerformanceMonitor>();
services.AddHostedService<MetricsAggregationBackgroundService>();

// Security monitoring
services.AddScoped<ISecurityMonitor, SecurityMonitor>();

// Compliance reporting
services.AddScoped<IComplianceReporter, ComplianceReporter>();
services.AddHostedService<ScheduledReportGenerationService>();

// Archival services
services.AddScoped<IArchivalService, ArchivalService>();
services.AddHostedService<ArchivalBackgroundService>();

// Alert management
services.AddSingleton<IAlertManager, AlertManager>();
services.AddHostedService<AlertProcessingBackgroundService>();
```

### Step 2: Verify Middleware Registration

Check `src/ThinkOnErp.API/Program.cs` to ensure middleware is registered in the correct order:

```csharp
// Request tracing middleware (MUST be first to generate correlation IDs)
app.UseMiddleware<RequestTracingMiddleware>();

// Exception handling middleware (MUST be after request tracing)
app.UseMiddleware<ExceptionHandlingMiddleware>();

// Standard ASP.NET Core middleware
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
```

### Step 3: Verify Serilog Configuration

Ensure Serilog is configured with the CorrelationIdEnricher:

```csharp
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithThreadId()
    .Enrich.With<CorrelationIdEnricher>()  // REQUIRED for correlation ID logging
    .WriteTo.Console()
    .WriteTo.File("logs/log-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();
```

---

## Security Key Generation

### Step 1: Generate Encryption Key (Optional)

If enabling audit data encryption:

```bash
# Generate a 32-byte (256-bit) encryption key
dotnet run --project src/ThinkOnErp.Infrastructure -- keygen encryption

# Output will be a Base64-encoded key like:
# Generated Encryption Key: Kx9mP2vR5tY8wB1eH4jL7nQ0sU3xA6zC9fI2kN5oR8u=
```

Add to appsettings.json:

```json
{
  "AuditEncryption": {
    "Enabled": true,
    "Key": "Kx9mP2vR5tY8wB1eH4jL7nQ0sU3xA6zC9fI2kN5oR8u=",
    "EncryptOldValue": true,
    "EncryptNewValue": true
  }
}
```

### Step 2: Generate Signing Key (Recommended)

For audit log integrity verification:

```bash
# Generate a signing key for HMAC-SHA256
dotnet run --project src/ThinkOnErp.Infrastructure -- keygen signing

# Output will be a Base64-encoded key
```

Add to appsettings.json:

```json
{
  "AuditIntegrity": {
    "Enabled": true,
    "SigningKey": "YOUR_GENERATED_SIGNING_KEY_HERE",
    "AutoGenerateHashes": true,
    "VerifyOnRead": false
  }
}
```

### Step 3: Secure Key Storage

**Production Best Practices**:

1. **Environment Variables** (Recommended):
```bash
# Set environment variables
export AUDIT_ENCRYPTION_KEY="your-encryption-key"
export AUDIT_SIGNING_KEY="your-signing-key"
```

Update appsettings.json to reference environment variables:
```json
{
  "KeyManagement": {
    "Provider": "Configuration",
    "Configuration": {
      "UseEnvironmentVariables": true,
      "EncryptionKeyEnvironmentVariable": "AUDIT_ENCRYPTION_KEY",
      "SigningKeyEnvironmentVariable": "AUDIT_SIGNING_KEY"
    }
  }
}
```

2. **Azure Key Vault** (Enterprise):
```json
{
  "KeyManagement": {
    "Provider": "AzureKeyVault",
    "AzureKeyVault": {
      "VaultUrl": "https://your-vault.vault.azure.net/",
      "EncryptionKeySecretName": "audit-encryption-key",
      "SigningKeySecretName": "audit-signing-key",
      "AuthenticationMethod": "ManagedIdentity"
    }
  }
}
```

3. **AWS Secrets Manager** (Enterprise):
```json
{
  "KeyManagement": {
    "Provider": "AwsSecretsManager",
    "AwsSecretsManager": {
      "Region": "us-east-1",
      "EncryptionKeySecretName": "audit/encryption-key",
      "SigningKeySecretName": "audit/signing-key",
      "AuthenticationMethod": "IAMRole"
    }
  }
}
```

---

## External Dependencies

### Redis (Optional but Recommended)

Redis is used for:
- Security monitoring (failed login tracking)
- Audit query result caching
- Distributed locking for background services

#### Installation

**Docker**:
```bash
docker run -d --name redis -p 6379:6379 redis:7-alpine
```

**Ubuntu/Debian**:
```bash
sudo apt-get install redis-server
sudo systemctl start redis-server
sudo systemctl enable redis-server
```

**Windows**:
```powershell
# Using Chocolatey
choco install redis-64

# Or download from: https://github.com/microsoftarchive/redis/releases
```

#### Configuration

```json
{
  "ConnectionStrings": {
    "Redis": "localhost:6379,abortConnect=false,connectTimeout=5000,syncTimeout=5000"
  },
  "SecurityMonitoring": {
    "UseRedisCache": true,
    "RedisConnectionString": "localhost:6379"
  },
  "AuditQueryCaching": {
    "Enabled": true,
    "RedisConnectionString": "localhost:6379"
  }
}
```

### SMTP Server (Optional)

For email alerts and scheduled reports:

```json
{
  "Alerting": {
    "Email": {
      "Enabled": true,
      "SmtpHost": "smtp.gmail.com",
      "SmtpPort": 587,
      "SmtpUsername": "your-email@gmail.com",
      "SmtpPassword": "your-app-password",
      "SmtpUseSsl": true,
      "FromEmailAddress": "alerts@thinkonerp.com",
      "DefaultRecipients": ["admin@thinkonerp.com"]
    }
  }
}
```

### External Storage (Optional)

For long-term audit log archival:

**AWS S3**:
```json
{
  "Archival": {
    "StorageProvider": "S3",
    "ExternalStorage": {
      "Enabled": true,
      "Provider": "S3",
      "S3": {
        "BucketName": "thinkonerp-audit-archive",
        "Region": "us-east-1",
        "AccessKeyId": "YOUR_ACCESS_KEY",
        "SecretAccessKey": "YOUR_SECRET_KEY",
        "UseServerSideEncryption": true
      }
    }
  }
}
```

**Azure Blob Storage**:
```json
{
  "Archival": {
    "StorageProvider": "Azure",
    "ExternalStorage": {
      "Enabled": true,
      "Provider": "Azure",
      "Azure": {
        "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=...",
        "ContainerName": "audit-archive",
        "UseEncryption": true
      }
    }
  }
}
```

---

## Validation and Testing

### Step 1: Start Application

```bash
# Development
dotnet run --project src/ThinkOnErp.API

# Production
dotnet src/ThinkOnErp.API/bin/Release/net8.0/ThinkOnErp.API.dll
```

### Step 2: Check Application Logs

Look for successful initialization messages:

```
[INFO] AuditLogger: Audit logging service started with batch size 50
[INFO] PerformanceMonitor: Performance monitoring service started
[INFO] SecurityMonitor: Security monitoring service started
[INFO] ArchivalService: Archival service scheduled for 02:00 UTC daily
```

### Step 3: Verify Database Connectivity

```bash
# Check audit log writes
curl -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"testuser","password":"testpass"}'

# Query audit logs
SELECT * FROM SYS_AUDIT_LOG 
WHERE CREATION_DATE > SYSDATE - 1/24 
ORDER BY CREATION_DATE DESC;
```

### Step 4: Test Correlation ID Propagation

```bash
# Make a request and capture correlation ID
CORRELATION_ID=$(curl -i http://localhost:5000/api/companies | grep X-Correlation-ID | awk '{print $2}')

# Verify correlation ID in logs
grep "$CORRELATION_ID" logs/log-*.txt
```

### Step 5: Test Performance Monitoring

```bash
# Make several requests to generate metrics
for i in {1..10}; do
  curl http://localhost:5000/api/companies
done

# Check performance metrics
SELECT * FROM SYS_PERFORMANCE_METRICS 
WHERE CREATION_DATE > SYSDATE - 1/24;
```

### Step 6: Test Security Monitoring

```bash
# Trigger failed login detection
for i in {1..6}; do
  curl -X POST http://localhost:5000/api/auth/login \
    -H "Content-Type: application/json" \
    -d '{"username":"testuser","password":"wrongpassword"}'
done

# Check security threats
SELECT * FROM SYS_SECURITY_THREATS 
WHERE CREATION_DATE > SYSDATE - 1/24;
```

### Step 7: Verify Background Services

```bash
# Check that background services are running
ps aux | grep ThinkOnErp.API

# Check service health
curl http://localhost:5000/health
```

---

## Rollback Procedures

### Emergency Rollback (< 1 hour after migration)

If critical issues occur immediately after migration:

1. **Stop the application**:
```bash
# Stop the service
sudo systemctl stop thinkonerp-api

# Or kill the process
pkill -f ThinkOnErp.API
```

2. **Restore configuration**:
```bash
# Restore backup
cp src/ThinkOnErp.API/appsettings.json.backup.* src/ThinkOnErp.API/appsettings.json
```

3. **Rollback database changes** (if necessary):
```sql
-- Drop new columns (data will be lost)
ALTER TABLE SYS_AUDIT_LOG DROP COLUMN CORRELATION_ID;
ALTER TABLE SYS_AUDIT_LOG DROP COLUMN BRANCH_ID;
-- ... (drop other new columns)

-- Drop new tables
DROP TABLE SYS_AUDIT_STATUS_TRACKING;
DROP TABLE SYS_AUDIT_LOG_ARCHIVE;
DROP TABLE SYS_PERFORMANCE_METRICS;
-- ... (drop other new tables)
```

4. **Restart with old configuration**:
```bash
sudo systemctl start thinkonerp-api
```

### Partial Rollback (Disable Features)

If specific features are causing issues, disable them without full rollback:

```json
{
  "AuditLogging": {
    "Enabled": false  // Disable audit logging
  },
  "PerformanceMonitoring": {
    "Enabled": false  // Disable performance monitoring
  },
  "SecurityMonitoring": {
    "Enabled": false  // Disable security monitoring
  },
  "Archival": {
    "Enabled": false  // Disable archival
  }
}
```

Restart the application to apply changes.

---

## Performance Tuning

### Database Connection Pooling

Optimize Oracle connection pool settings:

```json
{
  "ConnectionStrings": {
    "OracleDb": "Data Source=...;Pooling=true;Min Pool Size=10;Max Pool Size=200;Connection Timeout=15;Incr Pool Size=10;Decr Pool Size=5;Connection Lifetime=600;"
  }
}
```

### Audit Logging Performance

Adjust batch processing parameters based on load:

```json
{
  "AuditLogging": {
    "BatchSize": 100,           // Increase for high-volume systems
    "BatchWindowMs": 50,        // Decrease for faster writes
    "MaxQueueSize": 20000,      // Increase for burst traffic
    "DatabaseTimeoutSeconds": 60 // Increase for slow databases
  }
}
```

### Memory Optimization

Configure memory limits for background services:

```json
{
  "PerformanceMonitoring": {
    "SlidingWindowSizeMinutes": 30,  // Reduce to save memory
    "MetricsRetentionHours": 12      // Reduce to save memory
  }
}
```

### Query Performance

Enable query result caching:

```json
{
  "AuditQueryCaching": {
    "Enabled": true,
    "CacheDurationMinutes": 10,
    "MaxCachedResultSizeKB": 2048
  }
}
```

---

## Troubleshooting

### Issue: Application fails to start

**Symptoms**: Application crashes on startup with configuration errors.

**Solution**:
1. Check configuration validation errors in logs
2. Verify all required configuration sections are present
3. Ensure database connection string is correct
4. Check that Oracle database is accessible

```bash
# Test database connectivity
sqlplus THINKON_ERP/THINKON_ERP@XEPDB1

# Check application logs
tail -f logs/log-*.txt
```

### Issue: Audit logs not being written

**Symptoms**: No entries in SYS_AUDIT_LOG after migration.

**Solution**:
1. Check that AuditLogging.Enabled is true
2. Verify database permissions for INSERT on SYS_AUDIT_LOG
3. Check for circuit breaker activation in logs
4. Verify background service is running

```bash
# Check audit logger status
grep "AuditLogger" logs/log-*.txt | tail -20

# Check for circuit breaker errors
grep "CircuitBreaker" logs/log-*.txt | tail -20
```

### Issue: High memory usage

**Symptoms**: Application memory usage increases over time.

**Solution**:
1. Reduce audit queue size
2. Decrease metrics retention period
3. Enable query result caching with size limits
4. Increase batch processing frequency

```json
{
  "AuditLogging": {
    "MaxQueueSize": 5000,
    "BatchWindowMs": 50
  },
  "PerformanceMonitoring": {
    "MetricsRetentionHours": 6
  }
}
```

### Issue: Slow API response times

**Symptoms**: API requests take longer after migration.

**Solution**:
1. Disable payload logging in production
2. Reduce sensitive data masking overhead
3. Optimize database indexes
4. Enable async audit writes (should be default)

```json
{
  "RequestTracing": {
    "LogPayloads": false,
    "PayloadLoggingLevel": "MetadataOnly"
  },
  "AuditLogging": {
    "SensitiveFields": ["password", "token"]  // Reduce list
  }
}
```

### Issue: Redis connection failures

**Symptoms**: Errors related to Redis connectivity.

**Solution**:
1. Verify Redis is running: `redis-cli ping`
2. Check connection string format
3. Disable Redis-dependent features if not needed

```json
{
  "SecurityMonitoring": {
    "UseRedisCache": false  // Fallback to in-memory
  },
  "AuditQueryCaching": {
    "Enabled": false  // Disable caching
  }
}
```

### Issue: Background services not running

**Symptoms**: Archival, metrics aggregation, or alerts not working.

**Solution**:
1. Check that services are registered in DependencyInjection.cs
2. Verify configuration for each background service
3. Check logs for service startup errors

```bash
# Check for background service errors
grep "BackgroundService" logs/log-*.txt | tail -50
```

---

## Post-Migration Checklist

After successful migration, verify:

- [ ] Application starts without errors
- [ ] Audit logs are being written to SYS_AUDIT_LOG
- [ ] Correlation IDs are present in logs and responses
- [ ] Performance metrics are being collected
- [ ] Security monitoring is detecting threats
- [ ] Background services are running (archival, metrics aggregation)
- [ ] API response times are within acceptable limits
- [ ] Memory usage is stable
- [ ] Database connection pool is healthy
- [ ] Alerts are being sent (if configured)
- [ ] Scheduled reports are generating (if configured)

---

## Support and Resources

### Documentation

- **Full Traceability System Design**: `.kiro/specs/full-traceability-system/design.md`
- **Requirements Document**: `.kiro/specs/full-traceability-system/requirements.md`
- **API Documentation**: `http://localhost:5000/swagger`

### Monitoring

- **Health Check Endpoint**: `GET /health`
- **Metrics Endpoint**: `GET /api/monitoring/health`
- **Audit Log Query**: `GET /api/auditlogs`

### Contact

For migration support, contact:
- **Email**: support@thinkonerp.com
- **Documentation**: https://docs.thinkonerp.com
- **Issue Tracker**: https://github.com/thinkonerp/issues

---

## Appendix A: Complete Configuration Reference

See `src/ThinkOnErp.API/appsettings.json` for a complete configuration example with all available options and their default values.

## Appendix B: Database Schema Changes

See `Database/Scripts/13_Extend_SYS_AUDIT_LOG_For_Traceability.sql` for complete database schema changes.

## Appendix C: Performance Benchmarks

Expected performance characteristics after migration:

- **Audit Write Latency**: < 50ms (95th percentile)
- **API Request Overhead**: < 10ms (99th percentile)
- **Memory Usage**: +500MB - 2GB (depending on configuration)
- **Database Connections**: +5-10 connections
- **Throughput**: 10,000+ requests/minute

---

**Document Version**: 1.0  
**Last Updated**: 2024-01-15  
**Maintained By**: ThinkOnErp Development Team
