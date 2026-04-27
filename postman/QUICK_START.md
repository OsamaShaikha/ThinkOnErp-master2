# Quick Start Guide - Full Traceability System Postman Collection

## 5-Minute Setup

### Step 1: Import Collection (1 minute)

1. Open Postman
2. Click **Import** button (top left)
3. Drag and drop `Full-Traceability-System-API.postman_collection.json`
4. Click **Import**

### Step 2: Import Environment (Optional, 30 seconds)

1. Click **Import** button again
2. Drag and drop `Full-Traceability-System.postman_environment.json`
3. Click **Import**
4. Select "Full Traceability System - Local" from the environment dropdown (top right)

### Step 3: Configure Base URL (30 seconds)

1. Click on the collection name "Full Traceability System API"
2. Go to the **Variables** tab
3. Update `baseUrl` if your API is not running on `http://localhost:5000`
4. Click **Save**

### Step 4: Login (1 minute)

1. Expand **Authentication** folder
2. Click **Login (Admin)**
3. Update credentials in the request body if needed:
   ```json
   {
     "username": "admin",
     "password": "Admin@123"
   }
   ```
4. Click **Send**
5. ✓ Token is automatically saved!

### Step 5: Test an Endpoint (1 minute)

1. Expand **Audit Logs** folder
2. Click **Get Dashboard Counters**
3. Click **Send**
4. ✓ You should see audit log statistics!

## Common Tasks

### View Audit Logs
```
Audit Logs > Get Legacy Audit Logs
```
Filter by status, company, module, or date range using query parameters.

### Generate Compliance Report
```
Compliance Reports > GDPR > GDPR Access Report (JSON)
```
Set `dataSubjectId`, `startDate`, and `endDate` query parameters.

### Check System Health
```
Monitoring > Health & Performance > Get System Health
```
No authentication required for this endpoint.

### Monitor Security Threats
```
Monitoring > Security Monitoring > Get Active Security Threats
```
View all active security threats with pagination.

### Create Alert Rule
```
Alerts > Alert Rules > Create Alert Rule
```
Configure conditions, thresholds, and notification channels.

## Tips

### Auto-Populated Variables
- `token` - Set automatically after login
- `refreshToken` - Set automatically after login
- `auditLogId` - Set from first result in correlation/entity queries
- `alertRuleId` - Set from first result in alert rules query

### Query Parameters
- **Enabled** (checkbox checked) = included in request
- **Disabled** (checkbox unchecked) = excluded from request
- Toggle parameters on/off to test different scenarios

### Export Formats
Change the `format` query parameter:
- `json` - Structured data (default)
- `csv` - Spreadsheet format
- `pdf` - Formatted report (if implemented)

### Pagination
Most list endpoints support:
- `pageNumber` - Page number (1-based)
- `pageSize` - Items per page (max: 100)

## Troubleshooting

### 401 Unauthorized
**Solution**: Run **Authentication > Login (Admin)** to get a new token

### 403 Forbidden
**Solution**: Ensure you're using an admin account

### 404 Not Found
**Solution**: Verify the resource ID exists (check list endpoints first)

### Connection Refused
**Solution**: Ensure the API is running on the configured `baseUrl`

## Next Steps

1. **Explore the Collection**: Browse all 5 main folders
2. **Read the README**: See `README.md` for detailed documentation
3. **Test Workflows**: Try the common workflows in the README
4. **Customize**: Add your own requests or modify existing ones

## Support

- **API Documentation**: See `.kiro/specs/full-traceability-system/design.md`
- **Requirements**: See `.kiro/specs/full-traceability-system/requirements.md`
- **Issues**: Check server logs for detailed error messages

## Collection Structure

```
Full Traceability System API/
├── Authentication/
│   ├── Login (Admin)
│   └── Refresh Token
├── Audit Logs/
│   ├── Get Legacy Audit Logs
│   ├── Get Dashboard Counters
│   ├── Update Audit Log Status
│   ├── Get Logs by Correlation ID
│   ├── Get Entity History
│   ├── Get User Action Replay
│   ├── Search Audit Logs
│   └── Export Audit Logs (CSV/JSON)
├── Compliance Reports/
│   ├── GDPR/
│   │   ├── GDPR Access Report
│   │   └── GDPR Data Export
│   ├── SOX/
│   │   ├── SOX Financial Access Report
│   │   └── SOX Segregation of Duties Report
│   ├── ISO 27001/
│   │   └── ISO 27001 Security Report
│   └── General Reports/
│       ├── User Activity Report
│       └── Data Modification Report
├── Monitoring/
│   ├── Health & Performance/
│   │   ├── Get System Health
│   │   ├── Get Endpoint Statistics
│   │   ├── Get Slow Requests
│   │   ├── Get Slow Queries
│   │   ├── Get Connection Pool Metrics
│   │   ├── Get Audit Queue Depth
│   │   └── Get Audit Metrics
│   ├── Memory Management/
│   │   ├── Get Memory Metrics
│   │   ├── Get Memory Pressure
│   │   ├── Get Memory Optimization Recommendations
│   │   ├── Optimize Memory
│   │   └── Force Garbage Collection
│   └── Security Monitoring/
│       ├── Get Active Security Threats
│       ├── Get Daily Security Summary
│       ├── Check Failed Login Pattern
│       ├── Get Failed Login Count
│       ├── Check SQL Injection
│       ├── Check XSS
│       └── Check Anomalous Activity
└── Alerts/
    ├── Alert Rules/
    │   ├── Get Alert Rules
    │   ├── Create Alert Rule
    │   ├── Update Alert Rule
    │   └── Delete Alert Rule
    ├── Alert History/
    │   ├── Get Alert History
    │   ├── Acknowledge Alert
    │   └── Resolve Alert
    └── Notification Testing/
        ├── Test Email Notification
        ├── Test Webhook Notification
        └── Test SMS Notification
```

## Happy Testing! 🚀
