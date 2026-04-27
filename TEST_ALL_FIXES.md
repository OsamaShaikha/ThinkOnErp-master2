# Quick Test Guide - All Runtime Error Fixes

## 🚀 Quick Start

1. **Restart the application** (required!)
2. **Get a fresh token** (old one may be expired)
3. **Run the tests below**

---

## 1️⃣ Get Fresh Authentication Token

```bash
curl -X POST "https://localhost:7136/api/Auth/login" \
  -H "Content-Type: application/json" \
  -d "{\"username\":\"moe\",\"password\":\"Admin@123\"}" \
  -k
```

Copy the `token` value from the response and use it in tests below (replace `YOUR_TOKEN`).

---

## 2️⃣ Test Error 5 Fix: Legacy Audit API

**What was fixed**: Added missing columns (ACTOR_NAME, CORRELATION_ID, ENDPOINT_PATH, USER_AGENT, IP_ADDRESS)

```bash
curl -X GET "https://localhost:7136/api/auditlogs/legacy?pageNumber=1&pageSize=50" \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -k
```

**Expected Result**: 
- ✅ HTTP 200 OK
- ✅ JSON response with audit logs
- ✅ Each log has: `id`, `errorDescription`, `module`, `company`, `branch`, `user`, `device`, `dateTime`, `status`, `errorCode`, `correlationId`

**Before Fix**: HTTP 500 - "Unable to find specified column in result set"

---

## 3️⃣ Test Error 4 Fix: Company API

**What was fixed**: Added `HAS_LOGO` column to company procedures

```bash
curl -X GET "https://localhost:7136/api/companies" \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -k
```

**Expected Result**:
- ✅ HTTP 200 OK
- ✅ Each company has `hasLogo` field (true/false)

**Before Fix**: HTTP 500 - "Unable to find specified column in result set"

---

## 4️⃣ Test Error 3 Fix: Anonymous Audit Logging

**What was fixed**: Added 'ANONYMOUS' and 'SYSTEM' to allowed actor types

```bash
# Try to login with invalid credentials
curl -X POST "https://localhost:7136/api/Auth/login" \
  -H "Content-Type: application/json" \
  -d "{\"username\":\"invalid\",\"password\":\"wrong\"}" \
  -k
```

**Expected Result**:
- ✅ HTTP 401 Unauthorized (this is correct)
- ✅ No errors in application logs
- ✅ Audit log created with ACTOR_TYPE = 'ANONYMOUS'

**Before Fix**: HTTP 500 - "check constraint (CHK_AUDIT_LOG_ACTOR_TYPE) violated"

---

## 5️⃣ Test Errors 1-2 Fix: SLA Services

**What was fixed**: Created missing SLA procedures

### Check Application Logs

After restarting, look for these log entries (appear every 30 minutes):

```
[INF] Starting SLA escalation check
[DBG] Found X tickets approaching SLA deadline within 2 hours
[DBG] Found X overdue tickets
[INF] Found X tickets requiring SLA escalation
[INF] SLA escalation check completed. Processed X tickets
```

**Expected Result**:
- ✅ No errors about missing procedures
- ✅ SLA check completes successfully (even if X = 0)

**Before Fix**: "identifier 'SP_SYS_REQUEST_TICKET_SELECT_APPROACHING_SLA' must be declared"

---

## 📊 All Tests Summary

| Test | Endpoint | Expected | Status |
|------|----------|----------|--------|
| Legacy Audit | GET /api/auditlogs/legacy | 200 OK | ⏳ Test |
| Company List | GET /api/companies | 200 OK with hasLogo | ⏳ Test |
| Failed Login | POST /api/Auth/login (invalid) | 401 (not 500) | ⏳ Test |
| SLA Services | Check logs | No procedure errors | ⏳ Test |

---

## 🔍 Verify Database Procedures

```sql
-- Connect to database
sqlplus THINKON_ERP/THINKON_ERP@178.104.126.99:1521/XEPDB1

-- Check all procedures are VALID
SELECT object_name, status
FROM user_objects
WHERE object_name IN (
    'SP_SYS_REQUEST_TICKET_SELECT_APPROACHING_SLA',
    'SP_SYS_REQUEST_TICKET_SELECT_OVERDUE_BY_TIME',
    'SP_SYS_COMPANY_SELECT_ALL',
    'SP_SYS_COMPANY_SELECT_BY_ID',
    'SP_SYS_AUDIT_LOG_LEGACY_SELECT'
)
ORDER BY object_name;
```

**Expected**: All 5 procedures show `STATUS = VALID`

---

## 🐛 Troubleshooting

### Still Getting 500 Errors?

1. **Did you restart the application?**
   - Stop it completely (Ctrl+C)
   - Start it again (`dotnet run` or F5 in Visual Studio)

2. **Is your token expired?**
   - Get a fresh token (see step 1 above)
   - Tokens expire after a certain time

3. **Are procedures VALID in database?**
   - Run the SQL query above
   - All should show STATUS = VALID

4. **Check application logs**
   - Look for specific error messages
   - Share the error details if still failing

### Token Expired Error?

If you get "Unauthorized" on authenticated endpoints:
- Your token has expired
- Get a fresh token using the login endpoint (step 1)

### Connection Refused?

If you get "Connection refused":
- Application is not running
- Start the application first

---

## 📝 Test Results Template

Copy this and fill in your results:

```
Test Date: ___________
Application Restarted: [ ] Yes [ ] No

✅ = Pass, ❌ = Fail

[ ] Legacy Audit API - GET /api/auditlogs/legacy
    Result: HTTP ___ 
    Notes: ___________

[ ] Company API - GET /api/companies  
    Result: HTTP ___
    Has hasLogo field: [ ] Yes [ ] No
    Notes: ___________

[ ] Failed Login - POST /api/Auth/login (invalid)
    Result: HTTP ___
    Constraint error in logs: [ ] Yes [ ] No
    Notes: ___________

[ ] SLA Services - Check logs
    Procedure errors: [ ] Yes [ ] No
    Check completed: [ ] Yes [ ] No
    Notes: ___________

[ ] Database Procedures
    All VALID: [ ] Yes [ ] No
    Notes: ___________
```

---

## ✅ Success Criteria

All fixes are working if:

1. ✅ Legacy Audit API returns 200 (not 500)
2. ✅ Company API returns 200 with `hasLogo` field
3. ✅ Failed login returns 401 (not 500) with no constraint errors
4. ✅ SLA services run without procedure errors
5. ✅ All 5 database procedures show STATUS = VALID

---

## 🎯 Next Steps After Testing

Once all tests pass:

1. ✅ Mark all errors as resolved
2. ✅ Update documentation
3. ✅ Deploy to production (if applicable)
4. ✅ Monitor logs for any new issues

---

**Remember**: You MUST restart the application for the database changes to take effect!
