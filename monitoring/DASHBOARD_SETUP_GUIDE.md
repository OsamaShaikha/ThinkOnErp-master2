# Grafana Dashboard Setup Guide

## Overview

This guide provides instructions for setting up and using Grafana dashboards to monitor the ThinkOnErp Full Traceability System. The dashboards provide real-time visibility into:

- **Audit System Health**: Queue depth, processing times, write latency, error rates
- **Performance Metrics**: API latency, slow requests, database query performance
- **Security Monitoring**: Failed logins, security threats, unauthorized access attempts
- **System Health**: CPU, memory, database connections, garbage collection

## Prerequisites

1. **Prometheus** - Metrics collection and storage
2. **Grafana** - Dashboard visualization
3. **ThinkOnErp API** - Running with OpenTelemetry metrics enabled

## Quick Start

### 1. Start Prometheus

Create `prometheus.yml` configuration:

```yaml
global:
  scrape_interval: 15s
  evaluation_interval: 15s

scrape_configs:
  - job_name: 'thinkonerp-api'
    static_configs:
      - targets: ['localhost:5000']
    metrics_path: '/metrics'
    scrape_interval: 10s
```

Start Prometheus using Docker:

```bash
docker run -d \
  --name prometheus \
  -p 9090:9090 \
  -v $(pwd)/prometheus.yml:/etc/prometheus/prometheus.yml \
  prom/prometheus
```

Verify Prometheus is scraping metrics:
- Open http://localhost:9090
- Navigate to Status > Targets
- Verify `thinkonerp-api` target is UP

### 2. Start Grafana

Start Grafana using Docker:

```bash
docker run -d \
  --name grafana \
  -p 3000:3000 \
  -v $(pwd)/monitoring/grafana/dashboards:/etc/grafana/provisioning/dashboards \
  grafana/grafana
```

Access Grafana:
- Open http://localhost:3000
- Default credentials: admin/admin
- Change password on first login

### 3. Configure Prometheus Data Source

1. In Grafana, navigate to **Configuration** > **Data Sources**
2. Click **Add data source**
3. Select **Prometheus**
4. Configure:
   - **Name**: Prometheus
   - **URL**: http://prometheus:9090 (if using Docker) or http://localhost:9090
   - **Access**: Server (default)
5. Click **Save & Test**

### 4. Import Dashboards

#### Option A: Automatic Provisioning (Recommended)

Create provisioning configuration:

```bash
mkdir -p monitoring/grafana/provisioning/dashboards
```

Create `monitoring/grafana/provisioning/dashboards/dashboards.yml`:

```yaml
apiVersion: 1

providers:
  - name: 'ThinkOnErp Dashboards'
    orgId: 1
    folder: 'ThinkOnErp'
    type: file
    disableDeletion: false
    updateIntervalSeconds: 10
    allowUiUpdates: true
    options:
      path: /etc/grafana/provisioning/dashboards
```

Restart Grafana with provisioning:

```bash
docker run -d \
  --name grafana \
  -p 3000:3000 \
  -v $(pwd)/monitoring/grafana/dashboards:/etc/grafana/provisioning/dashboards \
  -v $(pwd)/monitoring/grafana/provisioning:/etc/grafana/provisioning \
  grafana/grafana
```

#### Option B: Manual Import

1. In Grafana, click **+** > **Import**
2. Click **Upload JSON file**
3. Select one of the dashboard JSON files:
   - `audit-system-health.json`
   - `performance-metrics.json`
   - `security-monitoring.json`
   - `system-health.json`
4. Select **Prometheus** as the data source
5. Click **Import**

Repeat for each dashboard.

## Dashboard Descriptions

### 1. Audit System Health Dashboard

**Purpose**: Monitor the health and performance of the audit logging system.

**Key Panels**:
- **Audit Queue Depth**: Current number of events waiting to be written
  - Alert: Triggers when queue depth exceeds 5000 events
  - Green: < 3000, Yellow: 3000-5000, Red: > 5000
- **Audit Write Latency**: p95 and p99 write latency in milliseconds
  - Target: < 50ms for p95
- **Audit Batch Processing Time**: Time to process batches of audit events
- **Audit Events Rate**: Events processed per second
- **Audit Write Success Rate**: Percentage of successful writes
  - Target: > 99%
- **Circuit Breaker State**: Current state (Closed/Open/Half-Open)
  - Green: Closed (healthy)
  - Yellow: Half-Open (recovering)
  - Red: Open (failing)
- **Failed Audit Writes**: Count of failed write operations
- **Audit Events by Type**: Distribution of event types (DataChange, Authentication, etc.)
- **Database Connection Pool**: Active, idle, and max connections
- **Memory Usage**: Process memory and GC heap size

**When to Use**:
- Monitor audit system performance during high load
- Investigate audit logging failures
- Verify queue depth is within acceptable limits
- Check circuit breaker state during database issues

### 2. Performance Metrics Dashboard

**Purpose**: Monitor API performance and identify bottlenecks.

**Key Panels**:
- **API Request Rate**: Requests per second
- **API Request Latency**: p50, p95, p99 latency percentiles
  - Alert: Triggers when p99 exceeds 1000ms
  - Target: p99 < 500ms
- **Slow Requests**: Requests exceeding 1000ms
- **Slow Database Queries**: Queries exceeding 500ms
- **Request Latency by Endpoint**: p95 latency for each endpoint
- **Database Query Execution Time**: Average and p95 query times
- **CPU Usage**: Process CPU utilization
  - Alert: Triggers when CPU exceeds 80%
- **Garbage Collection Rate**: GC collections per second
- **Active HTTP Requests**: Current number of in-flight requests

**When to Use**:
- Identify slow API endpoints
- Investigate performance degradation
- Monitor system resource utilization
- Optimize database query performance

### 3. Security Monitoring Dashboard

**Purpose**: Monitor security threats and authentication events.

**Key Panels**:
- **Failed Login Attempts**: Rate of failed authentication attempts
  - Alert: Triggers when rate exceeds 10 per minute
- **Security Threats Detected**: Rate of detected security threats
- **Threats by Type**: Distribution of threat types (SQL injection, XSS, etc.)
- **Unauthorized Access Attempts**: Rate of unauthorized access attempts
- **SQL Injection Attempts**: Count in last hour
- **XSS Attempts**: Count in last hour
- **Active Security Threats**: Current number of active threats
- **Blocked IPs**: Number of blocked IP addresses
- **Authentication Events**: Successful vs. failed logins
- **Failed Logins by IP**: Top 10 IPs with most failed attempts
- **Anomalous Activity**: Rate of detected anomalies
- **Security Events by Severity**: Distribution by severity level

**When to Use**:
- Monitor for security attacks
- Investigate suspicious authentication patterns
- Identify blocked or malicious IPs
- Review security threat trends

### 4. System Health Dashboard

**Purpose**: Monitor overall system health and resource utilization.

**Key Panels**:
- **System Health Status**: Overall health indicator
  - Green: Healthy
  - Yellow: Degraded
  - Red: Unhealthy
- **API Availability**: Percentage of successful requests
  - Target: > 99%
- **Database Health**: Database connection status
- **Audit System Health**: Audit logging system status
- **CPU Usage**: Process CPU utilization
  - Alert: Triggers when CPU exceeds 80%
- **Memory Usage**: Process memory consumption
  - Alert: Triggers when memory exceeds 1GB
- **Database Connection Pool**: Active, idle, and max connections
  - Alert: Triggers when active connections near max
- **GC Heap Size**: Managed heap size
- **GC Collections**: Gen 0, Gen 1, Gen 2 collection rates
- **Error Rate**: 4xx and 5xx error rates
- **Thread Pool**: Active threads and queue length
- **Disk I/O**: Read and write throughput

**When to Use**:
- Monitor overall system health
- Investigate resource exhaustion
- Check database connectivity
- Review error trends

## Alert Configuration

### Configuring Alert Notifications

1. In Grafana, navigate to **Alerting** > **Notification channels**
2. Click **Add channel**
3. Configure notification channel:
   - **Name**: Email Alerts
   - **Type**: Email
   - **Addresses**: admin@example.com
4. Click **Save**

### Linking Alerts to Notification Channels

1. Open a dashboard with alerts (e.g., Audit System Health)
2. Click on a panel with an alert (e.g., Audit Queue Depth)
3. Click **Edit**
4. Navigate to **Alert** tab
5. Under **Notifications**, click **Add**
6. Select your notification channel
7. Click **Save**

### Pre-configured Alerts

The following alerts are pre-configured in the dashboards:

#### Audit System Health
- **High Audit Queue Depth**: Queue depth > 5000 events for 5 minutes
- **Audit Write Failures**: Failed writes detected

#### Performance Metrics
- **High API Latency**: p99 latency > 1000ms for 5 minutes
- **High CPU Usage**: CPU usage > 80% for 5 minutes

#### Security Monitoring
- **High Failed Login Rate**: Failed logins > 10 per minute

#### System Health
- **High CPU Usage**: CPU usage > 80% for 5 minutes
- **High Memory Usage**: Memory > 1GB for 5 minutes
- **Connection Pool Exhaustion**: Active connections > 90% of max

## Customization

### Modifying Thresholds

To adjust alert thresholds:

1. Open the dashboard
2. Click on the panel to edit
3. Navigate to **Alert** tab
4. Modify the **Conditions** section
5. Update threshold values
6. Click **Save**

### Adding Custom Panels

To add new panels:

1. Open the dashboard
2. Click **Add panel** at the top
3. Select **Add new panel**
4. Configure:
   - **Query**: Prometheus query expression
   - **Visualization**: Graph, stat, table, etc.
   - **Thresholds**: Color-coded thresholds
5. Click **Apply**

### Example Custom Queries

**Audit events by company**:
```promql
sum by (company_id) (rate(audit_events_total[5m]))
```

**Average request latency by endpoint**:
```promql
rate(http_server_request_duration_seconds_sum[5m]) / rate(http_server_request_duration_seconds_count[5m])
```

**Database connection pool utilization percentage**:
```promql
(db_connection_pool_active / db_connection_pool_max) * 100
```

## Troubleshooting

### No Data in Dashboards

**Problem**: Dashboards show "No data"

**Solutions**:
1. Verify Prometheus is scraping metrics:
   - Open http://localhost:9090
   - Navigate to Status > Targets
   - Check if `thinkonerp-api` is UP
2. Verify ThinkOnErp API is exposing metrics:
   - Open http://localhost:5000/metrics
   - Verify metrics are returned
3. Check Prometheus data source configuration in Grafana
4. Verify time range in dashboard (top right)

### Metrics Not Updating

**Problem**: Metrics are stale or not updating

**Solutions**:
1. Check Prometheus scrape interval (default: 15s)
2. Verify ThinkOnErp API is running
3. Check Grafana refresh interval (top right)
4. Restart Prometheus if needed

### Alerts Not Firing

**Problem**: Alerts are not triggering

**Solutions**:
1. Verify alert conditions are met
2. Check alert evaluation interval (default: 1m)
3. Verify notification channels are configured
4. Check Grafana logs for alert errors

### High Memory Usage in Grafana

**Problem**: Grafana consuming too much memory

**Solutions**:
1. Reduce dashboard refresh rate
2. Limit time range for queries
3. Reduce number of panels per dashboard
4. Increase Grafana memory limits

## Best Practices

### Dashboard Organization

1. **Create folders** for different teams:
   - Operations: System health, performance
   - Security: Security monitoring, threats
   - Development: API performance, debugging

2. **Use tags** for easy discovery:
   - `audit`, `performance`, `security`, `health`

3. **Set appropriate refresh rates**:
   - Real-time monitoring: 10-30s
   - Historical analysis: 1-5m

### Query Optimization

1. **Use appropriate time ranges**:
   - Real-time: Last 15 minutes
   - Troubleshooting: Last 1-6 hours
   - Analysis: Last 24 hours - 7 days

2. **Limit query resolution**:
   - Use `rate()` with appropriate intervals (5m for most cases)
   - Avoid querying raw metrics over long periods

3. **Use recording rules** for expensive queries:
   - Pre-calculate complex aggregations in Prometheus

### Alert Management

1. **Set meaningful thresholds**:
   - Based on baseline performance
   - Adjusted for business requirements

2. **Avoid alert fatigue**:
   - Set appropriate evaluation intervals
   - Use alert grouping
   - Implement alert suppression during maintenance

3. **Document alert responses**:
   - Create runbooks for each alert
   - Include troubleshooting steps
   - Define escalation procedures

## Maintenance

### Regular Tasks

1. **Review dashboard performance** (weekly):
   - Check query execution times
   - Optimize slow queries
   - Remove unused panels

2. **Update alert thresholds** (monthly):
   - Review alert history
   - Adjust based on system changes
   - Remove false positive alerts

3. **Clean up old data** (quarterly):
   - Configure Prometheus retention
   - Archive historical data if needed

### Backup and Recovery

**Backup Grafana dashboards**:
```bash
# Export all dashboards
curl -H "Authorization: Bearer YOUR_API_KEY" \
  http://localhost:3000/api/search?type=dash-db | \
  jq -r '.[] | .uid' | \
  xargs -I {} curl -H "Authorization: Bearer YOUR_API_KEY" \
    http://localhost:3000/api/dashboards/uid/{} > backup-{}.json
```

**Restore dashboards**:
```bash
# Import dashboard
curl -X POST -H "Content-Type: application/json" \
  -H "Authorization: Bearer YOUR_API_KEY" \
  -d @backup-dashboard.json \
  http://localhost:3000/api/dashboards/db
```

## Additional Resources

- [Grafana Documentation](https://grafana.com/docs/)
- [Prometheus Query Language](https://prometheus.io/docs/prometheus/latest/querying/basics/)
- [OpenTelemetry Metrics](https://opentelemetry.io/docs/concepts/signals/metrics/)
- [ThinkOnErp APM Configuration Guide](../docs/APM_CONFIGURATION_GUIDE.md)

## Support

For dashboard setup support:
- Review the APM Configuration Guide
- Check Grafana logs: `docker logs grafana`
- Check Prometheus logs: `docker logs prometheus`
- Verify metrics endpoint: http://localhost:5000/metrics

## Next Steps

After setting up dashboards:
1. Configure alert notifications (Task 24.3-24.6)
2. Create operational runbooks (Task 24.7)
3. Document troubleshooting procedures (Task 24.8)
4. Train operations team on dashboard usage
5. Establish monitoring SLAs and response procedures
