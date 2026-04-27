# Quick Start Guide - Load Testing

Get started with load testing in 5 minutes!

## Prerequisites

- ThinkOnErp API running at `http://localhost:5000`
- One of the following tools installed:
  - **k6** (recommended) - [Install Guide](https://k6.io/docs/getting-started/installation/)
  - **Apache Bench** (ab) - Usually pre-installed on macOS/Linux
  - **PowerShell** - Pre-installed on Windows

## Quick Start Options

### Option 1: k6 (Recommended) ⭐

**Best for:** Comprehensive load testing with detailed metrics

```bash
# 1. Install k6 (if not already installed)
brew install k6  # macOS
# OR
choco install k6  # Windows

# 2. Navigate to load tests directory
cd tests/LoadTests

# 3. Run the load test
k6 run load-test-10k-rpm.js
```

**What it does:**
- Ramps up to 10,000 requests/minute over 10 minutes
- Sustains 10,000 req/min for 10 minutes
- Tests multiple API endpoints
- Validates performance thresholds
- Generates detailed metrics report

**Duration:** ~23 minutes

---

### Option 2: Bash Script (Simple)

**Best for:** Quick validation on macOS/Linux

```bash
# 1. Navigate to load tests directory
cd tests/LoadTests

# 2. Make script executable
chmod +x load-test-simple.sh

# 3. Run the test
./load-test-simple.sh
```

**What it does:**
- Tests each endpoint individually
- Runs 60 seconds per endpoint
- Generates summary report
- Saves results to `results/` directory

**Duration:** ~6 minutes (6 endpoints × 60 seconds)

---

### Option 3: PowerShell (Windows)

**Best for:** Windows users without k6

```powershell
# 1. Navigate to load tests directory
cd tests\LoadTests

# 2. Run the test
.\load-test-simple.ps1
```

**What it does:**
- Tests each endpoint individually
- Runs 60 seconds per endpoint
- Generates summary report
- Saves results to `results/` directory

**Duration:** ~6 minutes (6 endpoints × 60 seconds)

---

## Understanding the Results

### ✅ Success Indicators

Look for these in the output:

```
✓ p99 < 10ms          # Audit overhead is acceptable
✓ p99 < 500ms         # Total response time is good
✓ error_rate < 1%     # System is stable
✓ throughput ≥ 10k    # Target load achieved
```

### ⚠️ Warning Signs

If you see these, investigate further:

```
✗ p99 > 10ms          # Audit logging is too slow
✗ p99 > 500ms         # API performance degraded
✗ error_rate > 1%     # System is unstable
✗ throughput < 10k    # Target load not achieved
```

---

## Common Issues & Quick Fixes

### Issue: "Authentication failed"

**Fix:**
```bash
# Verify API is running
curl http://localhost:5000/api/health

# Check superadmin credentials in database
# Default: username=superadmin, password=SuperAdmin123!
```

### Issue: "Connection refused"

**Fix:**
```bash
# Start the API
cd src/ThinkOnErp.API
dotnet run

# Wait 30 seconds for startup, then run test again
```

### Issue: "High error rate"

**Fix:**
1. Check application logs: `tail -f logs/log-*.txt`
2. Check database connection pool settings
3. Verify database is running and accessible

### Issue: "Slow response times"

**Fix:**
1. Check database performance
2. Verify indexes are in place
3. Monitor system resources (CPU, memory)
4. Check for slow queries in database

---

## Next Steps

After your first successful test:

1. **Review the full documentation:** `README.md`
2. **Check the results:** `results/summary-*.txt`
3. **Monitor the application:** Check logs and database
4. **Optimize if needed:** See troubleshooting guide in README.md
5. **Schedule regular tests:** Add to CI/CD pipeline

---

## Need Help?

- **Full Documentation:** See `README.md` in this directory
- **Implementation Details:** See `TASK_11_5_LOAD_TESTING_IMPLEMENTATION.md`
- **k6 Documentation:** https://k6.io/docs/
- **Troubleshooting:** See "Troubleshooting" section in `README.md`

---

## Quick Reference

### k6 Commands

```bash
# Basic run
k6 run load-test-10k-rpm.js

# Custom API URL
k6 run --env API_URL=http://your-api:port load-test-10k-rpm.js

# Save results
k6 run --out json=results.json load-test-10k-rpm.js

# Shorter test (5 minutes)
k6 run --vus 100 --duration 5m load-test-10k-rpm.js
```

### Bash Script Commands

```bash
# Basic run
./load-test-simple.sh

# Custom API URL and token
./load-test-simple.sh http://localhost:5000 "your-jwt-token"
```

### PowerShell Commands

```powershell
# Basic run
.\load-test-simple.ps1

# Custom parameters
.\load-test-simple.ps1 -ApiUrl "http://localhost:5000" -JwtToken "your-token"

# Adjust concurrency and duration
.\load-test-simple.ps1 -Concurrency 100 -DurationSeconds 120
```

---

**Happy Load Testing! 🚀**
