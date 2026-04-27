# Monitoring Dashboards for Full Traceability System

## Overview

This directory contains Grafana dashboard JSON files for monitoring the ThinkOnErp Full Traceability System. These dashboards provide comprehensive visibility into:

- **Audit System Health**: Queue depth, write latency, batch processing, circuit breaker state
- **Request Tracing & Performance**: API latency, throughput, error rates, slow requests
- **Security Monitoring**: Failed logins, security threats, blocked IPs, authentication events
- **System Health & Resources**: CPU, memory, GC, database connections, thread pool

## Dashboard Files

### 1. audit-system-health.json
**Purpose**: Monitor the health and performance of the audit logging system

**Key Metrics**:
- Audit queue depth (with alert at 5000 events)
- Audit write latency (p95, p99)
- Audit events processed rate
- Audit write success rate
- Circuit breaker state
- Batch processing time
- Database connection pool utilization

**Alerts Configured**:
- High audit queue depth (>5000 events)
- High audit write latency (p95 >50ms)
- High connection pool utilization (>90%)

### 2. request-tracing-performance.json
**Purpose**: Monitor API performance and request tracing metrics

**Key Metrics**:
- API request rate by endpoint
- API response time percentiles (p50, p95, p99)
- Error rate by endpoint
- Success rate percentage
- Active requests count
- Slow requests (>1s)
- Database query time
- Top 10 slowest endpoints

**Alerts Configured**:
- High API latency (p99 >1000ms)

### 3. security-monitoring.json
**Purpose**: Monitor security events and detect threats

**Key Metrics**:
- Failed login attempts rate
- Security threats detected by type
- Active security threats count
- Blocked IPs count
- Unauthorized access attempts
- SQL injection attempts
- Failed logins by IP address (top 10)
- Authentication events timeline
- Threat types distribution

**Alerts Configured**:
- High failed login rate (>10 per minute)
- Security threat detected

### 4. system-health.json
**Purpose**: Monitor system resources and overall health

**Key Metrics**:
- CPU usage percentage
- Memory usage and GC heap size
- Garbage collection rate (Gen 0, 1, 2)
- Thread pool threads and queue length
- Database connection pool status
- Database query rate
- System health status (UP/DOWN)
- Uptime
- Disk I/O
- Network I/O
- Exception rate
- Health check status

**Alerts Configured**:
- High CPU usage (>80%)
- High memory usage (>2GB)

## Prerequisites

Before importing these dashboards, ensure you have:

1. **Prometheus** running and scraping metrics from ThinkOnErp API
   - Metrics endpoint: `http://localhost:5000/metrics`
   - Scrape interval: 15s (recommended)

2. **Grafana** installed and running
   - Version: 9.0 or higher recommended
   - Access: http://localhost:3000 (default)

3. **Prometheus Data Source** configured in Grafana
   - Name: "Prometheus" (or update dashboard queries)
   - URL: http://prometheus:9090 (or your Prometheus URL)

## Quick Start

### Option 1: Manual Import (Recommended for First-Time Setup)

1. **Access Grafana**:
   ```
   http://localhost:3000
   ```
   Default credentials: admin/admin

2. **Import Dashboard**:
   - Click "+" in the left sidebar
   - Select "Import"
   - Click "Upload JSON file"
   - Select one of the dashboard JSON files
   - Choose your Prometheus data source
   - Click "Import"

3. **Repeat for All Dashboards**:
   - Import all 4 dashboard files
   - Organize them in folders if desired

### Option 2: Automated Provisioning (Recommended for Production)

1. **Create Grafana Provisioning Directory**:
   ```bash
   mkdir -p /etc/grafana/provisioning/dashboards
   mkdir -p /etc/grafana/provisioning/datasources
   ```

2. **Copy Dashboard Files**:
   ```bash
   cp monitoring/dashboards/*.json /etc/grafana/provisioning/dashboards/
   ```

3. **Create Dashboard Provisioning Config**:
   Create `/etc/grafana/provisioning/dashboards/dashboards.yml`:
   ```yaml
   apiVersion: 1

   providers:
     - name: 'ThinkOnErp Traceability'
       orgId: 1
       folder: 'Traceability System'
       type: file
       disableDeletion: false
       updateIntervalSeconds: 10
       allowUiUpdates: true
       options:
         path: /etc/grafana/provisioning/dashboards
   ```

4. **Create Data Source Provisioning Config**:
   Create `/etc/grafana/provisioning/datasources/prometheus.yml`:
   ```yaml
   apiVersion: 1

   datasources:
     - name: Prometheus
       type: prometheus
       access: proxy
       url: http://prometheus:9090
       isDefault: true
       editable: true
   ```

5. **Restart Grafana**:
   ```bash
   docker-compose restart grafana
   # or
   systemctl restart grafana-server
   ```

### Option 3: Docker Compose Setup

Add to your `docker-compose.yml`:

```yaml
version: '3.8'

services:
  prometheus:
    image: prom/prometheus:latest
    container_name: prometheus
    ports:
      - "9090:9090"
    volumes:
      - ./monitoring/prometheus.yml:/etc/prometheus/prometheus.yml
      - prometheus-data:/prometheus
    command:
      - '--config.file=/etc/prometheus/prometheus.yml'
      - '--storage.tsdb.path=/prometheus'
      - '--web.console.libraries=/usr/share/prometheus/console_libraries'
      - '--web.console.templates=/usr/share/prometheus/consoles'
    networks:
      - monitoring

  grafana:
    image: grafana/grafana:latest
    container_name: grafana
    ports:
      - "3000:3000"
    volumes:
      - ./monitoring/dashboards:/etc/grafana/provisioning/dashboards
      - ./monitoring/provisioning:/etc/grafana/provisioning
      - grafana-data:/var/lib/grafana
    environment:
      - GF_SECURITY_ADMIN_PASSWORD=admin
      - GF_USERS_ALLOW_SIGN_UP=false
    networks:
      - monitoring
    depends_on:
      - prometheus

volumes:
  prometheus-data:
  grafana-data:

networks:
  monitoring:
    driver: bridge
```

Create `monitoring/prometheus.yml`:

```yaml
global:
  scrape_interval: 15s
  evaluation_interval: 15s

scrape_configs:
  - job_name: 'thinkonerp-api'
    static_configs:
      - targets: ['thinkonerp-api:5000']
    metrics_path: '/metrics'
```

Start the monitoring stack:

```bash
docker-compose up -d prometheus grafana
```

## Dashboard Configuration

### Customizing Thresholds

Each dashboard includes pre-configured alert thresholds. To customize:

1. Open the dashboard in Grafana
2. Click the panel title → Edit
3. Go to the "Alert" tab
4. Modify threshold values as needed
5. Save the dashboard

**Recommended Thresholds**:

| Metric | Warning | Critical |
|--------|---------|----------|
| Audit Queue Depth | 3000 | 5000 |
| Audit Write Latency (p95) | 30ms | 50ms |
| API Latency (p99) | 500ms | 1000ms |
| Connection Pool Utilization | 70% | 90% |
| CPU Usage | 60% | 80% |
| Memory Usage | 1.5GB | 2GB |
| Failed Login Rate | 5/min | 10/min |

### Configuring Notifications

To receive alerts via email, Slack, or other channels:

1. **Configure Notification Channel**:
   - Go to Alerting → Notification channels
   - Click "New channel"
   - Select type (Email, Slack, Webhook, etc.)
   - Configure settings
   - Test and save

2. **Link to Dashboard Alerts**:
   - Edit dashboard panel with alert
   - Go to Alert tab
   - Under "Notifications", select your channel
   - Save dashboard

### Adjusting Refresh Rate

Default refresh rate is 30 seconds (1 minute for security dashboard). To change:

1. Open dashboard
2. Click the refresh dropdown (top right)
3. Select desired interval
4. Save dashboard

## Metrics Reference

### Audit System Metrics

| Metric Name | Type | Description |
|-------------|------|-------------|
| `audit_queue_depth` | Gauge | Current number of events in audit queue |
| `audit_write_latency` | Histogram | Time taken to write audit batch to database |
| `audit_events_total` | Counter | Total number of audit events processed |
| `audit_writes_total` | Counter | Total number of audit write operations |
| `audit_writes_success_total` | Counter | Total number of successful audit writes |
| `circuit_breaker_state` | Gauge | Circuit breaker state (0=CLOSED, 1=HALF_OPEN, 2=OPEN) |
| `audit_batch_processing_time` | Histogram | Time taken to process audit batch |

### Performance Metrics

| Metric Name | Type | Description |
|-------------|------|-------------|
| `http_server_requests_total` | Counter | Total HTTP requests by method, endpoint, status |
| `http_server_request_duration_seconds` | Histogram | HTTP request duration in seconds |
| `http_server_active_requests` | Gauge | Number of active HTTP requests |
| `slow_requests_total` | Counter | Number of requests exceeding 1 second |
| `db_query_duration_seconds` | Histogram | Database query execution time |

### Security Metrics

| Metric Name | Type | Description |
|-------------|------|-------------|
| `failed_login_attempts_total` | Counter | Total failed login attempts |
| `security_threats_detected_total` | Counter | Total security threats detected by type |
| `security_threats_active` | Gauge | Number of active unresolved threats |
| `blocked_ips_total` | Gauge | Number of currently blocked IP addresses |
| `unauthorized_access_attempts_total` | Counter | Total unauthorized access attempts |
| `sql_injection_attempts_total` | Counter | Total SQL injection attempts detected |
| `authentication_events_total` | Counter | Total authentication events by type |

### System Health Metrics

| Metric Name | Type | Description |
|-------------|------|-------------|
| `process_cpu_usage` | Gauge | CPU usage percentage |
| `process_memory_usage` | Gauge | Memory usage in bytes |
| `process_runtime_dotnet_gc_heap_size_bytes` | Gauge | GC heap size in bytes |
| `process_runtime_dotnet_gc_collections_count` | Counter | GC collections by generation |
| `process_runtime_dotnet_thread_pool_threads_count` | Gauge | Active thread pool threads |
| `process_runtime_dotnet_thread_pool_queue_length` | Gauge | Thread pool queue length |
| `db_connection_pool_active` | Gauge | Active database connections |
| `db_connection_pool_idle` | Gauge | Idle database connections |
| `db_connection_pool_max` | Gauge | Maximum pool size |
| `db_queries_total` | Counter | Total database queries executed |
| `up` | Gauge | Service availability (1=UP, 0=DOWN) |
| `process_start_time_seconds` | Gauge | Process start time (Unix timestamp) |
| `exceptions_total` | Counter | Total exceptions thrown |
| `health_check_status` | Gauge | Health check status (0=Unhealthy, 1=Degraded, 2=Healthy) |

## Troubleshooting

### No Data Displayed

**Problem**: Dashboard shows "No data" or empty panels

**Solutions**:
1. Verify Prometheus is scraping metrics:
   ```bash
   curl http://localhost:9090/api/v1/targets
   ```
   Check that `thinkonerp-api` target is UP

2. Verify metrics endpoint is accessible:
   ```bash
   curl http://localhost:5000/metrics
   ```
   Should return Prometheus-format metrics

3. Check Prometheus data source in Grafana:
   - Configuration → Data Sources → Prometheus
   - Click "Test" button
   - Should show "Data source is working"

4. Verify time range:
   - Check dashboard time range (top right)
   - Ensure it covers period when API was running

### Metrics Not Matching Expected Values

**Problem**: Metrics show unexpected values or patterns

**Solutions**:
1. Check metric names in queries match actual metric names:
   ```bash
   curl http://localhost:5000/metrics | grep audit
   ```

2. Verify label filters in queries match your setup

3. Check aggregation functions (rate, increase, etc.) are appropriate

### Alerts Not Firing

**Problem**: Alerts configured but not triggering

**Solutions**:
1. Verify alert conditions are met:
   - Check metric values in panel
   - Ensure threshold is appropriate

2. Check alert evaluation:
   - Alerting → Alert Rules
   - View alert state and last evaluation

3. Verify notification channel is configured:
   - Alerting → Notification channels
   - Test notification channel

4. Check Grafana alerting is enabled:
   ```ini
   # grafana.ini
   [alerting]
   enabled = true
   ```

### High Resource Usage

**Problem**: Grafana or Prometheus consuming too much resources

**Solutions**:
1. Increase scrape interval in `prometheus.yml`:
   ```yaml
   scrape_interval: 30s  # Increase from 15s
   ```

2. Reduce dashboard refresh rate:
   - Change from 30s to 1m or 5m

3. Limit retention period in Prometheus:
   ```bash
   --storage.tsdb.retention.time=7d  # Reduce from default 15d
   ```

4. Use recording rules for expensive queries:
   ```yaml
   # prometheus.yml
   rule_files:
     - "recording_rules.yml"
   ```

## Best Practices

### Dashboard Organization

1. **Create Folders**: Organize dashboards by category
   - Traceability System
   - Performance
   - Security
   - Infrastructure

2. **Use Tags**: Tag dashboards for easy filtering
   - audit, performance, security, health

3. **Set Home Dashboard**: Set most important dashboard as home
   - Configuration → Preferences → Home Dashboard

### Monitoring Strategy

1. **Start with Overview**: Use "Audit System Health" as primary dashboard
2. **Drill Down**: Use other dashboards for detailed investigation
3. **Set Up Alerts**: Configure critical alerts first, then warnings
4. **Regular Review**: Review dashboards weekly to adjust thresholds
5. **Document Changes**: Keep notes on threshold adjustments

### Performance Optimization

1. **Use Variables**: Create dashboard variables for dynamic filtering
2. **Limit Time Range**: Use shorter time ranges for better performance
3. **Cache Queries**: Enable query caching in Grafana
4. **Use Recording Rules**: Pre-compute expensive queries in Prometheus

## Integration with Operational Runbooks

These dashboards integrate with the operational runbooks in `docs/OPERATIONAL_RUNBOOKS.md`:

- **High Queue Depth Alert** → Runbook 1: Audit Queue Depth Exceeding Threshold
- **Audit Write Failures** → Runbook 2: Audit Write Failures
- **High API Latency** → Runbook 3: High API Latency
- **Connection Pool Exhaustion** → Runbook 4: Database Connection Pool Exhaustion
- **Circuit Breaker Open** → Runbook 5: Circuit Breaker Open State
- **Failed Login Attack** → Runbook 6: Failed Login Attack Detection
- **Slow Queries** → Runbook 7: Slow Query Performance

When an alert fires, refer to the corresponding runbook for diagnosis and resolution steps.

## Support and Maintenance

### Regular Maintenance Tasks

1. **Weekly**:
   - Review alert history
   - Check for false positives
   - Adjust thresholds if needed

2. **Monthly**:
   - Review dashboard usage
   - Update panels based on feedback
   - Clean up unused dashboards

3. **Quarterly**:
   - Review metric retention policies
   - Optimize slow queries
   - Update documentation

### Getting Help

- **Grafana Documentation**: https://grafana.com/docs/
- **Prometheus Documentation**: https://prometheus.io/docs/
- **OpenTelemetry Documentation**: https://opentelemetry.io/docs/
- **Project Documentation**: See `docs/APM_CONFIGURATION_GUIDE.md`

## Version History

- **v1.0** (2024-01-XX): Initial dashboard release
  - Audit System Health dashboard
  - Request Tracing & Performance dashboard
  - Security Monitoring dashboard
  - System Health & Resources dashboard

## Contributing

To contribute improvements to these dashboards:

1. Make changes in Grafana UI
2. Export dashboard JSON (Share → Export → Save to file)
3. Update the JSON file in this directory
4. Update this README if adding new metrics or panels
5. Test import on clean Grafana instance
6. Submit changes for review

## License

These dashboards are part of the ThinkOnErp Full Traceability System and follow the same license as the main project.
