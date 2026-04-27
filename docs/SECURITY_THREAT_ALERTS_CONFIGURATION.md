# Security Threat Alerts Configuration Guide

## Overview

This document describes the alert configuration for security threat monitoring in the ThinkOnErp API. The security monitoring system detects various types of security threats and triggers alerts through multiple notification channels (email, webhook, SMS) to enable rapid response to security incidents.

## Security Threat Types

The system monitors and alerts on the following security threat types:

### 1. Failed Login Pattern
**Threat Type:** `FailedLoginPattern`  
**Default Severity:** High  
**Description:** Multiple failed login attempts from the same IP address, indicating a potential brute force attack.

**Detection Method:**
- Tracks failed login attempts per IP address using Redis sliding window or database
- Triggers when threshold (default: 5 attempts) is exceeded within time window (default: 5 minutes)
- Uses distributed tracking for accurate detection across multiple API instances

**Alert Configuration:**
```json
"FailedLoginPattern": {
  "Enabled": true,
  "Severity": "High",
  "Description": "Multiple failed login attempts detected from the same IP address - potential brute force attack",
  "Channels": [ "Email", "Webhook" ],
  "CheckIntervalMinutes": 1,
  "RateLimitPerHour": 10,
  "FailedLoginThreshold": 5,
  "WindowMinutes": 5,
  "AutoBlock": false,
  "BlockDurationMinutes": 30
}
```

**Configuration Parameters:**
- `FailedLoginThreshold`: Number of failed attempts before triggering alert (default: 5)
- `WindowMinutes`: Time window for counting failed attempts (default: 5 minutes)
- `AutoBlock`: Whether to automatically block the IP address (default: false in dev, true in production)
- `BlockDurationMinutes`: Duration to block the IP if AutoBlock is enabled (default: 30 minutes)

**Response Actions:**
1. Review the IP address and failed login attempts in the alert
2. Check if the IP is from a known location or VPN
3. Consider blocking the IP address if attack continues
4. Review user accounts that were targeted
5. Enable AutoBlock in production if not already enabled

---

### 2. SQL Injection Attempt
**Threat Type:** `SqlInjectionAttempt`  
**Default Severity:** Critical  
**Description:** SQL injection pattern detected in request parameters, indicating an immediate security threat.

**Detection Method:**
- Scans all request parameters for SQL injection patterns
- Detects multiple attack types:
  - Classic SQL injection (UNION, SELECT, INSERT, UPDATE, DELETE, DROP)
  - Time-based blind SQL injection (WAITFOR, SLEEP, BENCHMARK)
  - Boolean-based blind SQL injection
  - Encoded SQL injection attempts (hex, URL encoding)
  - Stacked queries and command injection
  - Information schema and system table access
- Uses regex pattern matching with timeout protection
- Applies false positive filtering for legitimate business data

**Alert Configuration:**
```json
"SqlInjectionAttempt": {
  "Enabled": true,
  "Severity": "Critical",
  "Description": "SQL injection pattern detected in request parameters - immediate security threat",
  "Channels": [ "Email", "Webhook", "Sms" ],
  "CheckIntervalMinutes": 1,
  "RateLimitPerHour": 20,
  "AutoBlock": true,
  "BlockDurationMinutes": 60,
  "LogFullRequest": true
}
```

**Configuration Parameters:**
- `AutoBlock`: Automatically blocks the IP address (default: true)
- `BlockDurationMinutes`: Duration to block the IP (default: 60 minutes)
- `LogFullRequest`: Logs the complete request for forensic analysis (default: true)

**Response Actions:**
1. **IMMEDIATE:** Review the alert details and matched SQL injection pattern
2. **IMMEDIATE:** Verify the IP address is blocked (if AutoBlock is enabled)
3. Check if any database queries were executed from the malicious request
4. Review database audit logs for any unauthorized data access
5. Analyze the attack pattern to identify the vulnerability
6. Apply security patches or input validation fixes
7. Consider extending the block duration for repeat offenders
8. Report the incident to the security team

---

### 3. XSS (Cross-Site Scripting) Attempt
**Threat Type:** `XssAttempt`  
**Default Severity:** High  
**Description:** Cross-site scripting pattern detected in request parameters.

**Detection Method:**
- Scans request parameters for XSS patterns
- Detects:
  - Script tags and inline JavaScript
  - Event handlers (onerror, onload, onclick, etc.)
  - Iframe, object, embed, and applet tags
  - JavaScript protocol handlers
  - Expression and eval functions
- Uses regex pattern matching with timeout protection

**Alert Configuration:**
```json
"XssAttempt": {
  "Enabled": true,
  "Severity": "High",
  "Description": "Cross-site scripting (XSS) pattern detected in request parameters",
  "Channels": [ "Email", "Webhook" ],
  "CheckIntervalMinutes": 1,
  "RateLimitPerHour": 15,
  "AutoBlock": true,
  "BlockDurationMinutes": 30,
  "LogFullRequest": true
}
```

**Configuration Parameters:**
- `AutoBlock`: Automatically blocks the IP address (default: true)
- `BlockDurationMinutes`: Duration to block the IP (default: 30 minutes)
- `LogFullRequest`: Logs the complete request for forensic analysis (default: true)

**Response Actions:**
1. Review the alert details and matched XSS pattern
2. Verify the IP address is blocked (if AutoBlock is enabled)
3. Check if the malicious input was stored in the database
4. If stored, sanitize or remove the malicious content
5. Review output encoding and input validation in the affected endpoint
6. Apply security patches or input sanitization fixes
7. Test the endpoint with XSS payloads to verify the fix

---

### 4. Unauthorized Access Attempt
**Threat Type:** `UnauthorizedAccessAttempt`  
**Default Severity:** High  
**Description:** User attempted to access data outside their assigned company or branch, indicating potential privilege escalation.

**Detection Method:**
- Validates user access to company and branch data
- Checks user's assigned company and branch against requested resources
- Detects attempts to access data from other companies or branches
- Tracks unauthorized access patterns per user

**Alert Configuration:**
```json
"UnauthorizedAccessAttempt": {
  "Enabled": true,
  "Severity": "High",
  "Description": "User attempted to access data outside their assigned company or branch - potential privilege escalation",
  "Channels": [ "Email", "Webhook" ],
  "CheckIntervalMinutes": 1,
  "RateLimitPerHour": 10,
  "RequireInvestigation": true,
  "LogFullRequest": true
}
```

**Configuration Parameters:**
- `RequireInvestigation`: Flags the alert for mandatory investigation (default: true)
- `LogFullRequest`: Logs the complete request for forensic analysis (default: true)

**Response Actions:**
1. Review the user account and their assigned company/branch
2. Check if the access attempt was legitimate (e.g., user recently changed companies)
3. Review the user's recent activity for other suspicious behavior
4. Verify the user's permissions and role assignments
5. If malicious, disable the user account immediately
6. Investigate how the user obtained the company/branch IDs
7. Review access control implementation in the affected endpoint
8. Consider implementing additional authorization checks

---

### 5. Anomalous User Activity
**Threat Type:** `AnomalousUserActivity`  
**Default Severity:** Medium  
**Description:** Unusual activity pattern detected for a user, such as high request volume or unusual timing.

**Detection Method:**
- Tracks request volume per user over time windows
- Detects unusual request patterns:
  - High request volume (default: 1000 requests in 10 minutes)
  - Requests at unusual times (e.g., 3 AM for a user who normally works 9-5)
  - Rapid sequential requests to different endpoints
- Uses baseline behavior analysis for each user

**Alert Configuration:**
```json
"AnomalousUserActivity": {
  "Enabled": true,
  "Severity": "Medium",
  "Description": "Unusual activity pattern detected for user - high request volume or unusual timing",
  "Channels": [ "Email" ],
  "CheckIntervalMinutes": 5,
  "RateLimitPerHour": 6,
  "RequestVolumeThreshold": 1000,
  "WindowMinutes": 10,
  "RequireInvestigation": false
}
```

**Configuration Parameters:**
- `RequestVolumeThreshold`: Number of requests before triggering alert (default: 1000)
- `WindowMinutes`: Time window for counting requests (default: 10 minutes)
- `RequireInvestigation`: Whether the alert requires investigation (default: false)

**Response Actions:**
1. Review the user's recent activity and request patterns
2. Check if the activity is legitimate (e.g., automated script, data export)
3. Contact the user to verify the activity
4. If malicious, disable the user account and reset credentials
5. Review the user's permissions and recent permission changes
6. Adjust the threshold if false positives are common

---

### 6. Geographic Anomaly
**Threat Type:** `GeographicAnomaly`  
**Default Severity:** Medium  
**Description:** API request from an unusual geographic location for this user.

**Detection Method:**
- Uses GeoIP service to determine request origin
- Compares request location to user's typical locations
- Detects:
  - Requests from new countries
  - Impossible travel (e.g., US to China in 1 hour)
  - Requests from high-risk countries

**Alert Configuration:**
```json
"GeographicAnomaly": {
  "Enabled": false,
  "Severity": "Medium",
  "Description": "API request from unusual geographic location for this user",
  "Channels": [ "Email" ],
  "CheckIntervalMinutes": 10,
  "RateLimitPerHour": 3,
  "RequireGeoIpService": true,
  "Comment": "Requires GeoIP service integration - disabled by default"
}
```

**Configuration Parameters:**
- `RequireGeoIpService`: Indicates that GeoIP service is required (default: true)
- **Note:** This alert is disabled by default and requires GeoIP service integration

**Response Actions:**
1. Review the user's typical locations and the anomalous location
2. Check if the user is traveling or using a VPN
3. Contact the user to verify the login
4. If unauthorized, disable the user account and reset credentials
5. Review recent activity from the anomalous location

**Setup Requirements:**
- Integrate with a GeoIP service (e.g., MaxMind, IP2Location)
- Configure GeoIP service credentials in appsettings
- Enable the alert rule after GeoIP integration is complete

---

### 7. Rate Limit Exceeded
**Threat Type:** `RateLimitExceeded`  
**Default Severity:** Medium  
**Description:** Unusually high API request volume from a single user or IP address.

**Detection Method:**
- Tracks request volume per user and per IP address
- Detects when request volume exceeds threshold (default: 500 requests in 5 minutes)
- Distinguishes between legitimate high-volume usage and abuse

**Alert Configuration:**
```json
"RateLimitExceeded": {
  "Enabled": true,
  "Severity": "Medium",
  "Description": "Unusually high API request volume from single user or IP address",
  "Channels": [ "Email" ],
  "CheckIntervalMinutes": 5,
  "RateLimitPerHour": 6,
  "RequestThreshold": 500,
  "WindowMinutes": 5,
  "AutoThrottle": true
}
```

**Configuration Parameters:**
- `RequestThreshold`: Number of requests before triggering alert (default: 500)
- `WindowMinutes`: Time window for counting requests (default: 5 minutes)
- `AutoThrottle`: Automatically throttle requests from the user/IP (default: true)

**Response Actions:**
1. Review the user/IP and their request patterns
2. Check if the activity is legitimate (e.g., data export, batch processing)
3. If legitimate, whitelist the user/IP or increase their rate limit
4. If malicious, block the user/IP
5. Review the endpoints being accessed for potential abuse
6. Adjust the threshold if false positives are common

---

### 8. Privilege Escalation Attempt
**Threat Type:** `PrivilegeEscalationAttempt`  
**Default Severity:** Critical  
**Description:** Unauthorized permission elevation attempt detected, indicating a critical security threat.

**Detection Method:**
- Monitors permission changes and role assignments
- Detects attempts to:
  - Assign admin roles to non-admin users
  - Grant elevated permissions without authorization
  - Modify permission tables directly
  - Bypass authorization checks

**Alert Configuration:**
```json
"PrivilegeEscalationAttempt": {
  "Enabled": true,
  "Severity": "Critical",
  "Description": "Unauthorized permission elevation attempt detected - critical security threat",
  "Channels": [ "Email", "Webhook", "Sms" ],
  "CheckIntervalMinutes": 1,
  "RateLimitPerHour": 20,
  "RequireInvestigation": true,
  "AutoBlock": true,
  "BlockDurationMinutes": 120,
  "LogFullRequest": true,
  "NotifySecurityTeam": true
}
```

**Configuration Parameters:**
- `RequireInvestigation`: Flags the alert for mandatory investigation (default: true)
- `AutoBlock`: Automatically blocks the user/IP (default: true)
- `BlockDurationMinutes`: Duration to block (default: 120 minutes)
- `LogFullRequest`: Logs the complete request for forensic analysis (default: true)
- `NotifySecurityTeam`: Sends additional notification to security team (default: true)

**Response Actions:**
1. **IMMEDIATE:** Disable the user account that attempted privilege escalation
2. **IMMEDIATE:** Review and revert any unauthorized permission changes
3. Review the user's recent activity and permission changes
4. Investigate how the user attempted the privilege escalation
5. Check for vulnerabilities in permission management endpoints
6. Review access control implementation across the application
7. Report the incident to the security team and management
8. Consider legal action if the attempt was malicious

---

## Alert Notification Channels

### Email Notifications
- **Configuration:** `Alerting:Email` section in appsettings.json
- **Requirements:** SMTP server credentials
- **Format:** HTML emails with alert details, severity indicators, and action links
- **Recipients:** Configured per alert rule or default recipients

### Webhook Notifications
- **Configuration:** `Alerting:Webhook` section in appsettings.json
- **Requirements:** Webhook URL and optional authentication
- **Format:** JSON payload with alert details
- **Use Cases:** Integration with incident management systems (PagerDuty, Opsgenie, etc.)

### SMS Notifications
- **Configuration:** `Alerting:Sms` section in appsettings.json
- **Requirements:** Twilio account credentials
- **Format:** Concise text message with alert summary
- **Use Cases:** Critical alerts requiring immediate attention

---

## Alert Rate Limiting

To prevent alert flooding, the system implements rate limiting per alert rule:

- **Default:** Maximum 10 alerts per rule per hour
- **Configurable:** `RateLimitPerHour` parameter per alert rule
- **Mechanism:** Uses distributed cache (Redis) for tracking across multiple API instances
- **Behavior:** When rate limit is exceeded, alerts are suppressed but logged

**Example:**
```json
"SqlInjectionAttempt": {
  "RateLimitPerHour": 20,  // Allow up to 20 SQL injection alerts per hour
  ...
}
```

---

## Integration with SecurityMonitor Service

The alert configuration works in conjunction with the `SecurityMonitor` service:

1. **SecurityMonitor** detects security threats using pattern matching and behavior analysis
2. When a threat is detected, **SecurityMonitor** creates a `SecurityThreat` object
3. **SecurityMonitor** calls `IAlertManager.TriggerAlertAsync()` with the threat details
4. **AlertManager** evaluates alert rules and determines which alerts to trigger
5. **AlertManager** applies rate limiting and queues notifications
6. Background service processes the notification queue and sends alerts via configured channels

**Code Flow:**
```csharp
// SecurityMonitor detects threat
var threat = await _securityMonitor.DetectSqlInjectionAsync(input);

if (threat != null)
{
    // Trigger alert
    var alert = new Alert
    {
        AlertType = "SqlInjectionAttempt",
        Severity = threat.Severity.ToString(),
        Title = "SQL Injection Detected",
        Description = threat.Description,
        CorrelationId = threat.CorrelationId,
        // ... other properties
    };
    
    await _alertManager.TriggerAlertAsync(alert);
}
```

---

## Configuration Examples

### Development Environment
```json
{
  "Alerting": {
    "Enabled": false,  // Disable alerts in development
    "AlertRules": {
      "SqlInjectionAttempt": {
        "Enabled": false
      },
      "FailedLoginPattern": {
        "Enabled": false
      }
    }
  }
}
```

### Production Environment
```json
{
  "Alerting": {
    "Enabled": true,
    "Email": {
      "Enabled": true,
      "SmtpHost": "smtp.company.com",
      "SmtpPort": 587,
      "SmtpUsername": "alerts@company.com",
      "SmtpPassword": "***",
      "DefaultRecipients": [ "security@company.com", "admin@company.com" ]
    },
    "Webhook": {
      "Enabled": true,
      "DefaultUrl": "https://pagerduty.com/api/v1/incidents",
      "AuthHeaderValue": "***"
    },
    "Sms": {
      "Enabled": true,
      "TwilioAccountSid": "***",
      "TwilioAuthToken": "***",
      "TwilioFromPhoneNumber": "+1234567890",
      "DefaultRecipients": [ "+1234567891" ]
    },
    "AlertRules": {
      "SqlInjectionAttempt": {
        "Enabled": true,
        "Severity": "Critical",
        "Channels": [ "Email", "Webhook", "Sms" ],
        "AutoBlock": true
      },
      "FailedLoginPattern": {
        "Enabled": true,
        "Severity": "High",
        "Channels": [ "Email", "Webhook", "Sms" ],
        "AutoBlock": true,
        "FailedLoginThreshold": 5
      }
    }
  }
}
```

---

## Testing Alert Configuration

### 1. Test Email Delivery
```bash
# Use the monitoring endpoint to trigger a test alert
curl -X POST https://api.thinkonerp.com/api/monitoring/test-alert \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "alertType": "SqlInjectionAttempt",
    "testMode": true
  }'
```

### 2. Test Security Threat Detection
```bash
# Trigger SQL injection detection with a test payload
curl -X GET "https://api.thinkonerp.com/api/users?search='; DROP TABLE users--" \
  -H "Authorization: Bearer YOUR_TOKEN"

# Check logs for security threat detection and alert triggering
```

### 3. Test Failed Login Pattern
```bash
# Make multiple failed login attempts
for i in {1..6}; do
  curl -X POST https://api.thinkonerp.com/api/auth/login \
    -H "Content-Type: application/json" \
    -d '{"username": "test", "password": "wrong"}'
done

# Check for failed login pattern alert
```

### 4. Verify Alert Rate Limiting
```bash
# Trigger multiple alerts rapidly
for i in {1..15}; do
  curl -X GET "https://api.thinkonerp.com/api/users?search='; DROP TABLE users--" \
    -H "Authorization: Bearer YOUR_TOKEN"
done

# Verify that only RateLimitPerHour alerts are sent (default: 20 for SQL injection)
```

---

## Monitoring and Troubleshooting

### Check Alert History
```bash
# Get recent alerts
curl -X GET https://api.thinkonerp.com/api/alerts/history?pageSize=50 \
  -H "Authorization: Bearer YOUR_TOKEN"
```

### Check Alert Rules
```bash
# Get configured alert rules
curl -X GET https://api.thinkonerp.com/api/alerts/rules \
  -H "Authorization: Bearer YOUR_TOKEN"
```

### Check Security Threats
```bash
# Get active security threats
curl -X GET https://api.thinkonerp.com/api/monitoring/security/threats \
  -H "Authorization: Bearer YOUR_TOKEN"
```

### Common Issues

#### Alerts Not Being Sent
1. Check if alerting is enabled: `Alerting:Enabled = true`
2. Check if the specific alert rule is enabled
3. Check if notification channels are configured correctly
4. Check SMTP/Webhook/Twilio credentials
5. Review logs for alert processing errors
6. Check if rate limiting is suppressing alerts

#### False Positives
1. Review the alert threshold and adjust if needed
2. Check if the detection pattern is too broad
3. Consider whitelisting legitimate users/IPs
4. Adjust the severity level if appropriate
5. Disable the alert rule if it's not useful

#### Missed Threats
1. Review the detection patterns and thresholds
2. Check if the alert rule is enabled
3. Verify that SecurityMonitor is detecting the threats
4. Check if rate limiting is suppressing alerts
5. Review logs for detection errors

---

## Security Best Practices

1. **Enable All Critical Alerts in Production**
   - SqlInjectionAttempt
   - PrivilegeEscalationAttempt
   - FailedLoginPattern (with AutoBlock)

2. **Configure Multiple Notification Channels**
   - Use email for all alerts
   - Use webhook for integration with incident management
   - Use SMS for critical alerts requiring immediate attention

3. **Set Appropriate Thresholds**
   - Balance between false positives and missed threats
   - Adjust thresholds based on your environment and traffic patterns
   - Review and tune thresholds regularly

4. **Enable AutoBlock for Critical Threats**
   - SQL injection attempts
   - XSS attempts
   - Privilege escalation attempts

5. **Monitor Alert Volume**
   - Track alert frequency and patterns
   - Investigate sudden increases in alerts
   - Adjust rate limiting if needed

6. **Regular Review and Testing**
   - Test alert delivery monthly
   - Review alert history weekly
   - Update alert rules based on new threats
   - Conduct security drills to test response procedures

7. **Document Response Procedures**
   - Create runbooks for each alert type
   - Define escalation procedures
   - Assign responsibilities for alert response
   - Conduct post-incident reviews

---

## Related Documentation

- [APM Configuration Guide](APM_CONFIGURATION_GUIDE.md)
- [Operational Runbooks](OPERATIONAL_RUNBOOKS.md)
- [Audit Logging Alerts Configuration](AUDIT_LOGGING_ALERTS_CONFIGURATION.md)
- [Security Monitoring Configuration](../src/ThinkOnErp.Infrastructure/Configuration/appsettings.security.example.json)

---

## Support

For questions or issues with security threat alerts:
- Review the logs in `Logs/` directory
- Check the monitoring dashboard
- Contact the security team
- Refer to the operational runbooks for common issues
