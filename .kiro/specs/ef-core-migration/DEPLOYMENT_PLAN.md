# EF Core Migration - Deployment Plan

## Document Information

**Version**: 1.0  
**Last Updated**: 2024  
**Status**: Ready for Review  
**Owner**: DevOps Team  
**Requirement**: REQ-19 - Deployment and Zero Downtime

---

## Executive Summary

This deployment plan outlines the strategy for migrating ThinkOnErp from ADO.NET to Entity Framework Core in production with **zero downtime**. The approach uses blue-green deployment with gradual traffic shifting, feature flags for controlled rollout, comprehensive health checks, and automated rollback mechanisms.

**Key Objectives:**
- Zero downtime during migration
- Gradual traffic shifting to minimize risk
- Automated rollback within 5 minutes if issues occur
- Comprehensive monitoring and alerting
- Rollback capability at any stage

**Estimated Deployment Time**: 4-6 hours (with 24-hour monitoring period)

---

## Table of Contents

1. [Deployment Strategy Overview](#deployment-strategy-overview)
2. [Blue-Green Deployment Architecture](#blue-green-deployment-architecture)
3. [Gradual Traffic Shifting](#gradual-traffic-shifting)
4. [Pre-Deployment Checklist](#pre-deployment-checklist)
5. [Deployment Procedure](#deployment-procedure)
6. [Health Check Verification](#health-check-verification)
7. [Monitoring During Deployment](#monitoring-during-deployment)
8. [Rollback Procedures](#rollback-procedures)
9. [Automated Rollback Triggers](#automated-rollback-triggers)
10. [Connection Pool Warm-Up](#connection-pool-warm-up)
11. [Post-Deployment Validation](#post-deployment-validation)
12. [Communication Plan](#communication-plan)

---

## 1. Deployment Strategy Overview

### Approach: Blue-Green with Feature Flags

The deployment uses a **hybrid blue-green deployment** combined with **feature flags** to enable gradual migration:

```
┌─────────────────────────────────────────────────────────────┐
│                    Load Balancer                             │
│              (Traffic Distribution Control)                  │
└─────────────────────────────────────────────────────────────┘
                            │
        ┌───────────────────┴───────────────────┐
        ▼                                       ▼
┌──────────────────┐                  ┌──────────────────┐
│   Blue (Current) │                  │  Green (New)     │
│   ADO.NET        │                  │  EF Core         │
│   Version        │                  │  Version         │
│                  │                  │                  │
│  - Stable        │                  │  - New Code      │
│  - Production    │                  │  - Feature Flags │
│  - Fallback      │                  │  - Gradual       │
└──────────────────┘                  └──────────────────┘
        │                                       │
        └───────────────────┬───────────────────┘
                            ▼
                ┌───────────────────────┐
                │   Oracle Database     │
                │   (Shared)            │
                └───────────────────────┘
```

### Key Principles

1. **Both versions run simultaneously** - Blue (ADO.NET) and Green (EF Core) environments active
2. **Feature flags control repository selection** - Per-repository flags enable gradual migration
3. **Shared database** - Both versions access the same Oracle database
4. **Traffic shifting** - Gradual increase of traffic to Green environment
5. **Instant rollback** - Disable feature flags to revert to Blue environment

### Migration Phases

| Phase | Duration | Traffic Split | Repositories Enabled | Risk Level |
|-------|----------|---------------|---------------------|------------|
| **Phase 0: Pre-Deployment** | 1 hour | 100% Blue | None | None |
| **Phase 1: Pilot** | 2 hours | 95% Blue / 5% Green | Core entities (Company, Branch, Currency) | Low |
| **Phase 2: Expansion** | 2 hours | 70% Blue / 30% Green | Add User, Role, Permission | Medium |
| **Phase 3: Full Migration** | 2 hours | 30% Blue / 70% Green | All 23 repositories | Medium |
| **Phase 4: Completion** | 24 hours | 100% Green | All repositories | Low |
| **Phase 5: Decommission** | 1 week | 100% Green | Blue environment removed | None |

---

## 2. Blue-Green Deployment Architecture

### Infrastructure Setup

#### Blue Environment (Current - ADO.NET)
```yaml
Environment: Production-Blue
Version: Current (ADO.NET)
Servers: 
  - app-server-01 (Primary)
  - app-server-02 (Secondary)
Load Balancer Pool: blue-pool
Database Connection: OracleDbContext (ADO.NET)
Feature Flags: UseEfCore = false (all repositories)
Status: Active, Fallback Ready
```

#### Green Environment (New - EF Core)
```yaml
Environment: Production-Green
Version: New (EF Core)
Servers:
  - app-server-03 (Primary)
  - app-server-04 (Secondary)
Load Balancer Pool: green-pool
Database Connection: ThinkOnErpDbContext (EF Core)
Feature Flags: UseEfCore = true (gradual enablement)
Status: Standby, Ready for Traffic
```

### Load Balancer Configuration

**Initial State (Pre-Deployment):**
```nginx
upstream backend {
    server app-server-01:5000 weight=50;  # Blue
    server app-server-02:5000 weight=50;  # Blue
    server app-server-03:5000 weight=0;   # Green (no traffic)
    server app-server-04:5000 weight=0;   # Green (no traffic)
}
```

**Phase 1 (5% Traffic to Green):**
```nginx
upstream backend {
    server app-server-01:5000 weight=47;  # Blue (95%)
    server app-server-02:5000 weight=48;  # Blue
    server app-server-03:5000 weight=3;   # Green (5%)
    server app-server-04:5000 weight=2;   # Green
}
```

### Database Connection Configuration

Both environments share the same Oracle database but use different connection contexts:

**Blue (ADO.NET):**
```json
{
  "ConnectionStrings": {
    "OracleDb": "Data Source=prod-oracle:1521/THINKONERP;User Id=THINKONERP_USER;Password=***;Pooling=true;Min Pool Size=10;Max Pool Size=100;"
  },
  "FeatureFlags": {
    "UseEfCore": false
  }
}
```

**Green (EF Core):**
```json
{
  "ConnectionStrings": {
    "OracleDb": "Data Source=prod-oracle:1521/THINKONERP;User Id=THINKONERP_USER;Password=***;Pooling=true;Min Pool Size=10;Max Pool Size=100;"
  },
  "FeatureFlags": {
    "UseEfCore": true,
    "EfCoreRepositories": {
      "CompanyRepository": true,
      "BranchRepository": true,
      "CurrencyRepository": true
    }
  }
}
```

---

## 3. Gradual Traffic Shifting

### Traffic Shifting Strategy

Traffic is shifted gradually over 6 hours with validation at each stage:

```
Time    │ Blue  │ Green │ Repositories Enabled        │ Validation
────────┼───────┼───────┼─────────────────────────────┼────────────────
T+0h    │ 100%  │   0%  │ None                        │ Pre-deployment
T+1h    │  95%  │   5%  │ Company, Branch, Currency   │ 30 min monitor
T+2h    │  90%  │  10%  │ + FiscalYear                │ 30 min monitor
T+3h    │  70%  │  30%  │ + User, Role, Permission    │ 30 min monitor
T+4h    │  50%  │  50%  │ + Screen, System            │ 30 min monitor
T+5h    │  30%  │  70%  │ + Ticket entities           │ 30 min monitor
T+6h    │   0%  │ 100%  │ All 23 repositories         │ 24h monitor
```

### Traffic Shifting Commands

**Using Nginx:**
```bash
# Phase 1: 5% to Green
sudo nano /etc/nginx/conf.d/thinkonerp-upstream.conf
# Update weights: Blue=95, Green=5
sudo nginx -t && sudo nginx -s reload

# Phase 2: 10% to Green
# Update weights: Blue=90, Green=10
sudo nginx -s reload

# Phase 3: 30% to Green
# Update weights: Blue=70, Green=30
sudo nginx -s reload

# Phase 4: 50% to Green
# Update weights: Blue=50, Green=50
sudo nginx -s reload

# Phase 5: 70% to Green
# Update weights: Blue=30, Green=70
sudo nginx -s reload

# Phase 6: 100% to Green
# Update weights: Blue=0, Green=100
sudo nginx -s reload
```

**Using Azure Application Gateway:**
```powershell
# Phase 1: 5% to Green
$appGw = Get-AzApplicationGateway -Name "thinkonerp-appgw" -ResourceGroupName "thinkonerp-rg"
$backendPool = Get-AzApplicationGatewayBackendAddressPool -ApplicationGateway $appGw -Name "backend-pool"

# Update backend pool weights
Set-AzApplicationGatewayBackendHttpSettings -ApplicationGateway $appGw `
    -Name "backend-settings" `
    -WeightedRoundRobin @{
        "blue-pool" = 95
        "green-pool" = 5
    }

Set-AzApplicationGateway -ApplicationGateway $appGw
```

### Feature Flag Configuration

Feature flags are managed through Azure App Configuration or appsettings.json:

**Phase 1 Configuration:**
```json
{
  "FeatureManagement": {
    "EfCoreMigration": {
      "EnabledFor": [
        {
          "Name": "Percentage",
          "Parameters": {
            "Value": 5
          }
        }
      ]
    }
  },
  "EfCoreRepositories": {
    "CompanyRepository": true,
    "BranchRepository": true,
    "CurrencyRepository": true,
    "FiscalYearRepository": false,
    "UserRepository": false
  }
}
```

**Phase 3 Configuration:**
```json
{
  "FeatureManagement": {
    "EfCoreMigration": {
      "EnabledFor": [
        {
          "Name": "Percentage",
          "Parameters": {
            "Value": 30
          }
        }
      ]
    }
  },
  "EfCoreRepositories": {
    "CompanyRepository": true,
    "BranchRepository": true,
    "CurrencyRepository": true,
    "FiscalYearRepository": true,
    "UserRepository": true,
    "RoleRepository": true,
    "PermissionRepository": true
  }
}
```


---

## 4. Pre-Deployment Checklist

### 4.1 Code Readiness

- [ ] **All 23 repositories migrated to EF Core**
  - [ ] CompanyRepository, BranchRepository, UserRepository
  - [ ] RoleRepository, PermissionRepository, CurrencyRepository
  - [ ] FiscalYearRepository, SuperAdminRepository, AuthRepository
  - [ ] AuditRepository, ScreenRepository, SystemRepository
  - [ ] TicketRepository, TicketTypeRepository, TicketStatusRepository
  - [ ] TicketPriorityRepository, TicketCommentRepository, TicketAttachmentRepository
  - [ ] TicketConfigRepository, TicketCategoryRepository
  - [ ] AlertRepository, SavedSearchRepository, SearchAnalyticsRepository

- [ ] **All unit tests passing** (100% pass rate)
- [ ] **All integration tests passing** (100% pass rate)
- [ ] **Performance tests completed** (EF Core >= 95% of ADO.NET baseline)
- [ ] **Code review completed** (all PRs approved)
- [ ] **Security scan completed** (no critical vulnerabilities)

### 4.2 Infrastructure Readiness

- [ ] **Green environment provisioned**
  - [ ] app-server-03 configured and tested
  - [ ] app-server-04 configured and tested
  - [ ] Load balancer pool created
  - [ ] SSL certificates installed

- [ ] **Database connectivity verified**
  - [ ] EF Core can connect to production Oracle database
  - [ ] Connection pooling configured (Min=10, Max=100)
  - [ ] Connection string tested from Green environment

- [ ] **Monitoring configured**
  - [ ] Application Insights instrumentation key configured
  - [ ] Custom metrics for EF Core queries enabled
  - [ ] Alert rules created (error rate, response time)
  - [ ] Dashboard created for deployment monitoring

- [ ] **Feature flags configured**
  - [ ] Azure App Configuration service ready
  - [ ] Feature flag definitions created
  - [ ] Repository-level flags configured
  - [ ] Percentage-based rollout configured

### 4.3 Backup and Rollback Readiness

- [ ] **Database backup completed**
  - [ ] Full database backup taken
  - [ ] Backup verified and tested
  - [ ] Backup retention policy confirmed

- [ ] **Blue environment preserved**
  - [ ] ADO.NET version deployed and stable
  - [ ] Configuration backed up
  - [ ] Rollback procedure documented

- [ ] **Rollback scripts prepared**
  - [ ] Feature flag disable script ready
  - [ ] Load balancer revert script ready
  - [ ] Configuration rollback script ready

### 4.4 Team Readiness

- [ ] **Deployment team briefed**
  - [ ] Deployment lead assigned
  - [ ] Roles and responsibilities defined
  - [ ] Communication channels established (Slack, Teams)

- [ ] **Support team on standby**
  - [ ] Level 1 support team notified
  - [ ] Level 2 support team on call
  - [ ] Database administrator available

- [ ] **Stakeholders notified**
  - [ ] Business stakeholders informed
  - [ ] Maintenance window communicated (if applicable)
  - [ ] Escalation contacts confirmed

### 4.5 Documentation Readiness

- [ ] **Deployment runbook reviewed**
- [ ] **Rollback procedure reviewed**
- [ ] **Health check procedures documented**
- [ ] **Monitoring dashboard URLs documented**
- [ ] **Contact list updated**

---

## 5. Deployment Procedure

### Phase 0: Pre-Deployment (T-1 hour)

**Duration**: 1 hour  
**Objective**: Prepare environments and verify readiness

#### Step 0.1: Final Verification (15 minutes)
```bash
# 1. Verify Green environment is running
curl https://green.thinkonerp.com/health
# Expected: HTTP 200, Status: Healthy

# 2. Verify database connectivity
curl https://green.thinkonerp.com/health/database
# Expected: HTTP 200, Database: Connected

# 3. Verify feature flags are disabled
curl https://green.thinkonerp.com/api/admin/feature-flags
# Expected: All EfCoreRepositories = false

# 4. Verify Blue environment is stable
curl https://thinkonerp.com/health
# Expected: HTTP 200, Status: Healthy
```

#### Step 0.2: Database Backup (30 minutes)
```sql
-- Connect to Oracle as SYSDBA
sqlplus / as sysdba

-- Create backup
BEGIN
  RMAN.BACKUP_DATABASE(
    backup_type => 'FULL',
    tag => 'PRE_EFCORE_MIGRATION',
    validate => TRUE
  );
END;
/

-- Verify backup
SELECT * FROM V$BACKUP_SET 
WHERE TAG = 'PRE_EFCORE_MIGRATION'
ORDER BY COMPLETION_TIME DESC;
```

#### Step 0.3: Enable Monitoring (15 minutes)
```bash
# 1. Open deployment dashboard
open https://portal.azure.com/#@thinkonerp/dashboard/deployment

# 2. Start monitoring script
./scripts/monitor-deployment.sh &

# 3. Verify alerts are active
az monitor alert list --resource-group thinkonerp-rg --query "[?enabled==true]"
```

**Go/No-Go Decision Point**: Review checklist. All items must be checked before proceeding.

---

### Phase 1: Pilot Deployment (T+0 to T+2 hours)

**Duration**: 2 hours  
**Objective**: Enable core repositories with 5% traffic  
**Repositories**: Company, Branch, Currency (3 repositories)

#### Step 1.1: Enable Feature Flags (5 minutes)
```bash
# Update feature flags in Azure App Configuration
az appconfig kv set \
  --name thinkonerp-appconfig \
  --key "EfCoreRepositories:CompanyRepository" \
  --value "true" \
  --yes

az appconfig kv set \
  --name thinkonerp-appconfig \
  --key "EfCoreRepositories:BranchRepository" \
  --value "true" \
  --yes

az appconfig kv set \
  --name thinkonerp-appconfig \
  --key "EfCoreRepositories:CurrencyRepository" \
  --value "true" \
  --yes

# Verify flags are set
az appconfig kv list \
  --name thinkonerp-appconfig \
  --key "EfCoreRepositories:*"
```

#### Step 1.2: Shift 5% Traffic to Green (5 minutes)
```bash
# Update load balancer weights
sudo nano /etc/nginx/conf.d/thinkonerp-upstream.conf

# Set weights:
# Blue: 95 (app-server-01: 47, app-server-02: 48)
# Green: 5 (app-server-03: 3, app-server-04: 2)

# Test configuration
sudo nginx -t

# Reload Nginx
sudo nginx -s reload

# Verify traffic distribution
watch -n 5 'curl -s https://thinkonerp.com/api/admin/server-info | jq .serverName'
```

#### Step 1.3: Warm Up Connection Pool (10 minutes)
```bash
# Execute connection pool warm-up script
./scripts/warmup-connection-pool.sh green

# Script will:
# 1. Make 100 requests to /health/database
# 2. Execute sample queries for Company, Branch, Currency
# 3. Verify connection pool is populated

# Verify pool status
curl https://green.thinkonerp.com/api/admin/connection-pool-stats
# Expected: ActiveConnections >= 10, IdleConnections >= 5
```

#### Step 1.4: Monitor for 30 Minutes
```bash
# Watch key metrics
./scripts/monitor-metrics.sh --duration 30m --repositories Company,Branch,Currency

# Metrics to watch:
# - Error rate (should be < 0.1%)
# - Response time (should be < 500ms p95)
# - Database query time (should be < 200ms p95)
# - Connection pool usage (should be < 80%)
```

**Validation Criteria:**
- ✅ Error rate < 0.1%
- ✅ Response time p95 < 500ms
- ✅ No database connection errors
- ✅ No exceptions in logs

**Decision Point**: If validation fails, execute rollback procedure (Section 8).

#### Step 1.5: Extended Monitoring (90 minutes)
```bash
# Continue monitoring for 90 minutes
./scripts/monitor-metrics.sh --duration 90m --alert-on-threshold

# Review logs every 15 minutes
tail -f /var/log/thinkonerp/green/application.log | grep -i "error\|exception"

# Check database performance
sqlplus thinkonerp_user/password@prod-oracle <<EOF
SELECT sql_text, executions, elapsed_time/1000000 as elapsed_sec
FROM v\$sql
WHERE sql_text LIKE '%SYS_COMPANY%' OR sql_text LIKE '%SYS_BRANCH%'
ORDER BY elapsed_time DESC
FETCH FIRST 10 ROWS ONLY;
EOF
```

**Go/No-Go Decision Point**: Review metrics. Proceed to Phase 2 only if all validation criteria met.

---

### Phase 2: Expansion (T+2 to T+4 hours)

**Duration**: 2 hours  
**Objective**: Add User, Role, Permission repositories with 30% traffic  
**Repositories**: +FiscalYear, User, Role, Permission (7 total)

#### Step 2.1: Enable Additional Feature Flags (5 minutes)
```bash
# Enable additional repositories
for repo in FiscalYearRepository UserRepository RoleRepository PermissionRepository; do
  az appconfig kv set \
    --name thinkonerp-appconfig \
    --key "EfCoreRepositories:$repo" \
    --value "true" \
    --yes
done

# Verify
az appconfig kv list --name thinkonerp-appconfig --key "EfCoreRepositories:*"
```

#### Step 2.2: Shift 30% Traffic to Green (5 minutes)
```bash
# Update load balancer weights
# Blue: 70 (app-server-01: 35, app-server-02: 35)
# Green: 30 (app-server-03: 15, app-server-04: 15)

sudo nano /etc/nginx/conf.d/thinkonerp-upstream.conf
sudo nginx -t && sudo nginx -s reload
```

#### Step 2.3: Monitor for 30 Minutes
```bash
./scripts/monitor-metrics.sh --duration 30m --repositories User,Role,Permission,FiscalYear
```

**Validation Criteria:**
- ✅ Error rate < 0.1%
- ✅ Response time p95 < 500ms
- ✅ Authentication operations working correctly
- ✅ Permission checks functioning

#### Step 2.4: Extended Monitoring (90 minutes)
Continue monitoring with focus on authentication and authorization operations.

---

### Phase 3: Full Migration (T+4 to T+6 hours)

**Duration**: 2 hours  
**Objective**: Enable all 23 repositories with 70% traffic

#### Step 3.1: Enable All Remaining Repositories (10 minutes)
```bash
# Enable all remaining repositories
for repo in ScreenRepository SystemRepository CompanySystemRepository \
            TicketRepository TicketTypeRepository TicketStatusRepository \
            TicketPriorityRepository TicketCommentRepository TicketAttachmentRepository \
            TicketConfigRepository TicketCategoryRepository \
            SuperAdminRepository AuditRepository AlertRepository \
            SavedSearchRepository SearchAnalyticsRepository; do
  az appconfig kv set \
    --name thinkonerp-appconfig \
    --key "EfCoreRepositories:$repo" \
    --value "true" \
    --yes
done
```

#### Step 3.2: Shift 70% Traffic to Green (5 minutes)
```bash
# Blue: 30, Green: 70
sudo nano /etc/nginx/conf.d/thinkonerp-upstream.conf
sudo nginx -t && sudo nginx -s reload
```

#### Step 3.3: Monitor for 30 Minutes
```bash
./scripts/monitor-metrics.sh --duration 30m --all-repositories
```

#### Step 3.4: Extended Monitoring (90 minutes)
Monitor all operations with special attention to ticket system and audit logging.

---

### Phase 4: Completion (T+6 hours)

**Duration**: 24 hours  
**Objective**: Shift 100% traffic to Green, monitor for 24 hours

#### Step 4.1: Shift 100% Traffic to Green (5 minutes)
```bash
# Blue: 0, Green: 100
sudo nano /etc/nginx/conf.d/thinkonerp-upstream.conf
sudo nginx -t && sudo nginx -s reload

# Verify no traffic to Blue
watch -n 5 'curl -s https://thinkonerp.com/api/admin/server-info | jq .serverName'
# Should only show app-server-03 and app-server-04
```

#### Step 4.2: 24-Hour Monitoring
```bash
# Continuous monitoring for 24 hours
./scripts/monitor-metrics.sh --duration 24h --alert-on-threshold --email-report
```

**Validation Criteria (24 hours):**
- ✅ Error rate < 0.1%
- ✅ Response time p95 < 500ms
- ✅ No database connection issues
- ✅ No memory leaks
- ✅ No performance degradation

---

### Phase 5: Decommission Blue (T+1 week)

**Duration**: 1 week after Phase 4 completion  
**Objective**: Remove Blue environment

#### Step 5.1: Final Verification
```bash
# Verify Green has been stable for 1 week
./scripts/generate-stability-report.sh --days 7

# Review report for:
# - Error rates
# - Performance metrics
# - Incident count
# - User feedback
```

#### Step 5.2: Decommission Blue Environment
```bash
# 1. Remove Blue servers from load balancer
sudo nano /etc/nginx/conf.d/thinkonerp-upstream.conf
# Remove app-server-01 and app-server-02

# 2. Stop Blue application servers
ssh app-server-01 "sudo systemctl stop thinkonerp"
ssh app-server-02 "sudo systemctl stop thinkonerp"

# 3. Archive Blue configuration
tar -czf blue-environment-backup-$(date +%Y%m%d).tar.gz \
  /etc/thinkonerp/blue/ \
  /var/log/thinkonerp/blue/

# 4. Deallocate Blue servers (optional)
# Keep for 30 days before full decommission
```


---

## 6. Health Check Verification

### 6.1 Health Check Endpoints

The application exposes multiple health check endpoints for deployment verification:

#### Basic Health Check
```bash
# Endpoint: /health
# Purpose: Verify application is running

curl -i https://green.thinkonerp.com/health

# Expected Response:
HTTP/1.1 200 OK
Content-Type: application/json

{
  "status": "Healthy",
  "timestamp": "2024-01-15T10:30:00Z",
  "version": "2.0.0-efcore",
  "environment": "Production-Green"
}
```

#### Database Health Check
```bash
# Endpoint: /health/database
# Purpose: Verify EF Core database connectivity

curl -i https://green.thinkonerp.com/health/database

# Expected Response:
HTTP/1.1 200 OK
Content-Type: application/json

{
  "status": "Healthy",
  "database": "Connected",
  "provider": "Oracle.EntityFrameworkCore",
  "connectionPoolSize": 15,
  "activeConnections": 8,
  "idleConnections": 7,
  "responseTime": "45ms"
}
```

#### Repository Health Check
```bash
# Endpoint: /health/repositories
# Purpose: Verify each repository can execute queries

curl -i https://green.thinkonerp.com/health/repositories

# Expected Response:
HTTP/1.1 200 OK
Content-Type: application/json

{
  "status": "Healthy",
  "repositories": {
    "CompanyRepository": {
      "status": "Healthy",
      "provider": "EfCore",
      "testQuery": "SELECT COUNT(*) FROM SYS_COMPANY",
      "responseTime": "23ms"
    },
    "BranchRepository": {
      "status": "Healthy",
      "provider": "EfCore",
      "testQuery": "SELECT COUNT(*) FROM SYS_BRANCH",
      "responseTime": "18ms"
    },
    "UserRepository": {
      "status": "Healthy",
      "provider": "EfCore",
      "testQuery": "SELECT COUNT(*) FROM SYS_USERS",
      "responseTime": "31ms"
    }
    // ... all 23 repositories
  }
}
```

#### Feature Flag Health Check
```bash
# Endpoint: /health/feature-flags
# Purpose: Verify feature flag configuration

curl -i https://green.thinkonerp.com/health/feature-flags

# Expected Response:
HTTP/1.1 200 OK
Content-Type: application/json

{
  "status": "Healthy",
  "featureFlags": {
    "EfCoreMigration": {
      "enabled": true,
      "percentage": 5
    },
    "repositories": {
      "CompanyRepository": true,
      "BranchRepository": true,
      "CurrencyRepository": true,
      "UserRepository": false
      // ... all repositories
    }
  }
}
```

### 6.2 Health Check Verification Script

```bash
#!/bin/bash
# File: scripts/verify-health-checks.sh
# Purpose: Verify all health checks before traffic shift

set -e

GREEN_URL="https://green.thinkonerp.com"
TIMEOUT=10

echo "=== Health Check Verification ==="
echo "Target: $GREEN_URL"
echo "Timestamp: $(date)"
echo ""

# Function to check endpoint
check_endpoint() {
  local endpoint=$1
  local expected_status=$2
  local description=$3
  
  echo -n "Checking $description... "
  
  response=$(curl -s -w "\n%{http_code}" --max-time $TIMEOUT "$GREEN_URL$endpoint")
  http_code=$(echo "$response" | tail -n1)
  body=$(echo "$response" | sed '$d')
  
  if [ "$http_code" -eq "$expected_status" ]; then
    echo "✅ PASS (HTTP $http_code)"
    return 0
  else
    echo "❌ FAIL (HTTP $http_code)"
    echo "Response: $body"
    return 1
  fi
}

# Run health checks
check_endpoint "/health" 200 "Basic Health"
check_endpoint "/health/database" 200 "Database Connectivity"
check_endpoint "/health/repositories" 200 "Repository Health"
check_endpoint "/health/feature-flags" 200 "Feature Flags"

# Verify specific repository queries
echo ""
echo "=== Repository Query Tests ==="

test_repository() {
  local repo=$1
  echo -n "Testing $repo... "
  
  response=$(curl -s "$GREEN_URL/api/admin/test-repository?name=$repo")
  status=$(echo "$response" | jq -r '.status')
  
  if [ "$status" == "success" ]; then
    echo "✅ PASS"
  else
    echo "❌ FAIL"
    echo "Response: $response"
    return 1
  fi
}

test_repository "CompanyRepository"
test_repository "BranchRepository"
test_repository "UserRepository"

echo ""
echo "=== Health Check Verification Complete ==="
```

### 6.3 Load Balancer Health Check Configuration

Configure load balancer to use health checks for automatic failover:

**Nginx Configuration:**
```nginx
upstream backend {
    server app-server-03:5000 max_fails=3 fail_timeout=30s;
    server app-server-04:5000 max_fails=3 fail_timeout=30s;
}

server {
    listen 443 ssl;
    server_name thinkonerp.com;
    
    location /health {
        proxy_pass http://backend;
        proxy_connect_timeout 5s;
        proxy_read_timeout 10s;
        
        # Health check configuration
        health_check interval=10s fails=3 passes=2 uri=/health;
    }
}
```

**Azure Application Gateway Health Probe:**
```powershell
$probe = New-AzApplicationGatewayProbeConfig `
  -Name "efcore-health-probe" `
  -Protocol Https `
  -Path "/health/database" `
  -Interval 30 `
  -Timeout 10 `
  -UnhealthyThreshold 3 `
  -PickHostNameFromBackendHttpSettings

Add-AzApplicationGatewayProbeConfig -ApplicationGateway $appGw -ProbeConfig $probe
```

### 6.4 Pre-Traffic Shift Verification Checklist

Before shifting traffic to Green environment, verify:

- [ ] `/health` returns HTTP 200
- [ ] `/health/database` returns HTTP 200 with "Connected" status
- [ ] `/health/repositories` shows all enabled repositories as "Healthy"
- [ ] `/health/feature-flags` shows correct flag configuration
- [ ] Connection pool has at least 10 active connections
- [ ] No errors in application logs (last 5 minutes)
- [ ] Database query response times < 200ms
- [ ] Load balancer health probe passing

**Automated Verification:**
```bash
# Run before each traffic shift
./scripts/verify-health-checks.sh

# If all checks pass, proceed with traffic shift
# If any check fails, investigate and resolve before proceeding
```

---

## 7. Monitoring During Deployment

### 7.1 Key Metrics to Monitor

#### Application Metrics

| Metric | Threshold | Alert Level | Action |
|--------|-----------|-------------|--------|
| Error Rate | > 0.1% | Warning | Investigate |
| Error Rate | > 0.5% | Critical | Rollback |
| Response Time (p95) | > 500ms | Warning | Investigate |
| Response Time (p95) | > 1000ms | Critical | Rollback |
| Request Rate | < 50% of baseline | Warning | Check load balancer |
| CPU Usage | > 80% | Warning | Monitor |
| Memory Usage | > 85% | Warning | Monitor |
| Memory Usage | > 95% | Critical | Rollback |

#### Database Metrics

| Metric | Threshold | Alert Level | Action |
|--------|-----------|-------------|--------|
| Query Time (p95) | > 200ms | Warning | Investigate |
| Query Time (p95) | > 500ms | Critical | Rollback |
| Connection Pool Usage | > 80% | Warning | Monitor |
| Connection Pool Usage | > 95% | Critical | Increase pool size |
| Failed Connections | > 5 per minute | Critical | Rollback |
| Deadlocks | > 1 per hour | Warning | Investigate |
| Long Running Queries | > 10 seconds | Warning | Investigate |

#### EF Core Specific Metrics

| Metric | Threshold | Alert Level | Action |
|--------|-----------|-------------|--------|
| DbContext Creation Time | > 100ms | Warning | Investigate |
| Query Compilation Time | > 50ms | Warning | Use compiled queries |
| Change Tracker Entries | > 1000 | Warning | Check for tracking leaks |
| SaveChanges Duration | > 500ms | Warning | Investigate |

### 7.2 Monitoring Dashboard

**Azure Application Insights Dashboard:**

```kusto
// Error Rate Query
requests
| where timestamp > ago(5m)
| where cloud_RoleName == "thinkonerp-green"
| summarize 
    TotalRequests = count(),
    FailedRequests = countif(success == false),
    ErrorRate = (countif(success == false) * 100.0) / count()
| project ErrorRate, TotalRequests, FailedRequests

// Response Time Query
requests
| where timestamp > ago(5m)
| where cloud_RoleName == "thinkonerp-green"
| summarize 
    p50 = percentile(duration, 50),
    p95 = percentile(duration, 95),
    p99 = percentile(duration, 99)
| project p50, p95, p99

// EF Core Query Performance
dependencies
| where timestamp > ago(5m)
| where type == "SQL"
| where cloud_RoleName == "thinkonerp-green"
| summarize 
    QueryCount = count(),
    AvgDuration = avg(duration),
    p95Duration = percentile(duration, 95)
    by name
| order by p95Duration desc
| take 10

// Repository Usage by Provider
customMetrics
| where timestamp > ago(5m)
| where name == "RepositoryCall"
| extend provider = tostring(customDimensions.Provider)
| summarize Count = sum(value) by provider, tostring(customDimensions.Repository)
| order by Count desc
```

**Grafana Dashboard Configuration:**

```json
{
  "dashboard": {
    "title": "EF Core Migration Deployment",
    "panels": [
      {
        "title": "Error Rate",
        "targets": [
          {
            "expr": "rate(http_requests_total{job='thinkonerp-green',status=~'5..'}[5m])"
          }
        ],
        "alert": {
          "conditions": [
            {
              "evaluator": { "params": [0.005], "type": "gt" },
              "operator": { "type": "and" },
              "query": { "params": ["A", "5m", "now"] },
              "reducer": { "type": "avg" },
              "type": "query"
            }
          ]
        }
      },
      {
        "title": "Response Time (p95)",
        "targets": [
          {
            "expr": "histogram_quantile(0.95, rate(http_request_duration_seconds_bucket{job='thinkonerp-green'}[5m]))"
          }
        ]
      },
      {
        "title": "Database Query Time",
        "targets": [
          {
            "expr": "histogram_quantile(0.95, rate(db_query_duration_seconds_bucket{job='thinkonerp-green'}[5m]))"
          }
        ]
      },
      {
        "title": "Connection Pool Usage",
        "targets": [
          {
            "expr": "db_connection_pool_active{job='thinkonerp-green'} / db_connection_pool_max{job='thinkonerp-green'}"
          }
        ]
      }
    ]
  }
}
```

### 7.3 Real-Time Monitoring Script

```bash
#!/bin/bash
# File: scripts/monitor-deployment.sh
# Purpose: Real-time monitoring during deployment

GREEN_URL="https://green.thinkonerp.com"
BLUE_URL="https://thinkonerp.com"
INTERVAL=30  # seconds

echo "=== EF Core Deployment Monitoring ==="
echo "Started: $(date)"
echo "Monitoring interval: ${INTERVAL}s"
echo ""

while true; do
  clear
  echo "=== Deployment Monitoring Dashboard ==="
  echo "Timestamp: $(date)"
  echo ""
  
  # Get metrics from Green environment
  green_metrics=$(curl -s "$GREEN_URL/api/admin/metrics")
  
  # Parse metrics
  error_rate=$(echo "$green_metrics" | jq -r '.errorRate')
  response_time_p95=$(echo "$green_metrics" | jq -r '.responseTimeP95')
  request_rate=$(echo "$green_metrics" | jq -r '.requestRate')
  db_query_time_p95=$(echo "$green_metrics" | jq -r '.dbQueryTimeP95')
  connection_pool_usage=$(echo "$green_metrics" | jq -r '.connectionPoolUsage')
  
  # Display metrics
  echo "📊 Application Metrics"
  echo "  Error Rate:           $error_rate% $(check_threshold $error_rate 0.1 0.5)"
  echo "  Response Time (p95):  ${response_time_p95}ms $(check_threshold $response_time_p95 500 1000)"
  echo "  Request Rate:         ${request_rate} req/s"
  echo ""
  
  echo "🗄️  Database Metrics"
  echo "  Query Time (p95):     ${db_query_time_p95}ms $(check_threshold $db_query_time_p95 200 500)"
  echo "  Connection Pool:      ${connection_pool_usage}% $(check_threshold $connection_pool_usage 80 95)"
  echo ""
  
  # Get repository status
  repo_status=$(curl -s "$GREEN_URL/health/repositories")
  efcore_count=$(echo "$repo_status" | jq '[.repositories[] | select(.provider=="EfCore")] | length')
  adonet_count=$(echo "$repo_status" | jq '[.repositories[] | select(.provider=="AdoNet")] | length')
  
  echo "🔄 Repository Status"
  echo "  EF Core:              $efcore_count repositories"
  echo "  ADO.NET:              $adonet_count repositories"
  echo ""
  
  # Check for errors in logs
  recent_errors=$(curl -s "$GREEN_URL/api/admin/recent-errors?minutes=5" | jq -r '.count')
  echo "⚠️  Recent Errors (5 min): $recent_errors"
  
  if [ "$recent_errors" -gt 0 ]; then
    echo ""
    echo "Recent error details:"
    curl -s "$GREEN_URL/api/admin/recent-errors?minutes=5" | jq -r '.errors[] | "  - \(.message)"'
  fi
  
  echo ""
  echo "Press Ctrl+C to stop monitoring"
  
  sleep $INTERVAL
done

# Helper function to check thresholds
check_threshold() {
  local value=$1
  local warning=$2
  local critical=$3
  
  if (( $(echo "$value > $critical" | bc -l) )); then
    echo "🔴 CRITICAL"
  elif (( $(echo "$value > $warning" | bc -l) )); then
    echo "🟡 WARNING"
  else
    echo "🟢 OK"
  fi
}
```

### 7.4 Alert Configuration

**Email Alerts:**
```yaml
# alerts.yaml
alerts:
  - name: "High Error Rate"
    condition: "error_rate > 0.5%"
    severity: "critical"
    notification:
      - email: "devops@thinkonerp.com"
      - slack: "#deployment-alerts"
    action: "Automatic rollback triggered"
  
  - name: "Slow Response Time"
    condition: "response_time_p95 > 1000ms"
    severity: "critical"
    notification:
      - email: "devops@thinkonerp.com"
    action: "Manual investigation required"
  
  - name: "Database Connection Issues"
    condition: "failed_connections > 5 per minute"
    severity: "critical"
    notification:
      - email: "dba@thinkonerp.com"
      - sms: "+1234567890"
    action: "Automatic rollback triggered"
```

**Slack Integration:**
```bash
# Send alert to Slack
send_slack_alert() {
  local message=$1
  local severity=$2
  local color="danger"
  
  if [ "$severity" == "warning" ]; then
    color="warning"
  fi
  
  curl -X POST https://hooks.slack.com/services/YOUR/WEBHOOK/URL \
    -H 'Content-Type: application/json' \
    -d "{
      \"attachments\": [{
        \"color\": \"$color\",
        \"title\": \"EF Core Deployment Alert\",
        \"text\": \"$message\",
        \"footer\": \"ThinkOnErp Deployment\",
        \"ts\": $(date +%s)
      }]
    }"
}
```

### 7.5 Monitoring Checklist

During each deployment phase, verify:

- [ ] Error rate < 0.1%
- [ ] Response time p95 < 500ms
- [ ] Database query time p95 < 200ms
- [ ] Connection pool usage < 80%
- [ ] No failed database connections
- [ ] No exceptions in logs (last 5 minutes)
- [ ] Request rate matches expected traffic split
- [ ] Memory usage stable (no leaks)
- [ ] CPU usage < 80%
- [ ] All enabled repositories responding

## 8. Rollback Procedures

### 8.1 Rollback Overview

The rollback strategy is designed to revert to the ADO.NET (Blue) environment within **5 minutes** if issues occur during deployment. Rollback is achieved through feature flag disabling and load balancer traffic shifting, requiring no code deployment.

**Rollback Triggers:**
- Error rate exceeds 0.5%
- Response time p95 exceeds 1000ms
- Database connection failures exceed 5 per minute
- Memory usage exceeds 95%
- Critical application errors
- Manual decision by deployment lead

**Rollback Time Objective (RTO)**: 5 minutes  
**Rollback Point Objective (RPO)**: 0 (no data loss - shared database)

---

### 8.2 Emergency Rollback Procedure (< 5 minutes)

Use this procedure when immediate rollback is required due to critical issues.

#### Step 1: Disable All EF Core Feature Flags (1 minute)

```bash
#!/bin/bash
# File: scripts/emergency-rollback.sh
# Purpose: Immediate rollback to ADO.NET

set -e

echo "=== EMERGENCY ROLLBACK INITIATED ==="
echo "Timestamp: $(date)"
echo "Initiated by: $USER"
echo ""

# Disable all EF Core repository flags
echo "Step 1: Disabling all EF Core feature flags..."

for repo in CompanyRepository BranchRepository UserRepository RoleRepository \
            PermissionRepository CurrencyRepository FiscalYearRepository \
            SuperAdminRepository AuthRepository AuditRepository \
            ScreenRepository SystemRepository CompanySystemRepository \
            TicketRepository TicketTypeRepository TicketStatusRepository \
            TicketPriorityRepository TicketCommentRepository TicketAttachmentRepository \
            TicketConfigRepository TicketCategoryRepository \
            AlertRepository SavedSearchRepository SearchAnalyticsRepository; do
  
  az appconfig kv set \
    --name thinkonerp-appconfig \
    --key "EfCoreRepositories:$repo" \
    --value "false" \
    --yes \
    2>&1 | grep -v "WARNING" || true
done

echo "✅ All feature flags disabled"
echo ""

# Verify flags are disabled
echo "Step 2: Verifying feature flags..."
disabled_count=$(az appconfig kv list \
  --name thinkonerp-appconfig \
  --key "EfCoreRepositories:*" \
  --query "[?value=='false'] | length(@)")

echo "✅ $disabled_count repositories reverted to ADO.NET"
echo ""

# Shift all traffic to Blue
echo "Step 3: Shifting 100% traffic to Blue environment..."
sudo sed -i 's/server app-server-03:5000 weight=[0-9]*/server app-server-03:5000 weight=0/' /etc/nginx/conf.d/thinkonerp-upstream.conf
sudo sed -i 's/server app-server-04:5000 weight=[0-9]*/server app-server-04:5000 weight=0/' /etc/nginx/conf.d/thinkonerp-upstream.conf
sudo sed -i 's/server app-server-01:5000 weight=[0-9]*/server app-server-01:5000 weight=50/' /etc/nginx/conf.d/thinkonerp-upstream.conf
sudo sed -i 's/server app-server-02:5000 weight=[0-9]*/server app-server-02:5000 weight=50/' /etc/nginx/conf.d/thinkonerp-upstream.conf

sudo nginx -t && sudo nginx -s reload

echo "✅ Traffic shifted to Blue environment"
echo ""

# Verify rollback
echo "Step 4: Verifying rollback..."
sleep 5

health_check=$(curl -s https://thinkonerp.com/health | jq -r '.environment')
if [[ "$health_check" == *"Blue"* ]]; then
  echo "✅ Rollback successful - Blue environment active"
else
  echo "⚠️  Warning: Health check shows: $health_check"
fi

echo ""
echo "=== ROLLBACK COMPLETE ==="
echo "Duration: $SECONDS seconds"
echo ""
echo "Next steps:"
echo "1. Verify application is functioning normally"
echo "2. Investigate root cause of issues"
echo "3. Review logs from Green environment"
echo "4. Document incident and lessons learned"
```

**Execute Emergency Rollback:**
```bash
# Run the emergency rollback script
./scripts/emergency-rollback.sh

# Expected output:
# === EMERGENCY ROLLBACK INITIATED ===
# Timestamp: 2024-01-15 14:30:00
# ...
# === ROLLBACK COMPLETE ===
# Duration: 180 seconds
```

---

### 8.3 Graceful Rollback Procedure (10-15 minutes)

Use this procedure when issues are detected but not critical, allowing for graceful rollback with monitoring.

#### Step 1: Stop Traffic Shifting (2 minutes)

```bash
# Pause any automated traffic shifting
./scripts/pause-traffic-shift.sh

# Verify current traffic distribution
curl -s https://thinkonerp.com/api/admin/traffic-distribution
```

#### Step 2: Gradually Shift Traffic Back to Blue (5 minutes)

```bash
# Shift traffic in stages to avoid sudden load changes

# Stage 1: Reduce Green to 30%
./scripts/shift-traffic.sh --blue 70 --green 30
sleep 60

# Verify error rate hasn't increased
./scripts/check-metrics.sh --duration 1m

# Stage 2: Reduce Green to 10%
./scripts/shift-traffic.sh --blue 90 --green 10
sleep 60

# Stage 3: Reduce Green to 0%
./scripts/shift-traffic.sh --blue 100 --green 0
```

#### Step 3: Disable EF Core Feature Flags (3 minutes)

```bash
# Disable feature flags for problematic repositories first
./scripts/disable-feature-flags.sh --repositories "UserRepository,RoleRepository"

# Wait and monitor
sleep 60

# Disable remaining feature flags
./scripts/disable-feature-flags.sh --all
```

#### Step 4: Verify Rollback (2 minutes)

```bash
# Run health checks
./scripts/verify-health-checks.sh

# Check error rates
./scripts/check-metrics.sh --duration 5m

# Verify all traffic on Blue
curl -s https://thinkonerp.com/api/admin/server-info | jq .serverName
# Should only show app-server-01 and app-server-02
```

#### Step 5: Document Rollback (3 minutes)

```bash
# Generate rollback report
./scripts/generate-rollback-report.sh \
  --reason "High error rate detected" \
  --metrics-before "error_rate=0.8%" \
  --metrics-after "error_rate=0.05%" \
  --duration "12 minutes"

# Send notification
./scripts/send-rollback-notification.sh \
  --severity "warning" \
  --message "Deployment rolled back due to high error rate"
```

---

### 8.4 Partial Rollback Procedure

Use this procedure to rollback specific repositories while keeping others on EF Core.

#### Scenario: Rollback User Authentication Repositories

```bash
# Disable specific repositories
az appconfig kv set \
  --name thinkonerp-appconfig \
  --key "EfCoreRepositories:UserRepository" \
  --value "false" \
  --yes

az appconfig kv set \
  --name thinkonerp-appconfig \
  --key "EfCoreRepositories:RoleRepository" \
  --value "false" \
  --yes

az appconfig kv set \
  --name thinkonerp-appconfig \
  --key "EfCoreRepositories:PermissionRepository" \
  --value "false" \
  --yes

# Verify other repositories remain on EF Core
az appconfig kv list \
  --name thinkonerp-appconfig \
  --key "EfCoreRepositories:*" \
  --query "[?value=='true']"

# Monitor for 5 minutes
./scripts/monitor-metrics.sh --duration 5m --repositories User,Role,Permission
```

---

### 8.5 Rollback Verification Checklist

After executing rollback, verify:

- [ ] All traffic routed to Blue environment (ADO.NET)
- [ ] Feature flags disabled for affected repositories
- [ ] Error rate returned to baseline (< 0.1%)
- [ ] Response time returned to baseline (< 500ms p95)
- [ ] No database connection errors
- [ ] Application logs show ADO.NET provider in use
- [ ] Health checks passing
- [ ] User-facing functionality working correctly
- [ ] Rollback documented in incident log
- [ ] Stakeholders notified

---

### 8.6 Post-Rollback Actions

#### Immediate Actions (Within 1 hour)

1. **Verify System Stability**
   ```bash
   # Monitor for 30 minutes
   ./scripts/monitor-metrics.sh --duration 30m
   
   # Check for any lingering issues
   ./scripts/check-system-health.sh
   ```

2. **Preserve Green Environment Logs**
   ```bash
   # Archive logs for investigation
   ssh app-server-03 "tar -czf /tmp/green-logs-$(date +%Y%m%d-%H%M%S).tar.gz /var/log/thinkonerp/green/"
   scp app-server-03:/tmp/green-logs-*.tar.gz ./incident-logs/
   
   # Archive Application Insights data
   ./scripts/export-app-insights-logs.sh \
     --start "$(date -d '2 hours ago' --iso-8601=seconds)" \
     --end "$(date --iso-8601=seconds)" \
     --output "./incident-logs/app-insights-$(date +%Y%m%d-%H%M%S).json"
   ```

3. **Notify Stakeholders**
   ```bash
   # Send rollback notification
   ./scripts/send-notification.sh \
     --type "rollback-complete" \
     --recipients "devops@thinkonerp.com,management@thinkonerp.com" \
     --message "EF Core deployment rolled back. System stable on ADO.NET."
   ```

#### Investigation Actions (Within 24 hours)

1. **Root Cause Analysis**
   - Review error logs from Green environment
   - Analyze performance metrics
   - Identify specific queries or operations that failed
   - Review database execution plans
   - Check for resource constraints (CPU, memory, connections)

2. **Create Incident Report**
   ```markdown
   # Incident Report: EF Core Deployment Rollback
   
   **Date**: 2024-01-15
   **Duration**: 14:30 - 14:42 (12 minutes)
   **Severity**: High
   **Impact**: None (rollback successful)
   
   ## Summary
   Deployment of EF Core migration was rolled back due to [reason].
   
   ## Timeline
   - 14:30: Issue detected (error rate 0.8%)
   - 14:32: Rollback initiated
   - 14:35: Feature flags disabled
   - 14:38: Traffic shifted to Blue
   - 14:42: Rollback verified
   
   ## Root Cause
   [Detailed analysis]
   
   ## Resolution
   [Steps taken to resolve]
   
   ## Lessons Learned
   [What we learned]
   
   ## Action Items
   - [ ] Fix identified issues
   - [ ] Add additional tests
   - [ ] Update deployment procedure
   - [ ] Schedule retry deployment
   ```

3. **Fix and Retest**
   - Address root cause in development environment
   - Add tests to prevent regression
   - Verify fix in staging environment
   - Update deployment plan if needed

#### Planning Actions (Within 1 week)

1. **Schedule Retry Deployment**
   - Review lessons learned
   - Update deployment plan
   - Schedule new deployment window
   - Brief team on changes

2. **Update Procedures**
   - Document any gaps in rollback procedure
   - Update monitoring thresholds if needed
   - Enhance automated rollback triggers
   - Improve health checks

---

### 8.7 Rollback Decision Matrix

Use this matrix to determine when to rollback:

| Metric | Current Value | Threshold | Action | Rollback Type |
|--------|---------------|-----------|--------|---------------|
| Error Rate | 0.2% | < 0.5% | Monitor | None |
| Error Rate | 0.6% | > 0.5% | Rollback | Emergency |
| Response Time p95 | 600ms | < 1000ms | Investigate | None |
| Response Time p95 | 1200ms | > 1000ms | Rollback | Graceful |
| DB Connection Failures | 3/min | < 5/min | Monitor | None |
| DB Connection Failures | 8/min | > 5/min | Rollback | Emergency |
| Memory Usage | 88% | < 95% | Monitor | None |
| Memory Usage | 97% | > 95% | Rollback | Emergency |
| User Complaints | 2 | < 5 | Investigate | None |
| User Complaints | 10 | > 5 | Rollback | Graceful |
| Critical Bug | None | Any | Rollback | Emergency |

**Decision Guidelines:**
- **Monitor**: Continue deployment, watch metrics closely
- **Investigate**: Pause traffic shifting, investigate issue
- **Graceful Rollback**: Gradually shift traffic back, disable feature flags
- **Emergency Rollback**: Immediate rollback, all traffic to Blue

---

### 8.8 Rollback Communication Template

**Email Template:**
```
Subject: [ACTION REQUIRED] EF Core Deployment Rollback - System Stable

Team,

The EF Core migration deployment has been rolled back to the ADO.NET (Blue) environment.

Status: System is stable and operating normally
Rollback Time: [timestamp]
Duration: [X] minutes
Impact: No user impact

Reason for Rollback:
[Brief description of issue]

Current State:
- All traffic routed to Blue environment (ADO.NET)
- All EF Core feature flags disabled
- System metrics returned to baseline
- No ongoing issues

Next Steps:
1. Root cause analysis in progress
2. Incident report will be published within 24 hours
3. Retry deployment will be scheduled after fixes are verified

If you have any questions or concerns, please contact:
- Deployment Lead: [name] ([email])
- DevOps Team: devops@thinkonerp.com

Thank you,
ThinkOnErp DevOps Team
```

**Slack Message Template:**
```
🔄 **EF Core Deployment Rollback**

Status: ✅ System Stable
Environment: Blue (ADO.NET)
Rollback Duration: [X] minutes

Reason: [Brief description]

All systems operating normally. Investigation in progress.

Contact: @devops-team for questions
```
## 9. Automated Rollback Triggers

### 9.1 Automated Rollback Overview

Automated rollback triggers monitor key metrics and automatically initiate rollback when critical thresholds are exceeded. This ensures rapid response to issues without requiring manual intervention.

**Benefits:**
- Reduces rollback time from 5 minutes to < 2 minutes
- Eliminates human decision delay
- Provides consistent rollback criteria
- Operates 24/7 without manual monitoring

**Architecture:**
```
┌─────────────────────────────────────────────────────────────┐
│                  Monitoring System                           │
│         (Application Insights / Prometheus)                  │
└─────────────────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────┐
│                  Alert Rules Engine                          │
│  - Error rate threshold                                      │
│  - Response time threshold                                   │
│  - Connection failure threshold                              │
│  - Memory usage threshold                                    │
└─────────────────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────┐
│              Automated Rollback Service                      │
│  - Validates alert conditions                                │
│  - Executes emergency rollback script                        │
│  - Sends notifications                                       │
│  - Logs rollback event                                       │
└─────────────────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────┐
│                  Rollback Actions                            │
│  - Disable feature flags                                     │
│  - Shift traffic to Blue                                     │
│  - Verify rollback success                                   │
└─────────────────────────────────────────────────────────────┘
```

---

### 9.2 Rollback Trigger Thresholds

#### Critical Thresholds (Immediate Rollback)

| Metric | Threshold | Window | Action |
|--------|-----------|--------|--------|
| Error Rate | > 0.5% | 5 minutes | Immediate rollback |
| Response Time p95 | > 1000ms | 5 minutes | Immediate rollback |
| Database Connection Failures | > 5 per minute | 2 minutes | Immediate rollback |
| Memory Usage | > 95% | 3 minutes | Immediate rollback |
| CPU Usage | > 95% | 5 minutes | Immediate rollback |
| Unhandled Exceptions | > 10 per minute | 2 minutes | Immediate rollback |
| Database Deadlocks | > 5 per minute | 2 minutes | Immediate rollback |
| Connection Pool Exhaustion | > 98% | 1 minute | Immediate rollback |

#### Warning Thresholds (Alert Only)

| Metric | Threshold | Window | Action |
|--------|-----------|--------|--------|
| Error Rate | > 0.1% | 5 minutes | Alert DevOps team |
| Response Time p95 | > 500ms | 5 minutes | Alert DevOps team |
| Database Query Time p95 | > 200ms | 5 minutes | Alert DBA team |
| Connection Pool Usage | > 80% | 5 minutes | Alert DevOps team |
| Memory Usage | > 85% | 5 minutes | Alert DevOps team |

---

### 9.3 Azure Application Insights Alert Rules

#### Alert Rule 1: High Error Rate

```powershell
# Create alert rule for high error rate
$actionGroup = Get-AzActionGroup -ResourceGroupName "thinkonerp-rg" -Name "deployment-alerts"

$condition = New-AzMetricAlertRuleV2Criteria `
  -MetricName "requests/failed" `
  -TimeAggregation Average `
  -Operator GreaterThan `
  -Threshold 0.5 `
  -MetricNamespace "microsoft.insights/components"

Add-AzMetricAlertRuleV2 `
  -Name "efcore-high-error-rate" `
  -ResourceGroupName "thinkonerp-rg" `
  -WindowSize 00:05:00 `
  -Frequency 00:01:00 `
  -TargetResourceId "/subscriptions/{subscription-id}/resourceGroups/thinkonerp-rg/providers/microsoft.insights/components/thinkonerp-appinsights" `
  -Condition $condition `
  -ActionGroup $actionGroup `
  -Severity 0 `
  -Description "Triggers automated rollback when error rate exceeds 0.5%"
```

#### Alert Rule 2: Slow Response Time

```powershell
$condition = New-AzMetricAlertRuleV2Criteria `
  -MetricName "requests/duration" `
  -TimeAggregation Percentile95 `
  -Operator GreaterThan `
  -Threshold 1000 `
  -MetricNamespace "microsoft.insights/components"

Add-AzMetricAlertRuleV2 `
  -Name "efcore-slow-response-time" `
  -ResourceGroupName "thinkonerp-rg" `
  -WindowSize 00:05:00 `
  -Frequency 00:01:00 `
  -TargetResourceId "/subscriptions/{subscription-id}/resourceGroups/thinkonerp-rg/providers/microsoft.insights/components/thinkonerp-appinsights" `
  -Condition $condition `
  -ActionGroup $actionGroup `
  -Severity 0 `
  -Description "Triggers automated rollback when p95 response time exceeds 1000ms"
```

#### Alert Rule 3: Database Connection Failures

```powershell
$condition = New-AzMetricAlertRuleV2Criteria `
  -MetricName "dependencies/failed" `
  -DimensionName "dependency/type" `
  -DimensionOperator Include `
  -DimensionValues @("SQL") `
  -TimeAggregation Count `
  -Operator GreaterThan `
  -Threshold 10 `
  -MetricNamespace "microsoft.insights/components"

Add-AzMetricAlertRuleV2 `
  -Name "efcore-db-connection-failures" `
  -ResourceGroupName "thinkonerp-rg" `
  -WindowSize 00:02:00 `
  -Frequency 00:01:00 `
  -TargetResourceId "/subscriptions/{subscription-id}/resourceGroups/thinkonerp-rg/providers/microsoft.insights/components/thinkonerp-appinsights" `
  -Condition $condition `
  -ActionGroup $actionGroup `
  -Severity 0 `
  -Description "Triggers automated rollback when database connection failures exceed 5 per minute"
```

#### Alert Rule 4: High Memory Usage

```powershell
$condition = New-AzMetricAlertRuleV2Criteria `
  -MetricName "performanceCounters/memoryAvailableBytes" `
  -TimeAggregation Average `
  -Operator LessThan `
  -Threshold 524288000 `
  -MetricNamespace "microsoft.insights/components"

Add-AzMetricAlertRuleV2 `
  -Name "efcore-high-memory-usage" `
  -ResourceGroupName "thinkonerp-rg" `
  -WindowSize 00:03:00 `
  -Frequency 00:01:00 `
  -TargetResourceId "/subscriptions/{subscription-id}/resourceGroups/thinkonerp-rg/providers/microsoft.insights/components/thinkonerp-appinsights" `
  -Condition $condition `
  -ActionGroup $actionGroup `
  -Severity 0 `
  -Description "Triggers automated rollback when available memory < 500MB (95% usage)"
```

---

### 9.4 Prometheus Alert Rules

For environments using Prometheus/Grafana:

```yaml
# File: prometheus-alerts.yml

groups:
  - name: efcore_deployment_alerts
    interval: 30s
    rules:
      # High Error Rate Alert
      - alert: EfCoreHighErrorRate
        expr: |
          (
            rate(http_requests_total{job="thinkonerp-green",status=~"5.."}[5m])
            /
            rate(http_requests_total{job="thinkonerp-green"}[5m])
          ) > 0.005
        for: 2m
        labels:
          severity: critical
          component: efcore-deployment
          action: automated-rollback
        annotations:
          summary: "EF Core error rate exceeds 0.5%"
          description: "Error rate is {{ $value | humanizePercentage }} (threshold: 0.5%)"
          runbook: "https://wiki.thinkonerp.com/runbooks/efcore-rollback"

      # Slow Response Time Alert
      - alert: EfCoreSlowResponseTime
        expr: |
          histogram_quantile(0.95,
            rate(http_request_duration_seconds_bucket{job="thinkonerp-green"}[5m])
          ) > 1.0
        for: 2m
        labels:
          severity: critical
          component: efcore-deployment
          action: automated-rollback
        annotations:
          summary: "EF Core p95 response time exceeds 1000ms"
          description: "p95 response time is {{ $value | humanizeDuration }} (threshold: 1000ms)"

      # Database Connection Failures
      - alert: EfCoreDbConnectionFailures
        expr: |
          rate(db_connection_errors_total{job="thinkonerp-green"}[2m]) > 0.083
        for: 1m
        labels:
          severity: critical
          component: efcore-deployment
          action: automated-rollback
        annotations:
          summary: "EF Core database connection failures exceed threshold"
          description: "Connection failures: {{ $value | humanize }} per second (threshold: 5/min)"

      # High Memory Usage
      - alert: EfCoreHighMemoryUsage
        expr: |
          (
            process_resident_memory_bytes{job="thinkonerp-green"}
            /
            node_memory_MemTotal_bytes
          ) > 0.95
        for: 3m
        labels:
          severity: critical
          component: efcore-deployment
          action: automated-rollback
        annotations:
          summary: "EF Core memory usage exceeds 95%"
          description: "Memory usage is {{ $value | humanizePercentage }} (threshold: 95%)"

      # Connection Pool Exhaustion
      - alert: EfCoreConnectionPoolExhaustion
        expr: |
          (
            db_connection_pool_active{job="thinkonerp-green"}
            /
            db_connection_pool_max{job="thinkonerp-green"}
          ) > 0.98
        for: 1m
        labels:
          severity: critical
          component: efcore-deployment
          action: automated-rollback
        annotations:
          summary: "EF Core connection pool exhausted"
          description: "Connection pool usage is {{ $value | humanizePercentage }} (threshold: 98%)"

      # Warning Alerts (No Automated Rollback)
      - alert: EfCoreWarningErrorRate
        expr: |
          (
            rate(http_requests_total{job="thinkonerp-green",status=~"5.."}[5m])
            /
            rate(http_requests_total{job="thinkonerp-green"}[5m])
          ) > 0.001
        for: 5m
        labels:
          severity: warning
          component: efcore-deployment
          action: alert-only
        annotations:
          summary: "EF Core error rate elevated"
          description: "Error rate is {{ $value | humanizePercentage }} (warning threshold: 0.1%)"
```

---

### 9.5 Automated Rollback Service Implementation

#### Service Architecture

```csharp
// File: AutomatedRollbackService.cs
// Purpose: Listens for alerts and executes automated rollback

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace ThinkOnErp.Deployment.Services
{
    public class AutomatedRollbackService : BackgroundService
    {
        private readonly ILogger<AutomatedRollbackService> _logger;
        private readonly IAlertMonitor _alertMonitor;
        private readonly IRollbackExecutor _rollbackExecutor;
        private readonly INotificationService _notificationService;
        private bool _rollbackInProgress = false;

        public AutomatedRollbackService(
            ILogger<AutomatedRollbackService> logger,
            IAlertMonitor alertMonitor,
            IRollbackExecutor rollbackExecutor,
            INotificationService notificationService)
        {
            _logger = logger;
            _alertMonitor = alertMonitor;
            _rollbackExecutor = rollbackExecutor;
            _notificationService = notificationService;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Automated Rollback Service started");

            await foreach (var alert in _alertMonitor.GetAlertsAsync(stoppingToken))
            {
                if (alert.Action == "automated-rollback" && !_rollbackInProgress)
                {
                    await HandleRollbackAlert(alert);
                }
            }
        }

        private async Task HandleRollbackAlert(Alert alert)
        {
            _rollbackInProgress = true;
            var stopwatch = Stopwatch.StartNew();

            try
            {
                _logger.LogCritical(
                    "Automated rollback triggered: {AlertName} - {Description}",
                    alert.Name,
                    alert.Description);

                // Send immediate notification
                await _notificationService.SendCriticalAlertAsync(
                    "Automated Rollback Initiated",
                    $"Alert: {alert.Name}\nReason: {alert.Description}\nTimestamp: {DateTime.UtcNow}");

                // Validate alert condition (prevent false positives)
                if (!await ValidateAlertCondition(alert))
                {
                    _logger.LogWarning("Alert condition no longer valid, rollback cancelled");
                    return;
                }

                // Execute rollback
                var result = await _rollbackExecutor.ExecuteEmergencyRollbackAsync();

                stopwatch.Stop();

                if (result.Success)
                {
                    _logger.LogInformation(
                        "Automated rollback completed successfully in {Duration}ms",
                        stopwatch.ElapsedMilliseconds);

                    await _notificationService.SendSuccessNotificationAsync(
                        "Automated Rollback Complete",
                        $"Rollback completed in {stopwatch.Elapsed.TotalSeconds:F1} seconds\n" +
                        $"System reverted to Blue environment (ADO.NET)\n" +
                        $"Trigger: {alert.Name}");
                }
                else
                {
                    _logger.LogError(
                        "Automated rollback failed: {Error}",
                        result.Error);

                    await _notificationService.SendCriticalAlertAsync(
                        "Automated Rollback FAILED",
                        $"Rollback failed after {stopwatch.Elapsed.TotalSeconds:F1} seconds\n" +
                        $"Error: {result.Error}\n" +
                        $"MANUAL INTERVENTION REQUIRED");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception during automated rollback");
                
                await _notificationService.SendCriticalAlertAsync(
                    "Automated Rollback Exception",
                    $"Exception: {ex.Message}\nMANUAL INTERVENTION REQUIRED");
            }
            finally
            {
                _rollbackInProgress = false;
            }
        }

        private async Task<bool> ValidateAlertCondition(Alert alert)
        {
            // Re-check the metric to prevent false positives
            // Wait 30 seconds and check again
            await Task.Delay(TimeSpan.FromSeconds(30));

            var currentMetric = await _alertMonitor.GetCurrentMetricValueAsync(alert.MetricName);
            
            return currentMetric > alert.Threshold;
        }
    }
}
```

#### Rollback Executor Implementation

```csharp
// File: RollbackExecutor.cs

using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace ThinkOnErp.Deployment.Services
{
    public interface IRollbackExecutor
    {
        Task<RollbackResult> ExecuteEmergencyRollbackAsync();
    }

    public class RollbackExecutor : IRollbackExecutor
    {
        private readonly ILogger<RollbackExecutor> _logger;
        private readonly IFeatureFlagService _featureFlagService;
        private readonly ILoadBalancerService _loadBalancerService;

        public RollbackExecutor(
            ILogger<RollbackExecutor> logger,
            IFeatureFlagService featureFlagService,
            ILoadBalancerService loadBalancerService)
        {
            _logger = logger;
            _featureFlagService = featureFlagService;
            _loadBalancerService = loadBalancerService;
        }

        public async Task<RollbackResult> ExecuteEmergencyRollbackAsync()
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                // Step 1: Disable all EF Core feature flags
                _logger.LogInformation("Step 1: Disabling all EF Core feature flags");
                await _featureFlagService.DisableAllEfCoreRepositoriesAsync();

                // Step 2: Shift all traffic to Blue environment
                _logger.LogInformation("Step 2: Shifting traffic to Blue environment");
                await _loadBalancerService.SetTrafficDistributionAsync(
                    blueWeight: 100,
                    greenWeight: 0);

                // Step 3: Verify rollback
                _logger.LogInformation("Step 3: Verifying rollback");
                await Task.Delay(TimeSpan.FromSeconds(5)); // Allow time for changes to propagate

                var healthCheck = await VerifyRollbackAsync();
                
                if (!healthCheck.Success)
                {
                    return new RollbackResult
                    {
                        Success = false,
                        Error = $"Rollback verification failed: {healthCheck.Error}",
                        Duration = stopwatch.Elapsed
                    };
                }

                stopwatch.Stop();

                return new RollbackResult
                {
                    Success = true,
                    Duration = stopwatch.Elapsed,
                    Message = "Emergency rollback completed successfully"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during rollback execution");
                
                return new RollbackResult
                {
                    Success = false,
                    Error = ex.Message,
                    Duration = stopwatch.Elapsed
                };
            }
        }

        private async Task<HealthCheckResult> VerifyRollbackAsync()
        {
            try
            {
                // Verify Blue environment is receiving traffic
                var trafficDistribution = await _loadBalancerService.GetTrafficDistributionAsync();
                
                if (trafficDistribution.GreenPercentage > 0)
                {
                    return new HealthCheckResult
                    {
                        Success = false,
                        Error = $"Green environment still receiving {trafficDistribution.GreenPercentage}% traffic"
                    };
                }

                // Verify feature flags are disabled
                var enabledFlags = await _featureFlagService.GetEnabledRepositoriesAsync();
                
                if (enabledFlags.Count > 0)
                {
                    return new HealthCheckResult
                    {
                        Success = false,
                        Error = $"{enabledFlags.Count} feature flags still enabled"
                    };
                }

                return new HealthCheckResult { Success = true };
            }
            catch (Exception ex)
            {
                return new HealthCheckResult
                {
                    Success = false,
                    Error = ex.Message
                };
            }
        }
    }

    public class RollbackResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string Error { get; set; }
        public TimeSpan Duration { get; set; }
    }

    public class HealthCheckResult
    {
        public bool Success { get; set; }
        public string Error { get; set; }
    }
}
```

---

### 9.6 Webhook Integration for Alerts

#### Azure Logic App for Automated Rollback

```json
{
  "definition": {
    "$schema": "https://schema.management.azure.com/providers/Microsoft.Logic/schemas/2016-06-01/workflowdefinition.json#",
    "triggers": {
      "When_a_metric_alert_is_triggered": {
        "type": "ApiConnection",
        "inputs": {
          "host": {
            "connection": {
              "name": "@parameters('$connections')['azuremonitorlogs']['connectionId']"
            }
          },
          "method": "post",
          "path": "/subscriptions/@{encodeURIComponent('subscription-id')}/providers/Microsoft.Insights/metricAlerts"
        }
      }
    },
    "actions": {
      "Check_if_automated_rollback_required": {
        "type": "If",
        "expression": {
          "and": [
            {
              "equals": [
                "@triggerBody()?['data']?['context']?['condition']?['allOf']?[0]?['metricName']",
                "requests/failed"
              ]
            },
            {
              "greater": [
                "@triggerBody()?['data']?['context']?['condition']?['allOf']?[0]?['metricValue']",
                0.5
              ]
            }
          ]
        },
        "actions": {
          "Execute_rollback_script": {
            "type": "Http",
            "inputs": {
              "method": "POST",
              "uri": "https://deployment-api.thinkonerp.com/api/rollback/execute",
              "headers": {
                "Authorization": "Bearer @{parameters('apiKey')}",
                "Content-Type": "application/json"
              },
              "body": {
                "reason": "@triggerBody()?['data']?['context']?['condition']?['allOf']?[0]?['metricName']",
                "metricValue": "@triggerBody()?['data']?['context']?['condition']?['allOf']?[0]?['metricValue']",
                "timestamp": "@utcNow()",
                "automated": true
              }
            }
          },
          "Send_notification": {
            "type": "ApiConnection",
            "inputs": {
              "host": {
                "connection": {
                  "name": "@parameters('$connections')['office365']['connectionId']"
                }
              },
              "method": "post",
              "path": "/v2/Mail",
              "body": {
                "To": "devops@thinkonerp.com",
                "Subject": "[CRITICAL] Automated Rollback Triggered",
                "Body": "Automated rollback has been triggered due to: @{triggerBody()?['data']?['context']?['condition']?['allOf']?[0]?['metricName']}\n\nMetric Value: @{triggerBody()?['data']?['context']?['condition']?['allOf']?[0]?['metricValue']}\n\nTimestamp: @{utcNow()}"
              }
            }
          }
        }
      }
    }
  }
}
```

---

### 9.7 Testing Automated Rollback

#### Test Procedure

```bash
#!/bin/bash
# File: scripts/test-automated-rollback.sh
# Purpose: Test automated rollback triggers without affecting production

echo "=== Testing Automated Rollback System ==="
echo ""

# Test 1: Verify alert rules are configured
echo "Test 1: Verifying alert rules..."
az monitor metrics alert list \
  --resource-group thinkonerp-rg \
  --query "[?contains(name, 'efcore')].{Name:name, Enabled:enabled, Severity:severity}" \
  --output table

# Test 2: Simulate high error rate (in test environment)
echo ""
echo "Test 2: Simulating high error rate..."
./scripts/simulate-errors.sh --rate 0.6 --duration 5m --environment test

# Test 3: Verify rollback service responds
echo ""
echo "Test 3: Monitoring rollback service response..."
timeout 300 ./scripts/monitor-rollback-service.sh

# Test 4: Verify rollback execution
echo ""
echo "Test 4: Verifying rollback was executed..."
./scripts/verify-rollback-execution.sh --environment test

echo ""
echo "=== Automated Rollback Test Complete ==="
```

#### Rollback Simulation (Non-Production)

```bash
# Trigger a test alert
az monitor metrics alert update \
  --name "efcore-high-error-rate" \
  --resource-group thinkonerp-test-rg \
  --enabled true

# Inject test metric data
./scripts/inject-test-metrics.sh \
  --metric "requests/failed" \
  --value 0.6 \
  --duration 5m

# Monitor for automated rollback
watch -n 5 'curl -s https://test.thinkonerp.com/api/admin/deployment-status'
```

---

### 9.8 Automated Rollback Monitoring Dashboard

Create a dedicated dashboard to monitor automated rollback system:

**Key Metrics to Display:**
- Alert rule status (enabled/disabled)
- Current metric values vs thresholds
- Rollback service health
- Recent rollback events
- Rollback success rate
- Average rollback duration

**Dashboard URL**: `https://portal.azure.com/#@thinkonerp/dashboard/automated-rollback`

---

### 9.9 Disabling Automated Rollback

In some scenarios, you may want to temporarily disable automated rollback:

```bash
# Disable all automated rollback alerts
az monitor metrics alert update \
  --name "efcore-high-error-rate" \
  --resource-group thinkonerp-rg \
  --enabled false

az monitor metrics alert update \
  --name "efcore-slow-response-time" \
  --resource-group thinkonerp-rg \
  --enabled false

# Or disable via configuration
az appconfig kv set \
  --name thinkonerp-appconfig \
  --key "AutomatedRollback:Enabled" \
  --value "false" \
  --yes

# Re-enable after maintenance
az appconfig kv set \
  --name thinkonerp-appconfig \
  --key "AutomatedRollback:Enabled" \
  --value "true" \
  --yes
```

**When to Disable:**
- During planned maintenance
- During load testing
- When investigating specific issues
- When manual control is required

**Important**: Always re-enable automated rollback after maintenance is complete.
## 10. Connection Pool Warm-Up Procedure

### 10.1 Connection Pool Warm-Up Overview

Connection pool warm-up is critical to prevent cold start delays when shifting traffic to the Green environment. Without warm-up, the first requests will experience high latency while connections are established, potentially triggering false positive alerts.

**Purpose:**
- Pre-populate connection pool before traffic arrives
- Prevent cold start latency spikes
- Ensure consistent response times from first request
- Avoid triggering automated rollback due to initial slow responses

**Target State:**
- Minimum 10 active connections in pool
- At least 5 idle connections ready
- All connections validated and healthy
- Connection pool metrics within normal range

---

### 10.2 Connection Pool Configuration

#### EF Core DbContext Configuration

```csharp
// File: Program.cs or DependencyInjection.cs

services.AddDbContext<ThinkOnErpDbContext>(options =>
{
    options.UseOracle(
        configuration.GetConnectionString("OracleDb"),
        oracleOptions =>
        {
            // Connection pooling configuration
            oracleOptions.UseOracleSQLCompatibility("11");
            oracleOptions.CommandTimeout(30);
            oracleOptions.MaxBatchSize(100);
            
            // Connection resilience
            oracleOptions.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(5),
                errorNumbersToAdd: null);
        })
        .EnableSensitiveDataLogging(isDevelopment)
        .EnableDetailedErrors(isDevelopment);
});
```

#### Oracle Connection String Configuration

```json
{
  "ConnectionStrings": {
    "OracleDb": "Data Source=prod-oracle:1521/THINKONERP;User Id=THINKONERP_USER;Password=***;Pooling=true;Min Pool Size=10;Max Pool Size=100;Connection Lifetime=300;Connection Timeout=30;Incr Pool Size=5;Decr Pool Size=2;"
  }
}
```

**Connection String Parameters:**
- `Pooling=true`: Enable connection pooling
- `Min Pool Size=10`: Maintain at least 10 connections
- `Max Pool Size=100`: Allow up to 100 connections
- `Connection Lifetime=300`: Recycle connections after 5 minutes
- `Connection Timeout=30`: 30 second connection timeout
- `Incr Pool Size=5`: Add 5 connections when pool grows
- `Decr Pool Size=2`: Remove 2 connections when pool shrinks

---

### 10.3 Warm-Up Script

```bash
#!/bin/bash
# File: scripts/warmup-connection-pool.sh
# Purpose: Warm up EF Core connection pool before traffic shift

set -e

# Configuration
ENVIRONMENT=${1:-green}
BASE_URL="https://${ENVIRONMENT}.thinkonerp.com"
TARGET_CONNECTIONS=10
WARMUP_REQUESTS=50
CONCURRENT_REQUESTS=5

echo "=== Connection Pool Warm-Up ==="
echo "Environment: $ENVIRONMENT"
echo "Target URL: $BASE_URL"
echo "Target Connections: $TARGET_CONNECTIONS"
echo "Warmup Requests: $WARMUP_REQUESTS"
echo "Timestamp: $(date)"
echo ""

# Step 1: Verify environment is healthy
echo "Step 1: Verifying environment health..."
health_response=$(curl -s -w "\n%{http_code}" "$BASE_URL/health")
health_code=$(echo "$health_response" | tail -n1)

if [ "$health_code" -ne 200 ]; then
  echo "❌ Health check failed (HTTP $health_code)"
  exit 1
fi

echo "✅ Environment is healthy"
echo ""

# Step 2: Warm up database connections
echo "Step 2: Warming up database connections..."

warmup_database_connections() {
  local request_num=$1
  
  # Make a simple database query to establish connection
  curl -s -o /dev/null -w "%{http_code}" \
    "$BASE_URL/health/database" \
    --max-time 10 \
    --connect-timeout 5
}

# Execute warmup requests in parallel
echo "Executing $WARMUP_REQUESTS warmup requests..."
for i in $(seq 1 $WARMUP_REQUESTS); do
  warmup_database_connections $i &
  
  # Limit concurrent requests
  if [ $((i % CONCURRENT_REQUESTS)) -eq 0 ]; then
    wait
  fi
  
  # Progress indicator
  if [ $((i % 10)) -eq 0 ]; then
    echo "  Progress: $i/$WARMUP_REQUESTS requests"
  fi
done

wait
echo "✅ Warmup requests completed"
echo ""

# Step 3: Verify connection pool status
echo "Step 3: Verifying connection pool status..."
sleep 2  # Allow time for metrics to update

pool_stats=$(curl -s "$BASE_URL/api/admin/connection-pool-stats")
active_connections=$(echo "$pool_stats" | jq -r '.activeConnections')
idle_connections=$(echo "$pool_stats" | jq -r '.idleConnections')
total_connections=$(echo "$pool_stats" | jq -r '.totalConnections')

echo "Connection Pool Status:"
echo "  Active Connections: $active_connections"
echo "  Idle Connections: $idle_connections"
echo "  Total Connections: $total_connections"
echo ""

# Validate pool status
if [ "$total_connections" -lt "$TARGET_CONNECTIONS" ]; then
  echo "⚠️  Warning: Connection pool has only $total_connections connections (target: $TARGET_CONNECTIONS)"
  echo "   Executing additional warmup..."
  
  # Execute additional warmup
  for i in $(seq 1 20); do
    curl -s -o /dev/null "$BASE_URL/health/database" &
  done
  wait
  
  # Re-check
  sleep 2
  pool_stats=$(curl -s "$BASE_URL/api/admin/connection-pool-stats")
  total_connections=$(echo "$pool_stats" | jq -r '.totalConnections')
  echo "  Updated Total Connections: $total_connections"
fi

if [ "$total_connections" -ge "$TARGET_CONNECTIONS" ]; then
  echo "✅ Connection pool warmed up successfully"
else
  echo "❌ Failed to warm up connection pool"
  exit 1
fi

echo ""

# Step 4: Execute sample queries for each repository
echo "Step 4: Executing sample queries for each repository..."

execute_repository_query() {
  local repo=$1
  local endpoint=$2
  
  echo -n "  Testing $repo... "
  
  response=$(curl -s -w "\n%{http_code}" "$BASE_URL$endpoint" --max-time 10)
  http_code=$(echo "$response" | tail -n1)
  
  if [ "$http_code" -eq 200 ]; then
    echo "✅"
  else
    echo "❌ (HTTP $http_code)"
  fi
}

# Core repositories
execute_repository_query "CompanyRepository" "/api/companies?pageSize=1"
execute_repository_query "BranchRepository" "/api/branches?pageSize=1"
execute_repository_query "UserRepository" "/api/users?pageSize=1"
execute_repository_query "CurrencyRepository" "/api/currencies?pageSize=1"
execute_repository_query "RoleRepository" "/api/roles?pageSize=1"

echo ""

# Step 5: Measure response times
echo "Step 5: Measuring response times..."

measure_response_time() {
  local endpoint=$1
  local description=$2
  
  response_time=$(curl -s -o /dev/null -w "%{time_total}" "$BASE_URL$endpoint")
  response_time_ms=$(echo "$response_time * 1000" | bc)
  
  echo "  $description: ${response_time_ms}ms"
  
  # Check if response time is acceptable
  if (( $(echo "$response_time_ms > 500" | bc -l) )); then
    echo "    ⚠️  Warning: Response time exceeds 500ms"
  fi
}

measure_response_time "/health" "Health Check"
measure_response_time "/health/database" "Database Health"
measure_response_time "/api/companies?pageSize=10" "Company List"
measure_response_time "/api/users?pageSize=10" "User List"

echo ""

# Step 6: Final verification
echo "Step 6: Final verification..."

# Get final pool stats
final_pool_stats=$(curl -s "$BASE_URL/api/admin/connection-pool-stats")
final_active=$(echo "$final_pool_stats" | jq -r '.activeConnections')
final_idle=$(echo "$final_pool_stats" | jq -r '.idleConnections')
final_total=$(echo "$final_pool_stats" | jq -r '.totalConnections')

echo "Final Connection Pool Status:"
echo "  Active: $final_active"
echo "  Idle: $final_idle"
echo "  Total: $final_total"
echo ""

# Check if pool is ready
if [ "$final_total" -ge "$TARGET_CONNECTIONS" ] && [ "$final_idle" -ge 5 ]; then
  echo "✅ Connection pool is ready for traffic"
  echo ""
  echo "=== Warm-Up Complete ==="
  echo "Duration: $SECONDS seconds"
  exit 0
else
  echo "❌ Connection pool not ready"
  echo "   Total connections: $final_total (target: $TARGET_CONNECTIONS)"
  echo "   Idle connections: $final_idle (target: >= 5)"
  exit 1
fi
```

---

### 10.4 Connection Pool Health Check Endpoint

Implement an endpoint to monitor connection pool status:

```csharp
// File: Controllers/AdminController.cs

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;
using System.Data;

namespace ThinkOnErp.API.Controllers
{
    [ApiController]
    [Route("api/admin")]
    public class AdminController : ControllerBase
    {
        private readonly ThinkOnErpDbContext _context;
        private readonly ILogger<AdminController> _logger;

        public AdminController(
            ThinkOnErpDbContext context,
            ILogger<AdminController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet("connection-pool-stats")]
        public async Task<IActionResult> GetConnectionPoolStats()
        {
            try
            {
                var connection = _context.Database.GetDbConnection() as OracleConnection;
                
                if (connection == null)
                {
                    return BadRequest(new { error = "Not an Oracle connection" });
                }

                // Get connection string to extract pool configuration
                var connectionString = connection.ConnectionString;
                var builder = new OracleConnectionStringBuilder(connectionString);

                // Get pool statistics
                var poolStats = new
                {
                    // Configuration
                    minPoolSize = builder.MinPoolSize,
                    maxPoolSize = builder.MaxPoolSize,
                    connectionLifetime = builder.ConnectionLifeTime,
                    connectionTimeout = builder.ConnectionTimeout,
                    
                    // Current state (approximation - Oracle doesn't expose exact pool stats)
                    // We estimate based on recent activity
                    activeConnections = await EstimateActiveConnectionsAsync(),
                    idleConnections = await EstimateIdleConnectionsAsync(),
                    totalConnections = await EstimateTotalConnectionsAsync(),
                    
                    // Health
                    isHealthy = true,
                    lastChecked = DateTime.UtcNow
                };

                return Ok(poolStats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting connection pool stats");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        private async Task<int> EstimateActiveConnectionsAsync()
        {
            // Query Oracle to estimate active connections from this application
            try
            {
                var sql = @"
                    SELECT COUNT(*) 
                    FROM V$SESSION 
                    WHERE USERNAME = :username 
                    AND STATUS = 'ACTIVE'";

                var username = _context.Database.GetDbConnection().Database;
                
                var count = await _context.Database
                    .SqlQueryRaw<int>(sql, new OracleParameter("username", username))
                    .FirstOrDefaultAsync();

                return count;
            }
            catch
            {
                return 0;
            }
        }

        private async Task<int> EstimateIdleConnectionsAsync()
        {
            try
            {
                var sql = @"
                    SELECT COUNT(*) 
                    FROM V$SESSION 
                    WHERE USERNAME = :username 
                    AND STATUS = 'INACTIVE'";

                var username = _context.Database.GetDbConnection().Database;
                
                var count = await _context.Database
                    .SqlQueryRaw<int>(sql, new OracleParameter("username", username))
                    .FirstOrDefaultAsync();

                return count;
            }
            catch
            {
                return 0;
            }
        }

        private async Task<int> EstimateTotalConnectionsAsync()
        {
            try
            {
                var sql = @"
                    SELECT COUNT(*) 
                    FROM V$SESSION 
                    WHERE USERNAME = :username";

                var username = _context.Database.GetDbConnection().Database;
                
                var count = await _context.Database
                    .SqlQueryRaw<int>(sql, new OracleParameter("username", username))
                    .FirstOrDefaultAsync();

                return count;
            }
            catch
            {
                return 0;
            }
        }

        [HttpPost("warmup-connection-pool")]
        public async Task<IActionResult> WarmUpConnectionPool([FromQuery] int targetConnections = 10)
        {
            try
            {
                _logger.LogInformation("Starting connection pool warm-up (target: {Target})", targetConnections);

                var tasks = new List<Task>();

                // Execute simple queries to establish connections
                for (int i = 0; i < targetConnections; i++)
                {
                    tasks.Add(Task.Run(async () =>
                    {
                        // Simple query to establish connection
                        await _context.Database.ExecuteSqlRawAsync("SELECT 1 FROM DUAL");
                    }));
                }

                await Task.WhenAll(tasks);

                // Wait for connections to be established
                await Task.Delay(TimeSpan.FromSeconds(2));

                // Get updated stats
                var stats = await GetConnectionPoolStats();

                _logger.LogInformation("Connection pool warm-up completed");

                return Ok(new
                {
                    message = "Connection pool warmed up",
                    stats = stats
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error warming up connection pool");
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}
```

---

### 10.5 Automated Warm-Up Integration

Integrate warm-up into deployment procedure:

```bash
# In deployment script, before traffic shift

echo "Warming up connection pool..."
./scripts/warmup-connection-pool.sh green

if [ $? -eq 0 ]; then
  echo "✅ Connection pool ready"
  echo "Proceeding with traffic shift..."
  ./scripts/shift-traffic.sh --blue 95 --green 5
else
  echo "❌ Connection pool warm-up failed"
  echo "Aborting traffic shift"
  exit 1
fi
```

---

### 10.6 Connection Pool Monitoring

Monitor connection pool health during deployment:

```bash
#!/bin/bash
# File: scripts/monitor-connection-pool.sh
# Purpose: Continuously monitor connection pool status

ENVIRONMENT=${1:-green}
BASE_URL="https://${ENVIRONMENT}.thinkonerp.com"
INTERVAL=10

echo "=== Connection Pool Monitoring ==="
echo "Environment: $ENVIRONMENT"
echo "Interval: ${INTERVAL}s"
echo ""

while true; do
  clear
  echo "=== Connection Pool Status ==="
  echo "Timestamp: $(date)"
  echo ""
  
  stats=$(curl -s "$BASE_URL/api/admin/connection-pool-stats")
  
  if [ $? -eq 0 ]; then
    echo "$stats" | jq '{
      Configuration: {
        MinPoolSize: .minPoolSize,
        MaxPoolSize: .maxPoolSize,
        ConnectionLifetime: .connectionLifetime,
        ConnectionTimeout: .connectionTimeout
      },
      CurrentState: {
        ActiveConnections: .activeConnections,
        IdleConnections: .idleConnections,
        TotalConnections: .totalConnections
      },
      Health: {
        IsHealthy: .isHealthy,
        LastChecked: .lastChecked
      }
    }'
    
    # Calculate pool usage percentage
    total=$(echo "$stats" | jq -r '.totalConnections')
    max=$(echo "$stats" | jq -r '.maxPoolSize')
    usage=$(echo "scale=2; $total / $max * 100" | bc)
    
    echo ""
    echo "Pool Usage: ${usage}%"
    
    if (( $(echo "$usage > 80" | bc -l) )); then
      echo "⚠️  Warning: Pool usage exceeds 80%"
    elif (( $(echo "$usage > 95" | bc -l) )); then
      echo "🔴 Critical: Pool usage exceeds 95%"
    else
      echo "✅ Pool usage normal"
    fi
  else
    echo "❌ Failed to retrieve connection pool stats"
  fi
  
  echo ""
  echo "Press Ctrl+C to stop monitoring"
  
  sleep $INTERVAL
done
```

---

### 10.7 Connection Pool Troubleshooting

#### Issue: Pool Not Warming Up

**Symptoms:**
- Total connections remain below target
- Idle connections stay at 0
- First requests have high latency

**Diagnosis:**
```bash
# Check database connectivity
curl https://green.thinkonerp.com/health/database

# Check Oracle listener
tnsping prod-oracle

# Check firewall rules
telnet prod-oracle 1521

# Check Oracle session limits
sqlplus thinkonerp_user/password@prod-oracle <<EOF
SELECT * FROM V\$RESOURCE_LIMIT WHERE RESOURCE_NAME = 'sessions';
EOF
```

**Resolution:**
1. Verify database is accessible from Green environment
2. Check Oracle session limits (should be > 200)
3. Verify connection string is correct
4. Check application logs for connection errors
5. Increase warm-up request count

#### Issue: Pool Exhaustion During Warm-Up

**Symptoms:**
- Connection pool reaches max size
- New requests fail with "connection pool exhausted"
- High connection wait times

**Diagnosis:**
```bash
# Check current pool usage
curl https://green.thinkonerp.com/api/admin/connection-pool-stats

# Check for connection leaks
./scripts/check-connection-leaks.sh
```

**Resolution:**
1. Increase `Max Pool Size` in connection string
2. Check for connection leaks in code (missing Dispose/using statements)
3. Reduce concurrent warm-up requests
4. Increase `Connection Lifetime` to recycle connections

#### Issue: Slow Connection Establishment

**Symptoms:**
- Warm-up takes > 2 minutes
- Individual connection establishment > 5 seconds
- Timeout errors during warm-up

**Diagnosis:**
```bash
# Measure connection time
time sqlplus thinkonerp_user/password@prod-oracle <<EOF
SELECT 1 FROM DUAL;
EXIT;
EOF

# Check network latency
ping prod-oracle
```

**Resolution:**
1. Check network latency between app server and database
2. Verify DNS resolution is fast
3. Check Oracle listener performance
4. Consider increasing `Connection Timeout` in connection string

---

### 10.8 Connection Pool Warm-Up Checklist

Before traffic shift, verify:

- [ ] Connection pool warm-up script executed successfully
- [ ] Total connections >= 10
- [ ] Idle connections >= 5
- [ ] All repository queries tested
- [ ] Response times < 500ms
- [ ] No connection errors in logs
- [ ] Pool usage < 80%
- [ ] Database health check passing
- [ ] Connection pool monitoring active

---

### 10.9 Connection Pool Best Practices

1. **Always warm up before traffic shift**
   - Never shift traffic to cold environment
   - Allow 2-3 minutes for warm-up
   - Verify pool status before proceeding

2. **Monitor pool usage continuously**
   - Set up alerts for high pool usage (> 80%)
   - Track connection establishment time
   - Monitor for connection leaks

3. **Configure appropriate pool sizes**
   - Min Pool Size: 10 (keeps connections ready)
   - Max Pool Size: 100 (prevents exhaustion)
   - Adjust based on load testing results

4. **Use connection lifetime**
   - Set to 300 seconds (5 minutes)
   - Prevents stale connections
   - Balances connection reuse and freshness

5. **Test warm-up in staging**
   - Verify warm-up procedure works
   - Measure warm-up duration
   - Identify any issues before production
## 11. Post-Deployment Validation

### 11.1 Post-Deployment Validation Overview

Post-deployment validation ensures the EF Core migration is functioning correctly in production and meets all acceptance criteria. This phase includes functional testing, performance validation, data integrity checks, and user acceptance.

**Validation Duration**: 24-48 hours after 100% traffic shift  
**Success Criteria**: All validation tests pass with no critical issues

---

### 11.2 Immediate Validation (First Hour)

Execute these checks immediately after 100% traffic shift to Green environment.

#### 11.2.1 System Health Validation

```bash
#!/bin/bash
# File: scripts/post-deployment-validation.sh
# Purpose: Comprehensive post-deployment validation

echo "=== Post-Deployment Validation ==="
echo "Timestamp: $(date)"
echo ""

# Test 1: Health Checks
echo "Test 1: Health Checks"
echo "====================="

check_endpoint() {
  local endpoint=$1
  local description=$2
  
  echo -n "  $description... "
  response=$(curl -s -w "\n%{http_code}" "https://thinkonerp.com$endpoint")
  http_code=$(echo "$response" | tail -n1)
  
  if [ "$http_code" -eq 200 ]; then
    echo "✅ PASS"
    return 0
  else
    echo "❌ FAIL (HTTP $http_code)"
    return 1
  fi
}

check_endpoint "/health" "Basic Health"
check_endpoint "/health/database" "Database Connectivity"
check_endpoint "/health/repositories" "Repository Health"
check_endpoint "/health/feature-flags" "Feature Flags"

echo ""

# Test 2: Verify EF Core is Active
echo "Test 2: Verify EF Core is Active"
echo "================================="

feature_flags=$(curl -s "https://thinkonerp.com/health/feature-flags")
efcore_enabled=$(echo "$feature_flags" | jq -r '.featureFlags.EfCoreMigration.enabled')
efcore_repos=$(echo "$feature_flags" | jq '[.featureFlags.repositories[] | select(. == true)] | length')

echo "  EF Core Migration Enabled: $efcore_enabled"
echo "  EF Core Repositories: $efcore_repos / 23"

if [ "$efcore_enabled" == "true" ] && [ "$efcore_repos" -eq 23 ]; then
  echo "  ✅ All repositories using EF Core"
else
  echo "  ❌ Not all repositories migrated"
fi

echo ""

# Test 3: Traffic Distribution
echo "Test 3: Traffic Distribution"
echo "============================"

# Make 20 requests and check which servers respond
declare -A server_counts
for i in {1..20}; do
  server=$(curl -s "https://thinkonerp.com/api/admin/server-info" | jq -r '.serverName')
  ((server_counts[$server]++))
done

echo "  Traffic Distribution:"
for server in "${!server_counts[@]}"; do
  count=${server_counts[$server]}
  percentage=$((count * 5))
  echo "    $server: $count requests (${percentage}%)"
done

# Verify no traffic to Blue servers
if [[ -n "${server_counts[app-server-01]}" ]] || [[ -n "${server_counts[app-server-02]}" ]]; then
  echo "  ⚠️  Warning: Traffic still going to Blue environment"
else
  echo "  ✅ All traffic on Green environment"
fi

echo ""

# Test 4: Error Rate
echo "Test 4: Error Rate"
echo "=================="

metrics=$(curl -s "https://thinkonerp.com/api/admin/metrics")
error_rate=$(echo "$metrics" | jq -r '.errorRate')

echo "  Current Error Rate: ${error_rate}%"

if (( $(echo "$error_rate < 0.1" | bc -l) )); then
  echo "  ✅ Error rate within acceptable range"
else
  echo "  ⚠️  Warning: Error rate elevated"
fi

echo ""

# Test 5: Response Time
echo "Test 5: Response Time"
echo "===================="

response_time_p50=$(echo "$metrics" | jq -r '.responseTimeP50')
response_time_p95=$(echo "$metrics" | jq -r '.responseTimeP95')
response_time_p99=$(echo "$metrics" | jq -r '.responseTimeP99')

echo "  Response Time p50: ${response_time_p50}ms"
echo "  Response Time p95: ${response_time_p95}ms"
echo "  Response Time p99: ${response_time_p99}ms"

if (( $(echo "$response_time_p95 < 500" | bc -l) )); then
  echo "  ✅ Response times within acceptable range"
else
  echo "  ⚠️  Warning: Response times elevated"
fi

echo ""

# Test 6: Database Query Performance
echo "Test 6: Database Query Performance"
echo "=================================="

db_query_time_p95=$(echo "$metrics" | jq -r '.dbQueryTimeP95')

echo "  Database Query Time p95: ${db_query_time_p95}ms"

if (( $(echo "$db_query_time_p95 < 200" | bc -l) )); then
  echo "  ✅ Database query times within acceptable range"
else
  echo "  ⚠️  Warning: Database query times elevated"
fi

echo ""

# Test 7: Connection Pool
echo "Test 7: Connection Pool"
echo "======================="

pool_stats=$(curl -s "https://thinkonerp.com/api/admin/connection-pool-stats")
active_connections=$(echo "$pool_stats" | jq -r '.activeConnections')
total_connections=$(echo "$pool_stats" | jq -r '.totalConnections')
max_connections=$(echo "$pool_stats" | jq -r '.maxPoolSize')

pool_usage=$(echo "scale=2; $total_connections / $max_connections * 100" | bc)

echo "  Active Connections: $active_connections"
echo "  Total Connections: $total_connections / $max_connections"
echo "  Pool Usage: ${pool_usage}%"

if (( $(echo "$pool_usage < 80" | bc -l) )); then
  echo "  ✅ Connection pool usage normal"
else
  echo "  ⚠️  Warning: Connection pool usage high"
fi

echo ""
echo "=== Immediate Validation Complete ==="
```

---

### 11.2.2 Functional Testing

Test core functionality to ensure all features work correctly:

```bash
#!/bin/bash
# File: scripts/functional-tests.sh
# Purpose: Execute functional tests against production

echo "=== Functional Testing ==="
echo ""

BASE_URL="https://thinkonerp.com"
AUTH_TOKEN=""  # Set from environment or secure store

# Authenticate
echo "Authenticating..."
auth_response=$(curl -s -X POST "$BASE_URL/api/auth/login" \
  -H "Content-Type: application/json" \
  -d '{"username":"test_user","password":"test_password"}')

AUTH_TOKEN=$(echo "$auth_response" | jq -r '.token')

if [ -z "$AUTH_TOKEN" ] || [ "$AUTH_TOKEN" == "null" ]; then
  echo "❌ Authentication failed"
  exit 1
fi

echo "✅ Authentication successful"
echo ""

# Test 1: Company Operations
echo "Test 1: Company Operations"
echo "=========================="

# List companies
echo -n "  List companies... "
companies=$(curl -s -H "Authorization: Bearer $AUTH_TOKEN" \
  "$BASE_URL/api/companies?pageSize=10")
company_count=$(echo "$companies" | jq '.data | length')

if [ "$company_count" -gt 0 ]; then
  echo "✅ PASS ($company_count companies)"
else
  echo "❌ FAIL"
fi

# Get company by ID
echo -n "  Get company by ID... "
company_id=$(echo "$companies" | jq -r '.data[0].rowId')
company=$(curl -s -H "Authorization: Bearer $AUTH_TOKEN" \
  "$BASE_URL/api/companies/$company_id")

if [ "$(echo "$company" | jq -r '.rowId')" == "$company_id" ]; then
  echo "✅ PASS"
else
  echo "❌ FAIL"
fi

echo ""

# Test 2: User Operations
echo "Test 2: User Operations"
echo "======================="

# List users
echo -n "  List users... "
users=$(curl -s -H "Authorization: Bearer $AUTH_TOKEN" \
  "$BASE_URL/api/users?pageSize=10")
user_count=$(echo "$users" | jq '.data | length')

if [ "$user_count" -gt 0 ]; then
  echo "✅ PASS ($user_count users)"
else
  echo "❌ FAIL"
fi

# Get user by ID
echo -n "  Get user by ID... "
user_id=$(echo "$users" | jq -r '.data[0].rowId')
user=$(curl -s -H "Authorization: Bearer $AUTH_TOKEN" \
  "$BASE_URL/api/users/$user_id")

if [ "$(echo "$user" | jq -r '.rowId')" == "$user_id" ]; then
  echo "✅ PASS"
else
  echo "❌ FAIL"
fi

echo ""

# Test 3: Branch Operations
echo "Test 3: Branch Operations"
echo "========================="

# List branches
echo -n "  List branches... "
branches=$(curl -s -H "Authorization: Bearer $AUTH_TOKEN" \
  "$BASE_URL/api/branches?pageSize=10")
branch_count=$(echo "$branches" | jq '.data | length')

if [ "$branch_count" -gt 0 ]; then
  echo "✅ PASS ($branch_count branches)"
else
  echo "❌ FAIL"
fi

echo ""

# Test 4: Role and Permission Operations
echo "Test 4: Role and Permission Operations"
echo "======================================"

# List roles
echo -n "  List roles... "
roles=$(curl -s -H "Authorization: Bearer $AUTH_TOKEN" \
  "$BASE_URL/api/roles?pageSize=10")
role_count=$(echo "$roles" | jq '.data | length')

if [ "$role_count" -gt 0 ]; then
  echo "✅ PASS ($role_count roles)"
else
  echo "❌ FAIL"
fi

# List permissions
echo -n "  List permissions... "
permissions=$(curl -s -H "Authorization: Bearer $AUTH_TOKEN" \
  "$BASE_URL/api/permissions?pageSize=10")
permission_count=$(echo "$permissions" | jq '.data | length')

if [ "$permission_count" -gt 0 ]; then
  echo "✅ PASS ($permission_count permissions)"
else
  echo "❌ FAIL"
fi

echo ""

# Test 5: Ticket Operations
echo "Test 5: Ticket Operations"
echo "========================="

# List tickets
echo -n "  List tickets... "
tickets=$(curl -s -H "Authorization: Bearer $AUTH_TOKEN" \
  "$BASE_URL/api/tickets?pageSize=10")
ticket_count=$(echo "$tickets" | jq '.data | length')

if [ "$ticket_count" -ge 0 ]; then
  echo "✅ PASS ($ticket_count tickets)"
else
  echo "❌ FAIL"
fi

echo ""

# Test 6: Audit Log Operations
echo "Test 6: Audit Log Operations"
echo "============================"

# List audit logs
echo -n "  List audit logs... "
audit_logs=$(curl -s -H "Authorization: Bearer $AUTH_TOKEN" \
  "$BASE_URL/api/audit-logs?pageSize=10")
audit_count=$(echo "$audit_logs" | jq '.data | length')

if [ "$audit_count" -gt 0 ]; then
  echo "✅ PASS ($audit_count audit logs)"
else
  echo "❌ FAIL"
fi

echo ""
echo "=== Functional Testing Complete ==="
```

---

### 11.3 Performance Validation (24 Hours)

Monitor performance metrics for 24 hours to ensure stability:

#### 11.3.1 Performance Metrics Comparison

```bash
#!/bin/bash
# File: scripts/compare-performance.sh
# Purpose: Compare EF Core performance with ADO.NET baseline

echo "=== Performance Comparison ==="
echo ""

# Get baseline metrics (from pre-deployment)
BASELINE_ERROR_RATE=0.05
BASELINE_RESPONSE_TIME_P95=450
BASELINE_DB_QUERY_TIME_P95=180

# Get current metrics
current_metrics=$(curl -s "https://thinkonerp.com/api/admin/metrics")
current_error_rate=$(echo "$current_metrics" | jq -r '.errorRate')
current_response_time_p95=$(echo "$current_metrics" | jq -r '.responseTimeP95')
current_db_query_time_p95=$(echo "$current_metrics" | jq -r '.dbQueryTimeP95')

echo "Metric Comparison:"
echo "=================="
echo ""

# Error Rate
echo "Error Rate:"
echo "  Baseline (ADO.NET): ${BASELINE_ERROR_RATE}%"
echo "  Current (EF Core):  ${current_error_rate}%"
error_rate_diff=$(echo "$current_error_rate - $BASELINE_ERROR_RATE" | bc)
echo "  Difference: ${error_rate_diff}%"

if (( $(echo "$current_error_rate <= $BASELINE_ERROR_RATE * 1.1" | bc -l) )); then
  echo "  ✅ Within acceptable range (≤ 10% increase)"
else
  echo "  ⚠️  Exceeds acceptable range"
fi

echo ""

# Response Time
echo "Response Time (p95):"
echo "  Baseline (ADO.NET): ${BASELINE_RESPONSE_TIME_P95}ms"
echo "  Current (EF Core):  ${current_response_time_p95}ms"
response_time_diff=$(echo "$current_response_time_p95 - $BASELINE_RESPONSE_TIME_P95" | bc)
echo "  Difference: ${response_time_diff}ms"

if (( $(echo "$current_response_time_p95 <= $BASELINE_RESPONSE_TIME_P95 * 1.1" | bc -l) )); then
  echo "  ✅ Within acceptable range (≤ 10% increase)"
else
  echo "  ⚠️  Exceeds acceptable range"
fi

echo ""

# Database Query Time
echo "Database Query Time (p95):"
echo "  Baseline (ADO.NET): ${BASELINE_DB_QUERY_TIME_P95}ms"
echo "  Current (EF Core):  ${current_db_query_time_p95}ms"
db_query_time_diff=$(echo "$current_db_query_time_p95 - $BASELINE_DB_QUERY_TIME_P95" | bc)
echo "  Difference: ${db_query_time_diff}ms"

if (( $(echo "$current_db_query_time_p95 <= $BASELINE_DB_QUERY_TIME_P95 * 1.1" | bc -l) )); then
  echo "  ✅ Within acceptable range (≤ 10% increase)"
else
  echo "  ⚠️  Exceeds acceptable range"
fi

echo ""
echo "=== Performance Comparison Complete ==="
```

---

### 11.4 Data Integrity Validation

Verify data integrity after migration:

```sql
-- File: scripts/data-integrity-checks.sql
-- Purpose: Validate data integrity after EF Core migration

-- Check 1: Record Counts
SELECT 'SYS_COMPANY' AS TABLE_NAME, COUNT(*) AS RECORD_COUNT FROM SYS_COMPANY
UNION ALL
SELECT 'SYS_BRANCH', COUNT(*) FROM SYS_BRANCH
UNION ALL
SELECT 'SYS_USERS', COUNT(*) FROM SYS_USERS
UNION ALL
SELECT 'SYS_ROLE', COUNT(*) FROM SYS_ROLE
UNION ALL
SELECT 'SYS_AUDIT_LOG', COUNT(*) FROM SYS_AUDIT_LOG;

-- Check 2: Recent Audit Logs (verify audit logging is working)
SELECT 
    TABLE_NAME,
    OPERATION,
    COUNT(*) AS COUNT,
    MAX(TIMESTAMP) AS LAST_OPERATION
FROM SYS_AUDIT_LOG
WHERE TIMESTAMP > SYSDATE - 1  -- Last 24 hours
GROUP BY TABLE_NAME, OPERATION
ORDER BY TABLE_NAME, OPERATION;

-- Check 3: Verify No Orphaned Records
SELECT 
    'Branches without Company' AS CHECK_NAME,
    COUNT(*) AS ORPHANED_COUNT
FROM SYS_BRANCH b
WHERE NOT EXISTS (SELECT 1 FROM SYS_COMPANY c WHERE c.ROW_ID = b.COMPANY_ID)
UNION ALL
SELECT 
    'Users without Branch',
    COUNT(*)
FROM SYS_USERS u
WHERE NOT EXISTS (SELECT 1 FROM SYS_BRANCH b WHERE b.ROW_ID = u.BRANCH_ID);

-- Check 4: Verify Referential Integrity
SELECT 
    constraint_name,
    table_name,
    status
FROM user_constraints
WHERE constraint_type = 'R'  -- Foreign keys
AND status != 'ENABLED'
ORDER BY table_name;

-- Check 5: Verify Sequences are in Sync
SELECT 
    'SYS_COMPANY' AS TABLE_NAME,
    MAX(ROW_ID) AS MAX_ID,
    SEQ_SYS_COMPANY.NEXTVAL AS NEXT_SEQUENCE_VALUE
FROM SYS_COMPANY
UNION ALL
SELECT 
    'SYS_BRANCH',
    MAX(ROW_ID),
    SEQ_SYS_BRANCH.NEXTVAL
FROM SYS_BRANCH
UNION ALL
SELECT 
    'SYS_USERS',
    MAX(ROW_ID),
    SEQ_SYS_USERS.NEXTVAL
FROM SYS_USERS;
```

---

### 11.5 User Acceptance Testing

Coordinate with business users to validate functionality:

#### UAT Checklist

- [ ] **Authentication and Authorization**
  - [ ] Users can log in successfully
  - [ ] Role-based access control working
  - [ ] Permission checks functioning correctly
  - [ ] Refresh token mechanism working

- [ ] **Company Management**
  - [ ] Create new company
  - [ ] Update company details
  - [ ] View company list
  - [ ] Search companies
  - [ ] Upload company logo

- [ ] **Branch Management**
  - [ ] Create new branch
  - [ ] Update branch details
  - [ ] View branch list
  - [ ] Set default branch

- [ ] **User Management**
  - [ ] Create new user
  - [ ] Update user details
  - [ ] Assign roles to users
  - [ ] Deactivate users
  - [ ] Reset passwords

- [ ] **Ticket System**
  - [ ] Create new ticket
  - [ ] Update ticket status
  - [ ] Add comments to tickets
  - [ ] Upload attachments
  - [ ] Search tickets
  - [ ] View ticket history

- [ ] **Reporting**
  - [ ] Generate audit reports
  - [ ] View system analytics
  - [ ] Export data

- [ ] **Performance**
  - [ ] Page load times acceptable
  - [ ] Search operations responsive
  - [ ] No timeouts or errors
  - [ ] Concurrent user operations work correctly

---

### 11.6 Monitoring and Alerting Validation

Verify monitoring and alerting systems are functioning:

```bash
#!/bin/bash
# File: scripts/validate-monitoring.sh
# Purpose: Validate monitoring and alerting configuration

echo "=== Monitoring and Alerting Validation ==="
echo ""

# Test 1: Verify Alert Rules are Active
echo "Test 1: Alert Rules"
echo "==================="

alert_rules=$(az monitor metrics alert list \
  --resource-group thinkonerp-rg \
  --query "[?contains(name, 'efcore')].{Name:name, Enabled:enabled, Severity:severity}" \
  --output json)

enabled_count=$(echo "$alert_rules" | jq '[.[] | select(.Enabled == true)] | length')
total_count=$(echo "$alert_rules" | jq 'length')

echo "  Alert Rules: $enabled_count / $total_count enabled"

if [ "$enabled_count" -eq "$total_count" ]; then
  echo "  ✅ All alert rules enabled"
else
  echo "  ⚠️  Some alert rules disabled"
fi

echo ""

# Test 2: Verify Application Insights is Receiving Data
echo "Test 2: Application Insights"
echo "============================"

# Query recent telemetry
recent_requests=$(az monitor app-insights query \
  --app thinkonerp-appinsights \
  --analytics-query "requests | where timestamp > ago(5m) | count" \
  --output json | jq -r '.tables[0].rows[0][0]')

echo "  Recent Requests (5 min): $recent_requests"

if [ "$recent_requests" -gt 0 ]; then
  echo "  ✅ Application Insights receiving data"
else
  echo "  ❌ No recent data in Application Insights"
fi

echo ""

# Test 3: Verify Dashboard is Accessible
echo "Test 3: Dashboard"
echo "================="

dashboard_url="https://portal.azure.com/#@thinkonerp/dashboard/deployment"
echo "  Dashboard URL: $dashboard_url"
echo "  ✅ Verify dashboard manually"

echo ""

# Test 4: Test Alert Notification
echo "Test 4: Alert Notifications"
echo "==========================="

echo "  Triggering test alert..."
# Trigger a test alert (warning level, not critical)
curl -s -X POST "https://thinkonerp.com/api/admin/trigger-test-alert" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"alertType":"test","severity":"warning"}'

echo "  ✅ Test alert triggered"
echo "  Verify notification received via email/Slack"

echo ""
echo "=== Monitoring Validation Complete ==="
```

---

### 11.7 Post-Deployment Report

Generate a comprehensive post-deployment report:

```bash
#!/bin/bash
# File: scripts/generate-post-deployment-report.sh
# Purpose: Generate comprehensive post-deployment report

REPORT_FILE="post-deployment-report-$(date +%Y%m%d-%H%M%S).md"

cat > "$REPORT_FILE" << 'EOF'
# EF Core Migration - Post-Deployment Report

## Deployment Information

**Deployment Date**: $(date)
**Deployment Duration**: [X] hours
**Rollback Count**: 0
**Final Status**: Success

---

## Validation Results

### System Health

- ✅ All health checks passing
- ✅ All 23 repositories using EF Core
- ✅ 100% traffic on Green environment
- ✅ No errors in application logs

### Performance Metrics

| Metric | Baseline (ADO.NET) | Current (EF Core) | Change | Status |
|--------|-------------------|-------------------|--------|--------|
| Error Rate | 0.05% | [X]% | [X]% | ✅ |
| Response Time p95 | 450ms | [X]ms | [X]ms | ✅ |
| DB Query Time p95 | 180ms | [X]ms | [X]ms | ✅ |
| Connection Pool Usage | 65% | [X]% | [X]% | ✅ |

### Functional Testing

- ✅ Authentication and authorization
- ✅ Company management operations
- ✅ User management operations
- ✅ Branch management operations
- ✅ Role and permission operations
- ✅ Ticket system operations
- ✅ Audit logging

### Data Integrity

- ✅ Record counts match baseline
- ✅ No orphaned records
- ✅ Referential integrity maintained
- ✅ Sequences in sync
- ✅ Audit logs being created

### User Acceptance

- ✅ Business users validated functionality
- ✅ No user-reported issues
- ✅ Performance acceptable to users

---

## Issues and Resolutions

### Issues Encountered

1. **Issue**: [Description]
   - **Impact**: [Impact level]
   - **Resolution**: [How it was resolved]
   - **Status**: Resolved

### Outstanding Items

None

---

## Recommendations

1. Continue monitoring for 7 days before decommissioning Blue environment
2. Schedule post-mortem meeting to discuss lessons learned
3. Update deployment documentation based on experience
4. Plan knowledge transfer sessions for team

---

## Sign-Off

**Deployment Lead**: [Name] - [Date]
**DevOps Team**: [Name] - [Date]
**DBA Team**: [Name] - [Date]
**Business Stakeholder**: [Name] - [Date]

---

## Appendix

### Deployment Timeline

- T+0h: Deployment initiated
- T+1h: Phase 1 complete (5% traffic)
- T+3h: Phase 2 complete (30% traffic)
- T+5h: Phase 3 complete (70% traffic)
- T+6h: Phase 4 complete (100% traffic)
- T+30h: 24-hour validation complete

### Metrics Graphs

[Include graphs from monitoring dashboard]

### Log Samples

[Include relevant log samples]
EOF

echo "Post-deployment report generated: $REPORT_FILE"
```

---

### 11.8 Post-Deployment Checklist

Complete this checklist within 48 hours of deployment:

- [ ] **Immediate Validation (Hour 1)**
  - [ ] All health checks passing
  - [ ] EF Core active for all repositories
  - [ ] 100% traffic on Green environment
  - [ ] Error rate < 0.1%
  - [ ] Response times < 500ms p95
  - [ ] Connection pool healthy

- [ ] **Functional Testing (Hours 1-4)**
  - [ ] Authentication working
  - [ ] All CRUD operations tested
  - [ ] Search functionality working
  - [ ] File uploads working
  - [ ] Audit logging active

- [ ] **Performance Validation (24 Hours)**
  - [ ] Performance metrics within 10% of baseline
  - [ ] No performance degradation over time
  - [ ] No memory leaks detected
  - [ ] Connection pool stable

- [ ] **Data Integrity (24 Hours)**
  - [ ] Record counts verified
  - [ ] No orphaned records
  - [ ] Referential integrity maintained
  - [ ] Audit logs complete

- [ ] **User Acceptance (48 Hours)**
  - [ ] Business users validated functionality
  - [ ] No critical user-reported issues
  - [ ] Performance acceptable to users

- [ ] **Monitoring and Alerting (48 Hours)**
  - [ ] All alerts configured and active
  - [ ] Monitoring dashboard accessible
  - [ ] Notifications working correctly

- [ ] **Documentation (48 Hours)**
  - [ ] Post-deployment report generated
  - [ ] Issues documented
  - [ ] Lessons learned captured
  - [ ] Knowledge base updated

- [ ] **Sign-Off (48 Hours)**
  - [ ] Deployment lead approval
  - [ ] DevOps team approval
  - [ ] DBA team approval
  - [ ] Business stakeholder approval

---

### 11.9 Success Criteria

Deployment is considered successful when:

1. ✅ All 23 repositories using EF Core
2. ✅ 100% traffic on Green environment for 24+ hours
3. ✅ Error rate < 0.1%
4. ✅ Response time p95 < 500ms
5. ✅ Performance within 10% of ADO.NET baseline
6. ✅ No critical issues reported
7. ✅ All functional tests passing
8. ✅ Data integrity verified
9. ✅ User acceptance obtained
10. ✅ All stakeholders signed off

When all criteria are met, proceed to Blue environment decommissioning (after 7-day stability period).
## 12. Communication Plan

### 12.1 Communication Overview

Effective communication is critical for successful deployment. This plan ensures all stakeholders are informed at appropriate times throughout the deployment process.

**Communication Objectives:**
- Keep stakeholders informed of deployment progress
- Provide timely updates on issues and resolutions
- Ensure transparency throughout the process
- Manage expectations and reduce uncertainty
- Facilitate rapid escalation when needed

---

### 12.2 Stakeholder Matrix

| Stakeholder Group | Communication Method | Frequency | Content Level |
|-------------------|---------------------|-----------|---------------|
| Executive Leadership | Email summary | Pre/Post deployment | High-level status |
| Business Stakeholders | Email + Meeting | Pre/During/Post | Business impact |
| Development Team | Slack + Email | Real-time | Technical details |
| DevOps Team | Slack | Real-time | Operational details |
| DBA Team | Slack + Phone | Real-time | Database metrics |
| Support Team | Email + Slack | Hourly during deployment | User impact |
| End Users | Portal announcement | Pre-deployment | Service availability |

---

### 12.3 Pre-Deployment Communication

#### T-1 Week: Initial Announcement

**To**: All Stakeholders  
**Method**: Email  
**Subject**: EF Core Migration Deployment Scheduled

```
Subject: EF Core Migration Deployment - Scheduled for [Date]

Team,

We are pleased to announce that the EF Core migration deployment has been scheduled for:

Date: [Date]
Time: [Time] - [Time] (estimated 6 hours)
Expected Impact: None (zero downtime deployment)

What is changing:
- Backend data access layer migrating from ADO.NET to Entity Framework Core
- No changes to user-facing functionality
- No changes to API contracts
- Improved performance and maintainability

Deployment approach:
- Blue-green deployment with gradual traffic shifting
- Continuous monitoring throughout deployment
- Automated rollback capability if issues occur
- 24-hour validation period after completion

What you need to know:
- No action required from end users
- System will remain available throughout deployment
- Support team will be monitoring for any issues
- Rollback plan in place if needed

Communication during deployment:
- Hourly status updates via email
- Real-time updates in #deployment-status Slack channel
- Immediate notification of any issues

If you have any questions or concerns, please contact:
- Deployment Lead: [Name] ([Email])
- DevOps Team: devops@thinkonerp.com

Thank you,
ThinkOnErp DevOps Team
```

#### T-24 Hours: Deployment Reminder

**To**: All Stakeholders  
**Method**: Email + Slack

```
Subject: Reminder: EF Core Migration Deployment Tomorrow

Team,

This is a reminder that the EF Core migration deployment will begin tomorrow:

Start Time: [Date] at [Time]
Duration: Approximately 6 hours
Impact: None expected (zero downtime)

Pre-deployment checklist completed:
✅ All code reviewed and tested
✅ Staging environment validated
✅ Rollback procedures tested
✅ Monitoring and alerts configured
✅ Team briefed and ready

During deployment:
- Monitor #deployment-status Slack channel for updates
- Support team on standby for any user issues
- DevOps team executing deployment procedure
- Automated monitoring and rollback in place

Contact information:
- Deployment Lead: [Name] - [Phone]
- DevOps On-Call: [Phone]
- Escalation: [Name] - [Phone]

We're confident in our preparation and look forward to a successful deployment.

Thank you,
ThinkOnErp DevOps Team
```

#### T-1 Hour: Deployment Starting Soon

**To**: Technical Teams  
**Method**: Slack (#deployment-status)

```
🚀 **EF Core Migration Deployment Starting in 1 Hour**

Start Time: [Time]
Deployment Lead: @[name]
DevOps Team: @devops-team
DBA: @[name]

Pre-flight checklist:
✅ Green environment healthy
✅ Database backup completed
✅ Monitoring active
✅ Team assembled

Monitoring Dashboard: [URL]
Deployment Runbook: [URL]

Stand by for deployment initiation...
```

---

### 12.4 During Deployment Communication

#### Deployment Initiated

**To**: All Stakeholders  
**Method**: Email + Slack

```
Subject: EF Core Migration Deployment - INITIATED

Team,

The EF Core migration deployment has been initiated.

Status: In Progress
Phase: Phase 0 - Pre-Deployment Checks
Started: [Time]
Expected Completion: [Time] (6 hours)

Current Activities:
- Final environment verification
- Database backup in progress
- Monitoring systems active

Next Steps:
- Phase 1: Enable pilot repositories (5% traffic)
- Continuous monitoring and validation
- Hourly status updates

Dashboard: [URL]
Real-time updates: #deployment-status Slack channel

No action required. System remains fully operational.

ThinkOnErp DevOps Team
```

#### Hourly Status Updates

**To**: All Stakeholders  
**Method**: Email + Slack

```
Subject: EF Core Migration Deployment - Status Update [Hour X]

Status: ✅ On Track
Current Phase: Phase [X] - [Description]
Progress: [X]% complete
Traffic Distribution: [X]% Blue / [X]% Green

Metrics (Last Hour):
- Error Rate: [X]% (Target: < 0.1%)
- Response Time p95: [X]ms (Target: < 500ms)
- Database Query Time: [X]ms (Target: < 200ms)
- Connection Pool Usage: [X]% (Target: < 80%)

Repositories Migrated: [X] / 23
- ✅ CompanyRepository
- ✅ BranchRepository
- ✅ UserRepository
[... list enabled repositories ...]

Issues: None

Next Steps:
- Continue Phase [X] monitoring
- Proceed to Phase [X+1] in [X] minutes

Dashboard: [URL]

ThinkOnErp DevOps Team
```

#### Phase Completion Notifications

**To**: Technical Teams  
**Method**: Slack (#deployment-status)

```
✅ **Phase 1 Complete**

Duration: 2 hours
Repositories Enabled: Company, Branch, Currency (3/23)
Traffic Distribution: 95% Blue / 5% Green

Validation Results:
✅ Error rate: 0.04% (Target: < 0.1%)
✅ Response time p95: 420ms (Target: < 500ms)
✅ Database query time: 165ms (Target: < 200ms)
✅ Connection pool: 68% (Target: < 80%)
✅ No issues detected

Decision: ✅ Proceed to Phase 2

Next Phase:
- Add FiscalYear, User, Role, Permission repositories
- Shift traffic to 30% Green
- Monitor for 30 minutes

Stand by for Phase 2 initiation...
```

---

### 12.5 Issue Communication

#### Warning Level Issue

**To**: Technical Teams  
**Method**: Slack (#deployment-status)

```
⚠️ **Warning: Elevated Response Time**

Metric: Response Time p95
Current Value: 650ms
Threshold: 500ms (warning), 1000ms (critical)
Duration: 3 minutes

Status: Monitoring
Action: Investigating

Details:
- Spike detected in /api/users endpoint
- Database query time normal
- Connection pool usage normal
- No errors detected

Next Steps:
- Continue monitoring for 5 minutes
- Investigate query performance
- Prepare for rollback if exceeds critical threshold

Team: @devops-team investigating
```

#### Critical Issue - Rollback Initiated

**To**: All Stakeholders  
**Method**: Email + Slack + Phone (Leadership)

```
Subject: [CRITICAL] EF Core Migration Deployment - ROLLBACK INITIATED

Team,

A critical issue has been detected and rollback has been initiated.

Status: 🔴 ROLLBACK IN PROGRESS
Issue: [Description of issue]
Detected: [Time]
Rollback Initiated: [Time]
Expected Completion: 5 minutes

Issue Details:
- Metric: [Metric name]
- Value: [Value]
- Threshold: [Threshold]
- Impact: [Description]

Rollback Actions:
1. ✅ Feature flags disabled
2. ⏳ Traffic shifting to Blue environment
3. ⏳ Verification in progress

System Status:
- Application remains available
- Users may experience brief delays
- No data loss

Updates:
- Real-time updates in #deployment-status
- Next update in 5 minutes

Contact:
- Deployment Lead: [Name] - [Phone]
- Escalation: [Name] - [Phone]

ThinkOnErp DevOps Team
```

#### Rollback Complete

**To**: All Stakeholders  
**Method**: Email + Slack

```
Subject: EF Core Migration Deployment - ROLLBACK COMPLETE

Team,

The rollback has been completed successfully. The system is stable and operating normally on the ADO.NET (Blue) environment.

Status: ✅ ROLLBACK COMPLETE
Duration: 4 minutes
System Status: Stable

Rollback Summary:
- All EF Core feature flags disabled
- 100% traffic on Blue environment (ADO.NET)
- System metrics returned to baseline
- No data loss or corruption

Current Metrics:
- Error Rate: 0.05% (baseline)
- Response Time p95: 445ms (baseline)
- Database Query Time: 178ms (baseline)
- Connection Pool: 64% (baseline)

Root Cause:
[Brief description - detailed analysis to follow]

Next Steps:
1. Root cause analysis in progress
2. Incident report will be published within 24 hours
3. Fix and retest in development environment
4. Schedule retry deployment after validation

Impact:
- No user impact
- No data loss
- System operating normally

We apologize for the disruption and appreciate your patience.

Contact:
- Deployment Lead: [Name] - [Email]
- DevOps Team: devops@thinkonerp.com

ThinkOnErp DevOps Team
```

---

### 12.6 Post-Deployment Communication

#### Deployment Complete

**To**: All Stakeholders  
**Method**: Email + Slack

```
Subject: ✅ EF Core Migration Deployment - COMPLETE

Team,

We are pleased to announce that the EF Core migration deployment has been completed successfully!

Status: ✅ COMPLETE
Duration: 6 hours 15 minutes
Final Traffic: 100% Green (EF Core)
Rollbacks: 0

Deployment Summary:
- All 23 repositories migrated to EF Core
- Zero downtime achieved
- No user-reported issues
- Performance within target thresholds

Final Metrics:
- Error Rate: 0.06% (Target: < 0.1%) ✅
- Response Time p95: 465ms (Target: < 500ms) ✅
- Database Query Time: 185ms (Target: < 200ms) ✅
- Connection Pool: 71% (Target: < 80%) ✅

Phase Timeline:
- Phase 0: Pre-deployment (1 hour)
- Phase 1: Pilot - 5% traffic (2 hours)
- Phase 2: Expansion - 30% traffic (2 hours)
- Phase 3: Full migration - 70% traffic (2 hours)
- Phase 4: Completion - 100% traffic (15 minutes)

What's Next:
- 24-hour monitoring period
- Post-deployment validation
- User acceptance testing
- Post-mortem meeting scheduled for [Date]

The system is now running on Entity Framework Core, providing improved performance, maintainability, and a foundation for future enhancements.

Thank you to everyone involved in making this deployment a success:
- Development Team
- DevOps Team
- DBA Team
- Support Team
- Business Stakeholders

Detailed post-deployment report will be published within 48 hours.

Congratulations team!

ThinkOnErp DevOps Team
```

#### 24-Hour Validation Complete

**To**: All Stakeholders  
**Method**: Email

```
Subject: EF Core Migration - 24-Hour Validation Complete

Team,

The 24-hour validation period following the EF Core migration deployment has been completed successfully.

Validation Period: [Start Time] - [End Time]
Status: ✅ ALL CHECKS PASSED

Validation Results:

Performance Metrics (24-hour average):
- Error Rate: 0.05% ✅
- Response Time p95: 458ms ✅
- Database Query Time p95: 182ms ✅
- Connection Pool Usage: 69% ✅

Stability:
- No performance degradation over time ✅
- No memory leaks detected ✅
- Connection pool stable ✅
- No unexpected errors ✅

Functional Testing:
- All CRUD operations validated ✅
- Authentication and authorization working ✅
- Audit logging active ✅
- File uploads working ✅

Data Integrity:
- Record counts verified ✅
- No orphaned records ✅
- Referential integrity maintained ✅
- Audit logs complete ✅

User Feedback:
- No critical issues reported ✅
- Performance acceptable to users ✅
- Functionality working as expected ✅

Next Steps:
1. Continue monitoring for 7 days
2. Schedule Blue environment decommissioning
3. Publish final deployment report
4. Conduct post-mortem meeting

The migration is considered successful. The Blue environment (ADO.NET) will be maintained for 7 days as a precaution, then decommissioned.

Thank you for your support throughout this deployment.

ThinkOnErp DevOps Team
```

#### Post-Mortem Meeting Invitation

**To**: All Stakeholders  
**Method**: Calendar Invite + Email

```
Subject: EF Core Migration Deployment - Post-Mortem Meeting

Team,

Please join us for a post-mortem meeting to discuss the EF Core migration deployment.

Date: [Date]
Time: [Time]
Duration: 1 hour
Location: [Meeting Room / Video Conference Link]

Agenda:
1. Deployment overview and timeline (10 min)
2. What went well (15 min)
3. What could be improved (15 min)
4. Lessons learned (10 min)
5. Action items (10 min)

Please come prepared to discuss:
- Your experience during the deployment
- Any challenges encountered
- Suggestions for future deployments
- Questions or concerns

Materials:
- Deployment timeline
- Metrics and performance data
- Issue log
- Post-deployment report

Looking forward to your participation.

ThinkOnErp DevOps Team
```

---

### 12.7 Communication Templates

#### Slack Status Update Template

```markdown
**EF Core Deployment Status**

⏰ Time: [HH:MM]
📊 Phase: [Phase Name]
🚦 Status: [On Track / Warning / Critical]
📈 Progress: [X]%

**Metrics:**
• Error Rate: [X]% [✅/⚠️/🔴]
• Response Time: [X]ms [✅/⚠️/🔴]
• DB Query Time: [X]ms [✅/⚠️/🔴]
• Pool Usage: [X]% [✅/⚠️/🔴]

**Traffic:** [X]% Blue / [X]% Green

**Repositories:** [X]/23 migrated

**Issues:** [None / Description]

**Next:** [Next action]

Dashboard: [URL]
```

#### Email Status Update Template

```
Subject: EF Core Migration - [Status] - [Time]

Status: [On Track / Warning / Critical / Complete]
Phase: [Phase Name]
Progress: [X]%
Time Elapsed: [X] hours

METRICS
-------
Error Rate: [X]% (Target: < 0.1%)
Response Time p95: [X]ms (Target: < 500ms)
Database Query Time: [X]ms (Target: < 200ms)
Connection Pool: [X]% (Target: < 80%)

PROGRESS
--------
Traffic Distribution: [X]% Blue / [X]% Green
Repositories Migrated: [X] / 23

ISSUES
------
[None / List of issues]

NEXT STEPS
----------
[Description of next actions]

DASHBOARD
---------
[URL]

Contact: [Name] - [Email] - [Phone]
```

---

### 12.8 Communication Channels

#### Primary Channels

1. **Email**
   - Distribution lists:
     - all-stakeholders@thinkonerp.com
     - technical-team@thinkonerp.com
     - leadership@thinkonerp.com
   - Use for: Formal updates, pre/post deployment, issues

2. **Slack**
   - Channels:
     - #deployment-status (real-time updates)
     - #devops-team (team coordination)
     - #incidents (issue tracking)
   - Use for: Real-time updates, team coordination

3. **Phone**
   - On-call numbers:
     - Deployment Lead: [Phone]
     - DevOps On-Call: [Phone]
     - Escalation: [Phone]
   - Use for: Critical issues, escalations

4. **Dashboard**
   - URL: https://portal.azure.com/#@thinkonerp/dashboard/deployment
   - Use for: Real-time metrics, visual status

#### Escalation Path

```
Level 1: Deployment Lead
  ↓ (if unresolved in 15 minutes)
Level 2: DevOps Manager
  ↓ (if critical impact)
Level 3: CTO
  ↓ (if business impact)
Level 4: CEO
```

---

### 12.9 Communication Schedule

| Time | Audience | Method | Content |
|------|----------|--------|---------|
| T-1 week | All Stakeholders | Email | Initial announcement |
| T-24 hours | All Stakeholders | Email + Slack | Deployment reminder |
| T-1 hour | Technical Teams | Slack | Deployment starting soon |
| T+0 | All Stakeholders | Email + Slack | Deployment initiated |
| T+1h, T+2h, etc. | All Stakeholders | Email + Slack | Hourly status updates |
| Phase completions | Technical Teams | Slack | Phase completion notifications |
| Issues | Relevant Teams | Slack + Email | Issue notifications |
| T+6h | All Stakeholders | Email + Slack | Deployment complete |
| T+24h | All Stakeholders | Email | 24-hour validation complete |
| T+48h | All Stakeholders | Email | Post-deployment report |
| T+1 week | All Stakeholders | Calendar + Email | Post-mortem meeting |

---

### 12.10 Communication Best Practices

1. **Be Transparent**
   - Share both successes and challenges
   - Provide honest assessments
   - Don't hide issues

2. **Be Timely**
   - Send updates on schedule
   - Notify immediately of critical issues
   - Don't delay bad news

3. **Be Clear**
   - Use simple language
   - Avoid jargon for non-technical audiences
   - Provide context and impact

4. **Be Consistent**
   - Use standard templates
   - Maintain regular update schedule
   - Use consistent terminology

5. **Be Actionable**
   - Clearly state what's needed from recipients
   - Provide contact information
   - Include links to resources

6. **Be Appropriate**
   - Match communication level to audience
   - Use appropriate channels
   - Escalate when necessary

---

### 12.11 Communication Checklist

- [ ] **Pre-Deployment**
  - [ ] T-1 week announcement sent
  - [ ] T-24 hour reminder sent
  - [ ] T-1 hour notification sent
  - [ ] All stakeholders informed
  - [ ] Contact information distributed

- [ ] **During Deployment**
  - [ ] Deployment initiated notification sent
  - [ ] Hourly status updates sent
  - [ ] Phase completions communicated
  - [ ] Issues communicated promptly
  - [ ] Dashboard accessible

- [ ] **Post-Deployment**
  - [ ] Deployment complete notification sent
  - [ ] 24-hour validation results shared
  - [ ] Post-deployment report published
  - [ ] Post-mortem meeting scheduled
  - [ ] Lessons learned documented

- [ ] **Continuous**
  - [ ] Slack channels monitored
  - [ ] Email responses timely
  - [ ] Escalations handled appropriately
  - [ ] Stakeholders kept informed

---

## Conclusion

This deployment plan provides a comprehensive strategy for migrating ThinkOnErp from ADO.NET to Entity Framework Core with zero downtime. The plan includes:

- ✅ Blue-green deployment architecture
- ✅ Gradual traffic shifting strategy
- ✅ Comprehensive health checks
- ✅ Automated rollback mechanisms
- ✅ Connection pool warm-up procedures
- ✅ Post-deployment validation
- ✅ Communication plan

**Key Success Factors:**
1. Thorough preparation and testing
2. Gradual, controlled rollout
3. Continuous monitoring
4. Rapid rollback capability
5. Clear communication
6. Team coordination

**Estimated Timeline:**
- Deployment: 6 hours
- Validation: 24-48 hours
- Stability period: 7 days
- Blue decommission: After 7 days

With proper execution of this plan, the EF Core migration will be completed successfully with zero downtime and minimal risk.

---

**Document Version**: 2.0  
**Last Updated**: [Date]  
**Next Review**: After deployment completion  
**Owner**: DevOps Team  
**Approvers**: CTO, DevOps Manager, DBA Lead
