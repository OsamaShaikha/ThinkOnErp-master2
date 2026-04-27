# Audit Logging Alerts Configuration Guide

## Overview

This document describes the alert configuration for monitoring audit logging system health and detecting failures. The audit logging system includes comprehensive monitoring and alerting to ensure no audit data is lost and system administrators are notified of any issues.

## Alert Rules for Audit Logging

### 1. Circuit Breaker Open Alert

**Alert Rule:** `AuditCircuitBreakerOpen`

**Severity:** Critical

**Description:** Triggered when the audit logging circuit breaker opens due to repeated database failures. When the circuit breaker is open, audit writes are being queued to fallback storage (file system) instead of the database.

**Configuration:**
```json
{
  "AuditCircuitBreakerOpen": {
    "Enabled": true,
    "Severity": "Critical",
    "Description": "Audit logging circuit breaker is open - audit writes are failing and being queued to fallback storage",
    "Channels": ["Email", "Webhook"],
    "CheckIntervalMinutes": 1,
    "RateLimitPerHour": 12,
    "AutoResolve": true,
    "AutoResolveCondition": "CircuitBreakerClosed"
  }
}
```

**When This Alert Fires:**
- The circuit breaker has detected 5 consecutive database write failures (default threshold)
- Audit events are being written to fallback file storage
- Database connectivity or performance issues are preventing audit writes

**Response Actions:**
1. Check database connectivity and health
2. Review database logs for errors
3. Check connection pool utilization
4. Verify database disk space
5. Monitor fallback file directory for accumulating events
6. Once database is healthy, circuit breaker will automatically close and replay fallback events

**Auto-Resolution:** This alert automatically resolves when the circuit breaker closes (database becomes available again).

---

### 2. Queue Overflow Warning

**Alert Rule:** `AuditQueueOverflow`

**Severity:** High

**Description:** Triggered when the audit logging queue reaches 90% capacity. This indicates the system is under heavy load or database writes are slower than event generation rate.

**Configuration:**
```json
{
  "AuditQueueOverflow": {
    "Enabled": true,
    "Severity": "High",
    "ThresholdPercentage": 90,
    "Description": "Audit logging queue is at 90% capacity - system may be under heavy load or database is slow",
    "Channels": ["Email"],
    "CheckIntervalMinutes": 5,
    "RateLimitPerHour": 6
  }
}
```

**When This Alert Fires:**
- Queue depth has reached 9,000 events (90% of default 10,000 capacity)
- Database writes are slower than event generation
- System is experiencing high traffic volume

**Response Actions:**
1. Monitor queue depth trend - is it increasing or stabilizing?
2. Check database query performance
3. Review slow query logs
4. Consider increasing batch size for more efficient writes
5. Check if database maintenance is running
6. Monitor API request volume

**Prevention:**
- Optimize database indexes for audit table
- Increase batch size if database can handle it
- Consider horizontal scaling if sustained high load

---

### 3. Queue Full Critical Alert

**Alert Rule:** `AuditQueueFull`

**Severity:** Critical

**Description:** Triggered when the audit logging queue is completely full. Backpressure is being applied, which may impact API response times.

**Configuration:**
```json
{
  "AuditQueueFull": {
    "Enabled": true,
    "Severity": "Critical",
    "ThresholdPercentage": 100,
    "Description": "Audit logging queue is full - backpressure is being applied and API performance may be impacted",
    "Channels": ["Email", "Webhook"],
    "CheckIntervalMinutes": 2,
    "RateLimitPerHour": 10
  }
}
```

**When This Alert Fires:**
- Queue has reached maximum capacity (10,000 events by default)
- New audit events are blocking until queue space is available
- API requests may experience increased latency due to backpressure

**Response Actions:**
1. **IMMEDIATE:** Check database connectivity - is it down?
2. Check if circuit breaker is open
3. Review database performance metrics
4. Check for long-running transactions blocking audit writes
5. Consider temporarily increasing MaxQueueSize in configuration
6. Monitor API response times for impact

**Critical Impact:** This condition directly impacts API performance. Resolve immediately.

---

### 4. Database Write Failure Alert

**Alert Rule:** `AuditDatabaseWriteFailure`

**Severity:** High

**Description:** Triggered when audit database write operations are failing repeatedly. Events are being retried or queued to fallback storage.

**Configuration:**
```json
{
  "AuditDatabaseWriteFailure": {
    "Enabled": true,
    "Severity": "High",
    "Description": "Audit database write operations are failing - events are being retried or queued to fallback storage",
    "Channels": ["Email"],
    "CheckIntervalMinutes": 5,
    "RateLimitPerHour": 6,
    "FailureThreshold": 5,
    "WindowMinutes": 10
  }
}
```

**When This Alert Fires:**
- 5 or more database write failures have occurred in a 10-minute window
- Transient database errors are occurring
- Database is experiencing intermittent connectivity issues

**Response Actions:**
1. Check database error logs for specific error codes
2. Review Oracle error codes (ORA-xxxxx) in logs
3. Check network connectivity to database
4. Verify database credentials are valid
5. Check if database is in maintenance mode
6. Monitor retry success rate

**Common Causes:**
- Network timeouts
- Database deadlocks
- Connection pool exhaustion
- Database maintenance windows
- Disk space issues on database server

---

### 5. Fallback Storage Activated Alert

**Alert Rule:** `AuditFallbackActivated`

**Severity:** High

**Description:** Triggered when audit logging has fallen back to file system storage because the database is unavailable.

**Configuration:**
```json
{
  "AuditFallbackActivated": {
    "Enabled": true,
    "Severity": "High",
    "Description": "Audit logging has fallen back to file system storage - database is unavailable",
    "Channels": ["Email", "Webhook"],
    "CheckIntervalMinutes": 5,
    "RateLimitPerHour": 6,
    "AutoResolve": true,
    "AutoResolveCondition": "DatabaseAvailable"
  }
}
```

**When This Alert Fires:**
- Circuit breaker is open and fallback storage is active
- Audit events are being written to `AuditFallback` directory
- Database is unavailable or experiencing severe issues

**Response Actions:**
1. Restore database connectivity
2. Monitor fallback directory size: `AuditFallback/`
3. Check disk space on application server
4. Once database is restored, fallback events will automatically replay
5. Verify replay success after database recovery

**Fallback File Location:** `AuditFallback/audit-fallback-{timestamp}.json`

**Auto-Resolution:** This alert automatically resolves when database becomes available and circuit breaker closes.

---

### 6. Processing Delay Alert

**Alert Rule:** `AuditProcessingDelayed`

**Severity:** Medium

**Description:** Triggered when audit event processing is delayed beyond acceptable threshold (10 seconds).

**Configuration:**
```json
{
  "AuditProcessingDelayed": {
    "Enabled": true,
    "Severity": "Medium",
    "ThresholdSeconds": 10,
    "Description": "Audit event processing is delayed beyond acceptable threshold",
    "Channels": ["Email"],
    "CheckIntervalMinutes": 10,
    "RateLimitPerHour": 3
  }
}
```

**When This Alert Fires:**
- Oldest event in queue has been waiting more than 10 seconds
- Processing is slower than event generation rate
- Database writes are taking longer than expected

**Response Actions:**
1. Check database query performance
2. Review batch processing metrics
3. Check if database maintenance is running
4. Monitor queue depth trend
5. Consider optimizing batch size

---

### 7. Health Check Failed Alert

**Alert Rule:** `AuditHealthCheckFailed`

**Severity:** Critical

**Description:** Triggered when the audit logging system health check fails. The system may not be capturing audit events.

**Configuration:**
```json
{
  "AuditHealthCheckFailed": {
    "Enabled": true,
    "Severity": "Critical",
    "Description": "Audit logging system health check has failed - system may not be capturing audit events",
    "Channels": ["Email", "Webhook"],
    "CheckIntervalMinutes": 2,
    "RateLimitPerHour": 10,
    "ConsecutiveFailureThreshold": 3
  }
}
```

**When This Alert Fires:**
- Health check has failed 3 consecutive times
- Background processing task may have stopped
- Channel may be closed
- Repository health check failed

**Response Actions:**
1. **CRITICAL:** Audit events may not be captured
2. Check application logs for exceptions
3. Verify background service is running
4. Check database connectivity
5. Consider restarting the application if issue persists
6. Review recent deployments or configuration changes

**Health Check Endpoint:** `GET /api/health` includes audit logging health status

---

### 8. Fallback Replay Failed Alert

**Alert Rule:** `AuditFallbackReplayFailed`

**Severity:** High

**Description:** Triggered when the system fails to replay fallback audit events to the database after recovery.

**Configuration:**
```json
{
  "AuditFallbackReplayFailed": {
    "Enabled": true,
    "Severity": "High",
    "Description": "Failed to replay fallback audit events to database - manual intervention may be required",
    "Channels": ["Email"],
    "CheckIntervalMinutes": 15,
    "RateLimitPerHour": 4
  }
}
```

**When This Alert Fires:**
- Automatic replay of fallback events failed
- Fallback files remain in `AuditFallback/` directory
- Manual intervention may be required

**Response Actions:**
1. Check fallback directory for pending files
2. Review application logs for replay errors
3. Verify database is healthy and accepting writes
4. Manually trigger replay using monitoring endpoint
5. If replay continues to fail, investigate specific error messages

**Manual Replay Endpoint:** `POST /api/monitoring/audit/replay-fallback`

---

### 9. High Failure Rate Alert

**Alert Rule:** `AuditHighFailureRate`

**Severity:** High

**Description:** Triggered when the audit logging failure rate exceeds 20% in a 15-minute window.

**Configuration:**
```json
{
  "AuditHighFailureRate": {
    "Enabled": true,
    "Severity": "High",
    "ThresholdPercentage": 20,
    "WindowMinutes": 15,
    "Description": "Audit logging failure rate has exceeded 20% in the last 15 minutes",
    "Channels": ["Email"],
    "CheckIntervalMinutes": 5,
    "RateLimitPerHour": 6
  }
}
```

**When This Alert Fires:**
- More than 20% of audit write attempts are failing
- Indicates systemic issues with database or configuration
- May precede circuit breaker opening

**Response Actions:**
1. Review error logs for common failure patterns
2. Check database health and performance
3. Verify configuration is correct
4. Check for recent changes to database schema
5. Monitor if failure rate is increasing or stabilizing

---

## Alert Delivery Channels

### Email Notifications

Configure SMTP settings in `appsettings.json`:

```json
{
  "Alerting": {
    "Email": {
      "Enabled": true,
      "SmtpHost": "smtp.example.com",
      "SmtpPort": 587,
      "SmtpUsername": "alerts@thinkonerp.com",
      "SmtpPassword": "your-password",
      "SmtpUseSsl": true,
      "FromEmailAddress": "alerts@thinkonerp.com",
      "FromDisplayName": "ThinkOnErp Alerts",
      "DefaultRecipients": ["admin@thinkonerp.com", "ops@thinkonerp.com"]
    }
  }
}
```

**Email Alert Format:**
- Subject: `[{Severity}] {AlertTitle}`
- Body: Includes alert description, timestamp, correlation ID, and recommended actions
- HTML formatted with severity color coding

### Webhook Notifications

Configure webhook settings for integration with incident management systems (PagerDuty, Slack, Teams, etc.):

```json
{
  "Alerting": {
    "Webhook": {
      "Enabled": true,
      "DefaultUrl": "https://hooks.slack.com/services/YOUR/WEBHOOK/URL",
      "AuthHeaderName": "X-API-Key",
      "AuthHeaderValue": "your-api-key",
      "TimeoutSeconds": 30,
      "RetryOnFailure": true,
      "IncludeFullPayload": true
    }
  }
}
```

**Webhook Payload Format:**
```json
{
  "alertType": "AuditCircuitBreakerOpen",
  "severity": "Critical",
  "title": "Audit Circuit Breaker Open",
  "description": "Audit logging circuit breaker is open...",
  "triggeredAt": "2024-01-15T10:30:00Z",
  "correlationId": "abc-123-def",
  "metadata": {
    "circuitState": "Open",
    "queueDepth": 8500,
    "failureCount": 5
  }
}
```

### SMS Notifications (Optional)

Configure Twilio for SMS alerts:

```json
{
  "Alerting": {
    "Sms": {
      "Enabled": true,
      "Provider": "Twilio",
      "TwilioAccountSid": "your-account-sid",
      "TwilioAuthToken": "your-auth-token",
      "TwilioFromPhoneNumber": "+1234567890",
      "MaxSmsLength": 160,
      "DefaultRecipients": ["+1234567890"]
    }
  }
}
```

---

## Monitoring Audit Logging Health

### Health Check Endpoint

**Endpoint:** `GET /api/health`

**Response:**
```json
{
  "status": "Healthy",
  "checks": {
    "auditLogging": {
      "status": "Healthy",
      "description": "Audit logging system is operational",
      "data": {
        "queueDepth": 150,
        "queueCapacity": 10000,
        "circuitState": "Closed",
        "processingTaskRunning": true
      }
    }
  }
}
```

### Metrics Endpoint

**Endpoint:** `GET /api/monitoring/audit/metrics`

**Response:**
```json
{
  "queueDepth": 150,
  "queueCapacity": 10000,
  "queueUtilizationPercent": 1.5,
  "circuitBreakerState": "Closed",
  "totalRequests": 10000,
  "successfulRequests": 9950,
  "failedRequests": 50,
  "successRate": 99.5,
  "failureRate": 0.5,
  "pendingFallbackFiles": 0
}
```

### Fallback Status Endpoint

**Endpoint:** `GET /api/monitoring/audit/fallback-status`

**Response:**
```json
{
  "fallbackActive": false,
  "pendingFileCount": 0,
  "oldestFileTimestamp": null,
  "totalPendingEvents": 0,
  "fallbackDirectory": "AuditFallback"
}
```

---

## Alert Rate Limiting

All alerts are rate-limited to prevent flooding:

- **Default:** Maximum 10 alerts per rule per hour
- **Configurable:** Set `RateLimitPerHour` per alert rule
- **Rate Limit Window:** 60 minutes (configurable via `RateLimitWindowMinutes`)

When rate limit is exceeded, alerts are suppressed and a warning is logged.

---

## Testing Alert Configuration

### 1. Test Email Delivery

```bash
curl -X POST https://your-api/api/monitoring/test-alert \
  -H "Content-Type: application/json" \
  -d '{
    "alertType": "Test",
    "severity": "Low",
    "channels": ["Email"]
  }'
```

### 2. Simulate Circuit Breaker Open

Temporarily disable database connectivity to trigger circuit breaker:

```bash
# Stop database or block network access
# Monitor logs for circuit breaker state changes
# Verify fallback activation alert is sent
```

### 3. Simulate Queue Overflow

Generate high volume of audit events:

```bash
# Use load testing tool to generate 10,000+ requests/minute
# Monitor queue depth metrics
# Verify queue overflow alert is sent at 90% threshold
```

---

## Troubleshooting

### Alert Not Firing

1. Check if alerting is enabled: `"Alerting": { "Enabled": true }`
2. Check if specific alert rule is enabled
3. Verify notification channels are configured
4. Check rate limiting - may be suppressed due to rate limit
5. Review application logs for alert processing errors

### Alert Flooding

1. Check rate limit configuration
2. Verify `RateLimitPerHour` is set appropriately
3. Consider increasing rate limit window
4. Review alert conditions - may be too sensitive

### Email Not Delivered

1. Verify SMTP configuration
2. Check SMTP credentials
3. Test SMTP connectivity: `telnet smtp.example.com 587`
4. Review email server logs
5. Check spam/junk folders

### Webhook Not Delivered

1. Verify webhook URL is accessible
2. Check authentication headers
3. Review webhook endpoint logs
4. Test webhook manually with curl
5. Check timeout settings

---

## Best Practices

1. **Configure Multiple Channels:** Use both email and webhook for critical alerts
2. **Set Appropriate Thresholds:** Tune thresholds based on your environment
3. **Monitor Alert Volume:** Review alert frequency and adjust rate limits
4. **Test Regularly:** Periodically test alert delivery
5. **Document Runbooks:** Create response procedures for each alert type
6. **Review and Tune:** Regularly review alert effectiveness and adjust configuration

---

## Related Documentation

- [Operational Runbooks](OPERATIONAL_RUNBOOKS.md)
- [APM Configuration Guide](APM_CONFIGURATION_GUIDE.md)
- [Audit Logging Graceful Degradation](../AUDIT_LOGGING_GRACEFUL_DEGRADATION_IMPLEMENTATION.md)
- [Circuit Breaker Tests](../CIRCUIT_BREAKER_TESTS_IMPLEMENTATION.md)

---

## Configuration Reference

For complete configuration options, see:
- `src/ThinkOnErp.Infrastructure/Configuration/AlertingOptions.cs`
- `src/ThinkOnErp.API/appsettings.json` - Section: `Alerting`

---

## Support

For issues or questions about audit logging alerts:
1. Check application logs in `Logs/` directory
2. Review health check endpoint: `/api/health`
3. Check metrics endpoint: `/api/monitoring/audit/metrics`
4. Contact system administrator or DevOps team
