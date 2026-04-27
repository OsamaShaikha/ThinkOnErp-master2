# Quick Start Guide - Monitoring Stack Setup

This guide will help you quickly set up Prometheus and Grafana monitoring for the ThinkOnErp Full Traceability System.

## Prerequisites

- Docker and Docker Compose installed
- ThinkOnErp API running and exposing metrics at `/metrics` endpoint
- Ports 3000 (Grafana) and 9090 (Prometheus) available

## Step 1: Start Monitoring Stack

Navigate to the monitoring directory and start the services:

```bash
cd monitoring
docker-compose -f docker-compose.monitoring.yml up -d
```

This will start:
- **Prometheus** on http://localhost:9090
- **Grafana** on http://localhost:3000

## Step 2: Verify Prometheus is Scraping Metrics

1. Open Prometheus UI: http://localhost:9090
2. Go to Status → Targets
3. Verify `thinkonerp-api` target shows as "UP"
4. If target is "DOWN", check:
   - ThinkOnErp API is running
   - Metrics endpoint is accessible: `curl http://localhost:5000/metrics`
   - Network connectivity between containers

## Step 3: Access Grafana

1. Open Grafana: http://localhost:3000
2. Login with default credentials:
   - Username: `admin`
   - Password: `admin` (or value of `GRAFANA_ADMIN_PASSWORD` env var)
3. Change password when prompted (recommended)

## Step 4: Verify Dashboards are Loaded

1. Click "Dashboards" in the left sidebar
2. Navigate to "Traceability System" folder
3. You should see 4 dashboards:
   - Audit System Health
   - Request Tracing & Performance
   - Security Monitoring
   - System Health & Resources

## Step 5: Verify Data is Flowing

1. Open "Audit System Health" dashboard
2. Check that panels show data (not "No data")
3. If no data:
   - Wait 30-60 seconds for first scrape
   - Check time range (top right) - set to "Last 5 minutes"
   - Verify Prometheus data source is working (Configuration → Data Sources → Prometheus → Test)

## Step 6: Configure Alerts (Optional)

### Email Notifications

1. Go to Alerting → Notification channels
2. Click "New channel"
3. Select "Email"
4. Configure SMTP settings:
   ```
   Name: Email Alerts
   Email addresses: ops-team@example.com
   SMTP Host: smtp.gmail.com:587
   User: your-email@gmail.com
   Password: your-app-password
   ```
5. Click "Test" to verify
6. Save

### Slack Notifications

1. Create Slack Incoming Webhook:
   - Go to https://api.slack.com/apps
   - Create new app → Incoming Webhooks
   - Copy webhook URL

2. In Grafana:
   - Alerting → Notification channels
   - New channel → Slack
   - Paste webhook URL
   - Test and save

### Link Alerts to Dashboards

1. Open any dashboard with alerts (e.g., "Audit System Health")
2. Edit panel with alert (e.g., "Audit Queue Depth")
3. Go to Alert tab
4. Under "Notifications", select your notification channel
5. Save dashboard

## Step 7: Customize for Your Environment

### Update Prometheus Targets

If your API is not at `thinkonerp-api:5000`, edit `prometheus.yml`:

```yaml
scrape_configs:
  - job_name: 'thinkonerp-api'
    static_configs:
      - targets: ['your-api-host:5000']  # Update this
```

Reload Prometheus configuration:
```bash
docker-compose -f docker-compose.monitoring.yml restart prometheus
```

### Adjust Alert Thresholds

See `dashboards/README.md` for recommended thresholds and how to customize them.

### Change Grafana Admin Password

Set environment variable before starting:
```bash
export GRAFANA_ADMIN_PASSWORD=your-secure-password
docker-compose -f docker-compose.monitoring.yml up -d
```

## Common Issues and Solutions

### Issue: Prometheus shows "Connection Refused" for API target

**Solution**:
1. Check if API is running: `docker ps | grep thinkonerp-api`
2. Verify metrics endpoint: `curl http://localhost:5000/metrics`
3. Check network connectivity:
   ```bash
   docker network ls
   docker network inspect thinkonerp-monitoring
   ```
4. Ensure API is on same network or update `prometheus.yml` with correct hostname

### Issue: Grafana shows "No data" in dashboards

**Solution**:
1. Verify Prometheus is scraping:
   - Open http://localhost:9090/targets
   - Check target is UP
2. Test Prometheus query:
   - Go to http://localhost:9090/graph
   - Run query: `up`
   - Should return 1
3. Check Grafana data source:
   - Configuration → Data Sources → Prometheus
   - Click "Test" - should show "Data source is working"
4. Adjust time range in dashboard (top right)

### Issue: Dashboards not appearing in Grafana

**Solution**:
1. Check provisioning logs:
   ```bash
   docker logs thinkonerp-grafana | grep provisioning
   ```
2. Verify dashboard files are mounted:
   ```bash
   docker exec thinkonerp-grafana ls /etc/grafana/provisioning/dashboards
   ```
3. Restart Grafana:
   ```bash
   docker-compose -f docker-compose.monitoring.yml restart grafana
   ```

### Issue: Alerts not firing

**Solution**:
1. Check alert state in Grafana:
   - Alerting → Alert Rules
   - View alert state and last evaluation
2. Verify alert conditions are met:
   - Open dashboard panel
   - Check if metric value exceeds threshold
3. Check notification channel is configured:
   - Edit panel → Alert tab → Notifications
4. Test notification channel:
   - Alerting → Notification channels → Test

## Next Steps

1. **Review Dashboards**: Familiarize yourself with all 4 dashboards
2. **Set Up Alerts**: Configure notification channels and link to critical alerts
3. **Customize Thresholds**: Adjust alert thresholds based on your environment
4. **Create Runbooks**: Document response procedures for each alert
5. **Regular Review**: Schedule weekly reviews of metrics and alerts

## Production Deployment

For production deployment:

1. **Use External Volumes**: Store data on persistent volumes
   ```yaml
   volumes:
     prometheus-data:
       driver: local
       driver_opts:
         type: none
         o: bind
         device: /data/prometheus
   ```

2. **Enable HTTPS**: Use reverse proxy (nginx, Traefik) with SSL
3. **Secure Credentials**: Use secrets management (Docker secrets, Vault)
4. **Configure Retention**: Adjust Prometheus retention based on storage
5. **Set Up Backups**: Backup Grafana dashboards and Prometheus data
6. **Enable Authentication**: Configure OAuth or LDAP for Grafana
7. **Resource Limits**: Set CPU and memory limits in docker-compose

## Monitoring the Monitoring Stack

Monitor Prometheus and Grafana themselves:

1. **Prometheus Self-Monitoring**:
   - Already configured in `prometheus.yml`
   - View at http://localhost:9090/targets

2. **Grafana Health Check**:
   ```bash
   curl http://localhost:3000/api/health
   ```

3. **Container Health**:
   ```bash
   docker-compose -f docker-compose.monitoring.yml ps
   ```

## Stopping the Monitoring Stack

To stop all monitoring services:

```bash
cd monitoring
docker-compose -f docker-compose.monitoring.yml down
```

To stop and remove all data:

```bash
docker-compose -f docker-compose.monitoring.yml down -v
```

## Support

For detailed information:
- Dashboard documentation: `dashboards/README.md`
- APM configuration: `../docs/APM_CONFIGURATION_GUIDE.md`
- Operational runbooks: `../docs/OPERATIONAL_RUNBOOKS.md`

For issues:
- Check container logs: `docker-compose -f docker-compose.monitoring.yml logs`
- Review Prometheus logs: `docker logs thinkonerp-prometheus`
- Review Grafana logs: `docker logs thinkonerp-grafana`
