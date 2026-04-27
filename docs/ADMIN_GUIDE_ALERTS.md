# Administrator Guide: Alert Management

## Overview

The ThinkOnErp alert system monitors security threats, performance degradation, and system health. Alerts are processed by the `AlertProcessingBackgroundService` and delivered via email, SMS, or webhook.

---

## View Active Alerts

```http
GET /api/alerts?status=active&page=1&pageSize=20
Authorization: Bearer {admin-token}
```

## View Alert History

```http
GET /api/alerts/history?startDate=2024-01-01&endDate=2024-01-31&page=1&pageSize=50
```

---

## Create an Alert Rule

```http
POST /api/alerts/rules
Authorization: Bearer {admin-token}
Content-Type: application/json

{
  "name": "High Error Rate",
  "description": "Alert when error rate exceeds 5% of requests",
  "category": "Performance",
  "severity": "Warning",
  "condition": {
    "metric": "error_rate_percent",
    "operator": "greater_than",
    "threshold": 5.0,
    "windowMinutes": 5
  },
  "notification": {
    "channels": ["email", "webhook"],
    "recipients": ["ops-team@example.com"],
    "cooldownMinutes": 15
  },
  "enabled": true
}
```

### Common Alert Rules

| Alert | Metric | Threshold | Severity |
|---|---|---|---|
| High Error Rate | `error_rate_percent` | >5% | Warning |
| Slow Responses | `p99_latency_ms` | >5000ms | Warning |
| Failed Logins | `failed_login_count` | >10 in 5min | Critical |
| Connection Pool Exhaustion | `pool_utilization_percent` | >80% | Warning |
| Memory Pressure | `memory_usage_mb` | >1024MB | Warning |
| Audit Queue Depth | `audit_queue_depth` | >5000 | Warning |
| Security Threat | `security_threat_detected` | any | Critical |
| Database Failure | `circuit_breaker_open` | true | Critical |

---

## Acknowledge an Alert

```http
PUT /api/alerts/{alertId}/acknowledge
Authorization: Bearer {admin-token}
Content-Type: application/json

{
  "acknowledgedBy": "admin-username",
  "notes": "Investigating the issue"
}
```

## Resolve an Alert

```http
PUT /api/alerts/{alertId}/resolve
Authorization: Bearer {admin-token}
Content-Type: application/json

{
  "resolvedBy": "admin-username",
  "resolutionNotes": "Increased connection pool size to 200",
  "rootCause": "Unexpected traffic spike from batch import"
}
```

---

## Manage Alert Rules

### List All Rules
```http
GET /api/alerts/rules
```

### Update a Rule
```http
PUT /api/alerts/rules/{ruleId}
```

### Disable a Rule
```http
PUT /api/alerts/rules/{ruleId}/disable
```

### Delete a Rule
```http
DELETE /api/alerts/rules/{ruleId}
```

---

## Notification Channels

### Email
- Uses SMTP configuration from `Alerting:Email`
- Supports HTML templates
- Configure recipients per rule

### SMS (Twilio)
- Requires Twilio account credentials in `Alerting:Sms`
- Best for critical alerts only (cost consideration)

### Webhook
- HTTP POST to configured URL
- Payload includes alert details, severity, and timestamp
- Automatic retry with exponential backoff (max 3 retries)
- Configure per-rule URLs or use default

---

## Rate Limiting

Alerts are rate-limited to prevent notification flooding:
- Default: 10 alerts per minute per rule
- Configure `cooldownMinutes` per rule to suppress repeated alerts
- Global limit configurable via `Alerting:RateLimitPerMinute`

---

## Monitoring the Alert System

```http
GET /api/monitoring/alerts/health
```

Returns: processing status, queue depth, last processing time, channel health.
