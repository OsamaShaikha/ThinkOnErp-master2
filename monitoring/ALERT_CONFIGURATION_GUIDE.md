# Alert Configuration Guide

## Overview

This guide provides comprehensive documentation for the Prometheus alert rules configured for the ThinkOnErp Full Traceability System. The alert system monitors queue depth, processing delays, database health, audit failures, and security threats.

## Implementation Status

✅ **COMPLETED** - Task 24.3: Configure alerts for queue depth and processing delays
✅ **COMPLETED** - Task 24.4: Configure alerts for database connection pool exhaustion
✅ **COMPLETED** - Task 24.5: Configure alerts for audit logging failures
✅ **COMPLETED** - Task 24.6: Configure alerts for security threats and anomalies

## Alert Rule Files

The alert rules are organized into four files:

1. **audit-system-alerts.yml** - Queue depth and processing delay alerts
2. **database-alerts.yml** - Database connection pool and health alerts
3. **audit-failures-alerts.yml** - Audit write failures and system health alerts
4. **security-alerts.yml** - Security threats and authentication alerts

## Alert Categories

### 1. Queue Depth Alerts

#### AuditQueueDepthCritical
- **Severity**: Critical
- **Threshold**: Queue depth > 8000 events
- **Duration**: 2 minutes
- **Impact**: Audit events may be dropped if queue reaches maximum capacity (10000)
- **Action**: Check audit system health, verify database connectivity, review batch processing

#### AuditQueueDepthHigh
- **Severity**: Warning
- **Threshold**: Queue depth > 5000 events
- **Duration**: 5 minutes
- **Impact**: System approaching capacity limits
- **Action**: Monitor trend, check write latency, review traffic patterns

#### AuditQueueDepthGrowing
- **Severity**: Warning
- **Threshold**: Queue growing > 100 events/sec
- **Duration**: 3 minutes
- **Impact**: Queue may reach critical levels
- **Action**: Investigate cause, check for traffic spikes, verify batch processing

#### AuditQueueNearCapacity
- **Severity**: Critical
- **Threshold**: Queue depth > 9000 events
- **Duration**: 1 minute
- **Impact**: Events will be dropped when queue reaches 10000
- **Action**: IMMEDIATE - Stop non-critical events, scale processing, check database

### 2. Processing Delay Alerts

#### AuditBatchProcessingDelayCritical
- **Severity**: Critical
- **Threshold**: p95 batch processing time > 5000ms
- **Duration**: 3 minutes
- **Impact**: Audit events significantly delayed, queue will grow
- **Action**: Check database performance, review slow queries, verify indexes

#### AuditBatchProcessingDelayHigh
- **Severity**: Warning
- **Threshold**: p95 batch processing time > 2000ms
- **Duration**: 5 minutes
- **Impact**: Processing slower than expected
- **Action**: Monitor database performance, check connection pool

#### AuditWriteLatencyCritical
- **Severity**: Critical
- **Threshold**: p95 write latency > 200ms
- **Duration**: 3 minutes
- **Impact**: Throughput severely degraded
- **Action**: Check database health, review connection pool, check for locks

#### AuditWriteLatencyHigh
- **Severity**: Warning
- **Threshold**: p95 write latency > 100ms
- **Duration**: 5 minutes
- **Impact**: May not meet performance SLA
- **Action**: Monitor database performance, check for slow queries

#### AuditProcessingStalled
- **Severity**: Critical
- **Threshold**: No events processed for 5 minutes with queue > 100
- **Duration**: 2 minutes
- **Impact**: Audit trail incomplete, queue may reach capacity
- **Action**: IMMEDIATE - Check service status, verify database access, review logs

#### AuditProcessingRateLow
- **Severity**: Warning
- **Threshold**: Processing < 10 events/sec with queue > 1000
- **Duration**: 5 minutes
- **Impact**: Processing may be stalled
- **Action**: Check service health, verify database connectivity, review circuit breaker

### 3. Database Connection Pool Alerts

#### DatabaseConnectionPoolExhausted
- **Severity**: Critical
- **Threshold**: Active connections > 95% of max
- **Duration**: 2 minutes
- **Impact**: API requests will fail or timeout
- **Action**: IMMEDIATE - Check for connection leaks, review long-running queries

#### DatabaseConnectionPoolHighUtilization
- **Severity**: Warning
- **Threshold**: Active connections > 80% of max
- **Duration**: 5 minutes
- **Impact**: Approaching capacity limits
- **Action**: Monitor trend, review active connections, check slow queries

#### DatabaseConnectionPoolNoIdleConnections
- **Severity**: Warning
- **Threshold**: Idle connections = 0 with active > 10
- **Duration**: 3 minutes
- **Impact**: New requests must wait for connections
- **Action**: Check database load, review active queries, look for long transactions

#### DatabaseConnectionTimeouts
- **Severity**: Critical
- **Threshold**: Connection timeouts > 1/sec
- **Duration**: 2 minutes
- **Impact**: API requests failing
- **Action**: IMMEDIATE - Check pool exhaustion, verify database health

#### PossibleDatabaseConnectionLeak
- **Severity**: Warning
- **Threshold**: High active connections with low request rate
- **Duration**: 10 minutes
- **Impact**: Pool may exhaust if leak continues
- **Action**: Review recent code changes, check for unclosed connections

### 4. Audit Failure Alerts

#### AuditWriteFailuresCritical
- **Severity**: Critical
- **Threshold**: Write failures > 10/sec
- **Duration**: 2 minutes
- **Impact**: Audit trail compromised
- **Action**: IMMEDIATE - Check database connectivity, review error logs

#### AuditWriteSuccessRateLow
- **Severity**: Critical
- **Threshold**: Success rate < 95%
- **Duration**: 5 minutes
- **Impact**: Audit trail incomplete, compliance at risk
- **Action**: IMMEDIATE - Investigate failures, check database health

#### AuditCircuitBreakerOpen
- **Severity**: Critical
- **Threshold**: Circuit breaker state = OPEN
- **Duration**: 1 minute
- **Impact**: Audit writes rejected, trail incomplete
- **Action**: IMMEDIATE - Check database connectivity, fix underlying issue

#### AuditSystemUnhealthy
- **Severity**: Critical
- **Threshold**: Health status = 0 (unhealthy)
- **Duration**: 2 minutes
- **Impact**: Audit system not functioning correctly
- **Action**: IMMEDIATE - Check service status, review logs, verify database

#### AuditDataIntegrityFailures
- **Severity**: Critical
- **Threshold**: Integrity check failures > 0
- **Duration**: 1 minute
- **Impact**: Data tampering or corruption suspected
- **Action**: IMMEDIATE - Alert security team, investigate tampering

### 5. Security Threat Alerts

#### HighFailedLoginRate
- **Severity**: Critical
- **Threshold**: Failed logins > 10/sec
- **Duration**: 2 minutes
- **Impact**: Possible brute force attack
- **Action**: IMMEDIATE - Review sources, consider IP blocking, alert security

#### SQLInjectionAttemptsDetected
- **Severity**: Critical
- **Threshold**: SQL injection attempts > 1/sec
- **Duration**: 2 minutes
- **Impact**: Database security at risk
- **Action**: IMMEDIATE - Alert security, block attacking IPs, verify parameterized queries

#### SecurityThreatsDetectedHigh
- **Severity**: Critical
- **Threshold**: Threats detected > 5/sec
- **Duration**: 3 minutes
- **Impact**: System under active attack
- **Action**: IMMEDIATE - Alert security team, review threat types, block malicious IPs

#### ActiveSecurityThreatsHigh
- **Severity**: Critical
- **Threshold**: Active threats > 10
- **Duration**: 5 minutes
- **Impact**: Multiple unresolved threats
- **Action**: IMMEDIATE - Review all threats, prioritize, assign to security team

#### MultipleAccountsCompromised
- **Severity**: Critical
- **Threshold**: Account compromises > 0.1/sec
- **Duration**: 2 minutes
- **Impact**: Data breach risk high
- **Action**: IMMEDIATE - Force password resets, revoke sessions, notify users

## Alert Severity Levels

### Critical
- **Response Time**: Immediate (< 5 minutes)
- **Escalation**: On-call engineer + security team
- **Impact**: System functionality or security severely compromised
- **Examples**: Queue near capacity, circuit breaker open, SQL injection detected

### Warning
- **Response Time**: Within 30 minutes
- **Escalation**: On-call engineer
- **Impact**: System degraded but functional
- **Examples**: High queue depth, elevated latency, high connection pool usage

### Info
- **Response Time**: Next business day
- **Escalation**: Team notification
- **Impact**: Informational, no immediate action required
- **Examples**: Batch size suboptimal, configuration recommendations

## Alert Notification Channels

### Email Notifications
Configure email notifications in Grafana:
1. Navigate to **Alerting** > **Notification channels**
2. Click **Add channel**
3. Select **Email**
4. Configure:
   - Name: "Operations Team Email"
   - Email addresses: ops-team@thinkonerp.com
   - Send on all alerts: Yes

### Slack Notifications
Configure Slack notifications:
1. Create Slack webhook URL
2. In Grafana, add notification channel
3. Select **Slack**
4. Configure:
   - Name: "Operations Slack"
   - Webhook URL: [your-webhook-url]
   - Channel: #ops-alerts
   - Mention: @channel for critical, @here for warnings

### PagerDuty Integration
For critical alerts:
1. Create PagerDuty integration key
2. In Grafana, add notification channel
3. Select **PagerDuty**
4. Configure:
   - Name: "PagerDuty Critical"
   - Integration Key: [your-key]
   - Severity: Critical only

### Webhook Notifications
For custom integrations:
1. In Grafana, add notification channel
2. Select **Webhook**
3. Configure:
   - URL: https://your-webhook-endpoint.com/alerts
   - HTTP Method: POST
   - Include image: Optional

## Alert Configuration in Prometheus

### Prometheus Configuration
The alert rules are loaded in `prometheus.yml`:

```yaml
rule_files:
  - "alerts/audit-system-alerts.yml"
  - "alerts/database-alerts.yml"
  - "alerts/audit-failures-alerts.yml"
  - "alerts/security-alerts.yml"
```

### AlertManager Configuration (Optional)
For advanced alert routing and grouping, configure AlertManager:

```yaml
# alertmanager.yml
global:
  resolve_timeout: 5m

route:
  group_by: ['alertname', 'component']
  group_wait: 10s
  group_interval: 10s
  repeat_interval: 12h
  receiver: 'default'
  routes:
    - match:
        severity: critical
      receiver: 'pagerduty'
      continue: true
    - match:
        severity: warning
      receiver: 'slack'
    - match:
        category: security
      receiver: 'security-team'

receivers:
  - name: 'default'
    email_configs:
      - to: 'ops-team@thinkonerp.com'
  
  - name: 'pagerduty'
    pagerduty_configs:
      - service_key: 'YOUR_PAGERDUTY_KEY'
  
  - name: 'slack'
    slack_configs:
      - api_url: 'YOUR_SLACK_WEBHOOK'
        channel: '#ops-alerts'
  
  - name: 'security-team'
    email_configs:
      - to: 'security@thinkonerp.com'
```

## Testing Alerts

### Manual Alert Testing

#### Test Queue Depth Alert
```bash
# Simulate high queue depth by generating many audit events
curl -X POST http://localhost:5000/api/test/generate-audit-events \
  -H "Content-Type: application/json" \
  -d '{"count": 10000}'
```

#### Test Processing Delay Alert
```bash
# Simulate slow database by introducing artificial delay
curl -X POST http://localhost:5000/api/test/simulate-slow-database \
  -H "Content-Type: application/json" \
  -d '{"delayMs": 5000}'
```

#### Test Failed Login Alert
```bash
# Generate failed login attempts
for i in {1..100}; do
  curl -X POST http://localhost:5000/api/auth/login \
    -H "Content-Type: application/json" \
    -d '{"username": "invalid", "password": "wrong"}'
done
```

### Verify Alert Firing

1. **Check Prometheus Alerts**:
   - Open http://localhost:9090/alerts
   - Verify alert is in "Firing" state
   - Check alert labels and annotations

2. **Check Grafana Alerts**:
   - Open Grafana dashboard
   - Look for red alert indicators on panels
   - Check Alerting > Alert Rules for status

3. **Verify Notifications**:
   - Check email inbox
   - Check Slack channel
   - Verify PagerDuty incident created

## Alert Tuning

### Adjusting Thresholds

If alerts are too sensitive or not sensitive enough, adjust thresholds:

```yaml
# Example: Increase queue depth threshold
- alert: AuditQueueDepthHigh
  expr: audit_queue_depth > 7000  # Changed from 5000
  for: 5m
```

### Adjusting Duration

If alerts fire too quickly or too slowly:

```yaml
# Example: Increase duration before firing
- alert: AuditQueueDepthHigh
  expr: audit_queue_depth > 5000
  for: 10m  # Changed from 5m
```

### Silencing Alerts

To temporarily silence alerts during maintenance:

1. In Grafana, go to **Alerting** > **Silences**
2. Click **New Silence**
3. Configure:
   - Matchers: alertname=AuditQueueDepthHigh
   - Duration: 2 hours
   - Comment: "Planned maintenance"

## Alert Response Procedures

### Critical Alert Response

1. **Acknowledge Alert** (< 2 minutes)
   - Acknowledge in PagerDuty/Grafana
   - Notify team in Slack

2. **Initial Assessment** (< 5 minutes)
   - Check alert details and metrics
   - Review recent changes/deployments
   - Check system health dashboard

3. **Immediate Mitigation** (< 15 minutes)
   - Follow runbook procedures
   - Implement temporary fixes
   - Escalate if needed

4. **Resolution** (< 1 hour)
   - Implement permanent fix
   - Verify metrics return to normal
   - Document incident

5. **Post-Incident** (< 24 hours)
   - Write incident report
   - Update runbooks
   - Implement preventive measures

### Warning Alert Response

1. **Review Alert** (< 30 minutes)
   - Check alert details
   - Review metric trends
   - Assess urgency

2. **Investigation** (< 2 hours)
   - Identify root cause
   - Check for patterns
   - Review logs

3. **Action Plan** (< 4 hours)
   - Create action plan
   - Schedule fix
   - Monitor for escalation

## Alert Metrics

### Alert Effectiveness Metrics

Track these metrics to measure alert effectiveness:

- **Mean Time to Detect (MTTD)**: Time from issue start to alert firing
- **Mean Time to Acknowledge (MTTA)**: Time from alert to acknowledgment
- **Mean Time to Resolve (MTTR)**: Time from alert to resolution
- **False Positive Rate**: Percentage of alerts that were not actionable
- **Alert Fatigue Score**: Number of alerts per day per engineer

### Target Metrics

- MTTD: < 2 minutes
- MTTA: < 5 minutes (critical), < 30 minutes (warning)
- MTTR: < 1 hour (critical), < 4 hours (warning)
- False Positive Rate: < 5%
- Alert Fatigue: < 10 alerts per day per engineer

## Troubleshooting

### Alerts Not Firing

1. **Check Prometheus is scraping metrics**:
   ```bash
   curl http://localhost:9090/api/v1/targets
   ```

2. **Verify alert rules are loaded**:
   ```bash
   curl http://localhost:9090/api/v1/rules
   ```

3. **Check for syntax errors**:
   ```bash
   promtool check rules alerts/*.yml
   ```

4. **Verify metrics exist**:
   ```bash
   curl 'http://localhost:9090/api/v1/query?query=audit_queue_depth'
   ```

### Alerts Firing Incorrectly

1. **Check metric values**:
   - Open Prometheus UI
   - Query the metric
   - Verify values match expectations

2. **Review alert expression**:
   - Check for typos
   - Verify threshold is appropriate
   - Test expression in Prometheus UI

3. **Check duration**:
   - Verify `for` duration is appropriate
   - Consider increasing if too sensitive

### Notifications Not Received

1. **Check notification channel configuration**:
   - Verify webhook URLs
   - Check email addresses
   - Test notification channel

2. **Check AlertManager logs**:
   ```bash
   docker logs thinkonerp-alertmanager
   ```

3. **Verify routing rules**:
   - Check AlertManager configuration
   - Verify matchers are correct

## Best Practices

### Alert Design

1. **Make alerts actionable**: Every alert should have clear action steps
2. **Avoid alert fatigue**: Don't alert on every minor issue
3. **Use appropriate severity**: Reserve critical for truly critical issues
4. **Include context**: Provide relevant information in annotations
5. **Link to runbooks**: Include runbook URLs for quick reference

### Alert Maintenance

1. **Review alerts monthly**: Check for false positives and missed issues
2. **Update thresholds**: Adjust based on system behavior
3. **Document changes**: Keep alert documentation up to date
4. **Test regularly**: Verify alerts fire correctly
5. **Gather feedback**: Ask on-call engineers for improvement suggestions

### Alert Organization

1. **Group related alerts**: Organize by component or category
2. **Use consistent naming**: Follow naming conventions
3. **Maintain separate files**: Keep alert rules organized
4. **Version control**: Track changes in git
5. **Document decisions**: Explain why thresholds were chosen

## References

- [Prometheus Alerting Documentation](https://prometheus.io/docs/alerting/latest/overview/)
- [AlertManager Configuration](https://prometheus.io/docs/alerting/latest/configuration/)
- [Grafana Alerting](https://grafana.com/docs/grafana/latest/alerting/)
- [Alert Design Best Practices](https://docs.google.com/document/d/199PqyG3UsyXlwieHaqbGiWVa8eMWi8zzAn0YfcApr8Q/edit)

## Conclusion

The alert configuration provides comprehensive monitoring of:
- ✅ Queue depth and processing delays
- ✅ Database connection pool exhaustion
- ✅ Audit logging failures
- ✅ Security threats and anomalies

All alerts include:
- Clear severity levels
- Actionable descriptions
- Impact assessments
- Response procedures
- Runbook links

The alert system is production-ready and will help operations teams:
- Detect issues proactively
- Respond quickly to incidents
- Maintain system reliability
- Ensure audit trail integrity
- Protect against security threats
