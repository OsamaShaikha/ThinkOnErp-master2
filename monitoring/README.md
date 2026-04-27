# ThinkOnErp Monitoring Stack

This directory contains Grafana dashboards and monitoring infrastructure for the ThinkOnErp Full Traceability System.

## Contents

- **dashboards/**: Grafana dashboard JSON files
  - `audit-system-health.json` - Audit logging system monitoring
  - `performance-metrics.json` - API performance and latency tracking
  - `security-monitoring.json` - Security threats and authentication monitoring
  - `system-health.json` - System resources and health status

- **prometheus/**: Prometheus configuration
  - `prometheus.yml` - Prometheus scrape configuration
  - `alerts/` - Alert rule files (54 rules total)
    - `audit-system-alerts.yml` - Queue depth and processing delays (14 rules)
    - `database-alerts.yml` - Database connection pool and health (10 rules)
    - `audit-failures-alerts.yml` - Audit write failures and system health (13 rules)
    - `security-alerts.yml` - Security threats and authentication (17 rules)

- **grafana/**: Grafana provisioning configuration
  - `provisioning/datasources/` - Data source configuration
  - `provisioning/dashboards/` - Dashboard provisioning configuration

- **docker-compose.monitoring.yml**: Docker Compose file for monitoring stack
- **DASHBOARD_SETUP_GUIDE.md**: Comprehensive setup and usage guide
- **ALERT_CONFIGURATION_GUIDE.md**: Alert rules documentation and configuration guide

## Quick Start

### 1. Start the Monitoring Stack

```bash
# From the monitoring directory
docker-compose -f docker-compose.monitoring.yml up -d
```

This will start:
- **Prometheus** on http://localhost:9090
- **Grafana** on http://localhost:3000
- **AlertManager** on http://localhost:9093 (optional)

### 2. Access Grafana

1. Open http://localhost:3000
2. Login with default credentials:
   - Username: `admin`
   - Password: `admin`
3. Change password on first login

### 3. View Dashboards

Dashboards are automatically provisioned and available in the "ThinkOnErp" folder:
- Audit System Health Dashboard
- Performance Metrics Dashboard
- Security Monitoring Dashboard
- System Health Dashboard

## Prerequisites

- Docker and Docker Compose installed
- ThinkOnErp API running with metrics endpoint enabled at `/metrics`
- Port 3000 (Grafana), 9090 (Prometheus), and 9093 (AlertManager) available

## Configuration

### Prometheus

Edit `prometheus/prometheus.yml` to configure:
- Scrape intervals
- Target endpoints
- Alert rules (loaded from `alerts/` directory)

Alert rules are organized by category:
- **Audit System Alerts** (14 rules): Queue depth, processing delays, throughput
- **Database Alerts** (10 rules): Connection pool, health, query performance
- **Audit Failures Alerts** (13 rules): Write failures, circuit breaker, data integrity
- **Security Alerts** (17 rules): Authentication threats, security threats, anomalies

See [Alert Configuration Guide](ALERT_CONFIGURATION_GUIDE.md) for details.

### Grafana

Dashboards are automatically provisioned from the `dashboards/` directory. To customize:
1. Edit dashboard JSON files directly, or
2. Modify dashboards in Grafana UI and export

### Alerts

Alert rules are automatically loaded from the `alerts/` directory. To configure notifications:

1. Navigate to **Alerting** > **Notification channels** in Grafana
2. Add email, Slack, PagerDuty, or webhook notifications
3. Link notification channels to dashboard alerts

For detailed alert configuration, testing, and response procedures, see:
- [Alert Configuration Guide](ALERT_CONFIGURATION_GUIDE.md)

**54 alert rules** monitor:
- ✅ Queue depth and processing delays (14 rules) - **CONFIGURED**
- ✅ Database connection pool exhaustion (10 rules) - **CONFIGURED**
- ✅ Audit logging failures (13 rules) - **CONFIGURED**
- ✅ Security threats and anomalies (17 rules) - **CONFIGURED**

**Alert rules are now enabled in prometheus.yml and ready for production use.**

## Dashboard Overview

### Audit System Health
Monitors audit logging system performance:
- Queue depth and processing times
- Write latency and success rates
- Circuit breaker state
- Database connection pool utilization

### Performance Metrics
Tracks API performance:
- Request rates and latency percentiles
- Slow requests and queries
- CPU and memory usage
- Garbage collection metrics

### Security Monitoring
Monitors security events:
- Failed login attempts
- Security threats by type
- Unauthorized access attempts
- SQL injection and XSS detection

### System Health
Overall system health monitoring:
- System health status
- API availability
- Database health
- Resource utilization

## Stopping the Monitoring Stack

```bash
docker-compose -f docker-compose.monitoring.yml down
```

To remove volumes (data will be lost):
```bash
docker-compose -f docker-compose.monitoring.yml down -v
```

## Troubleshooting

### No Data in Dashboards

1. Verify ThinkOnErp API is running and exposing metrics:
   ```bash
   curl http://localhost:5000/metrics
   ```

2. Check Prometheus targets:
   - Open http://localhost:9090
   - Navigate to Status > Targets
   - Verify `thinkonerp-api` is UP

3. Check Prometheus logs:
   ```bash
   docker logs thinkonerp-prometheus
   ```

### Grafana Connection Issues

1. Verify Grafana is running:
   ```bash
   docker ps | grep grafana
   ```

2. Check Grafana logs:
   ```bash
   docker logs thinkonerp-grafana
   ```

3. Verify data source configuration:
   - In Grafana, go to Configuration > Data Sources
   - Test the Prometheus connection

## Documentation

For detailed setup instructions, customization, and best practices, see:
- [Dashboard Setup Guide](DASHBOARD_SETUP_GUIDE.md) - Dashboard configuration and usage
- [Alert Configuration Guide](ALERT_CONFIGURATION_GUIDE.md) - Alert rules and notification setup
- [APM Configuration Guide](../docs/APM_CONFIGURATION_GUIDE.md) - Application performance monitoring

## Support

For monitoring setup support:
- Review the Dashboard Setup Guide
- Check container logs
- Verify metrics endpoint is accessible
- Ensure ports are not blocked by firewall

## Next Steps

After setting up dashboards:
1. ✅ Configure Prometheus and Grafana (Task 24.2 - COMPLETED)
2. ✅ Configure alert rules (Tasks 24.3-24.6 - COMPLETED)
3. Configure alert notifications (email, Slack, PagerDuty)
4. Create operational runbooks (Task 24.7)
5. Document troubleshooting procedures (Task 24.8)
