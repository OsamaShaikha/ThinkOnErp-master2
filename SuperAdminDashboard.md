# 🧠 SuperAdmin Dashboard Feature (ASP.NET Core + Clean Architecture + CQRS + Oracle)

## 🎯 Goal

Create a **high-performance dashboard API** for `SuperAdmin` that returns all required data in **ONE optimized request**, matching the dashboard shown in the reference image.

---

## ⚠️ VERY IMPORTANT (READ FIRST)

Before implementing anything:

> You MUST first analyze the existing project structure, database schema, and current patterns.

### 🔍 Step 0: Analyze Existing Project

* Scan all layers:

  * Domain
  * Application
  * Infrastructure
  * API

* Identify:

  * Existing Entities (Company, Branch, User, Request, etc.)
  * Existing Repository implementations (ADO.NET style)
  * Naming conventions
  * Stored procedures already in use
  * Response/Result patterns

* DO NOT duplicate logic

* DO NOT break existing architecture

* FOLLOW existing patterns strictly

---

## 🧩 Dashboard Requirements

### 📊 Metrics (Top Cards)

Return the following:

* Total Companies
* Active Companies
* Inactive Companies
* Total Branches
* Active Branches
* Pending Requests
* Total System Admins

---

### 🚨 System Alerts

* Inactive companies count > 0 → show warning
* Pending requests > 0 → require action

---

### 🏢 Recently Created Companies

Return:

* Company Name
* Country
* Number of Branches
* Status (Active / Pending)
* Created Date

Limit: Top 5 (latest)

---

### 🏬 Recent Branch Activity

Return:

* Branch Name
* Company Name
* Activity Type (New / Update)
* Date

Limit: Top 5

---

### 📥 Pending Requests

Return:

* Company Name
* Request Type
* Priority (High / Medium / Low)
* Date

Limit: Top 5

---

## 🧱 Architecture Design (CQRS)

### 📁 Application Layer

#### Query

```csharp
public class GetSuperAdminDashboardQuery : IRequest<DashboardDto>
{
}
```

---

#### DTOs

```csharp
public class DashboardDto
{
    public DashboardStatsDto Stats { get; set; }
    public List<CompanyDto> RecentCompanies { get; set; }
    public List<BranchActivityDto> RecentBranches { get; set; }
    public List<RequestDto> PendingRequests { get; set; }
    public List<string> Alerts { get; set; }
}

public class DashboardStatsDto
{
    public int TotalCompanies { get; set; }
    public int ActiveCompanies { get; set; }
    public int InactiveCompanies { get; set; }
    public int TotalBranches { get; set; }
    public int ActiveBranches { get; set; }
    public int PendingRequests { get; set; }
    public int TotalAdmins { get; set; }
}
```

---

#### Handler

```csharp
public class GetSuperAdminDashboardHandler 
    : IRequestHandler<GetSuperAdminDashboardQuery, DashboardDto>
{
    private readonly IDashboardRepository _repo;

    public GetSuperAdminDashboardHandler(IDashboardRepository repo)
    {
        _repo = repo;
    }

    public async Task<DashboardDto> Handle(
        GetSuperAdminDashboardQuery request,
        CancellationToken cancellationToken)
    {
        var result = await _repo.GetDashboardDataAsync();

        // Build alerts
        var alerts = new List<string>();

        if (result.Stats.InactiveCompanies > 0)
            alerts.Add($"{result.Stats.InactiveCompanies} inactive companies detected");

        if (result.Stats.PendingRequests > 0)
            alerts.Add($"{result.Stats.PendingRequests} pending requests require action");

        result.Alerts = alerts;

        return result;
    }
}
```

---

## 🏗️ Infrastructure Layer (ADO.NET + Oracle)

### 📌 Interface

```csharp
public interface IDashboardRepository
{
    Task<DashboardDto> GetDashboardDataAsync();
}
```

---

### ⚡ PERFORMANCE STRATEGY (IMPORTANT)

* Use **ONE stored procedure**
* Return **multiple result sets (REF CURSOR)**
* Avoid multiple DB calls ❌
* Minimize network round trips ✅

---

### 🧾 Stored Procedure (Oracle)

```sql
CREATE OR REPLACE PROCEDURE GET_SUPERADMIN_DASHBOARD
(
    p_stats OUT SYS_REFCURSOR,
    p_companies OUT SYS_REFCURSOR,
    p_branches OUT SYS_REFCURSOR,
    p_requests OUT SYS_REFCURSOR
)
AS
BEGIN

-- Stats
OPEN p_stats FOR
SELECT
    (SELECT COUNT(*) FROM COMPANIES) AS TOTAL_COMPANIES,
    (SELECT COUNT(*) FROM COMPANIES WHERE IS_ACTIVE = 1) AS ACTIVE_COMPANIES,
    (SELECT COUNT(*) FROM COMPANIES WHERE IS_ACTIVE = 0) AS INACTIVE_COMPANIES,
    (SELECT COUNT(*) FROM BRANCHES) AS TOTAL_BRANCHES,
    (SELECT COUNT(*) FROM BRANCHES WHERE IS_ACTIVE = 1) AS ACTIVE_BRANCHES,
    (SELECT COUNT(*) FROM REQUESTS WHERE STATUS = 'PENDING') AS PENDING_REQUESTS,
    (SELECT COUNT(*) FROM USERS WHERE ROLE = 'ADMIN') AS TOTAL_ADMINS
FROM DUAL;

-- Recent Companies
OPEN p_companies FOR
SELECT * FROM (
    SELECT c.NAME, c.COUNTRY, c.CREATED_DATE, c.STATUS
    FROM COMPANIES c
    ORDER BY c.CREATED_DATE DESC
) WHERE ROWNUM <= 5;

-- Branch Activity
OPEN p_branches FOR
SELECT * FROM (
    SELECT b.NAME, c.NAME AS COMPANY_NAME, b.UPDATED_DATE
    FROM BRANCHES b
    JOIN COMPANIES c ON c.ID = b.COMPANY_ID
    ORDER BY b.UPDATED_DATE DESC
) WHERE ROWNUM <= 5;

-- Pending Requests
OPEN p_requests FOR
SELECT * FROM (
    SELECT r.TYPE, r.PRIORITY, r.CREATED_DATE, c.NAME AS COMPANY_NAME
    FROM REQUESTS r
    JOIN COMPANIES c ON c.ID = r.COMPANY_ID
    WHERE r.STATUS = 'PENDING'
    ORDER BY r.CREATED_DATE DESC
) WHERE ROWNUM <= 5;

END;
```

---

### ⚙️ Repository Implementation (ADO.NET)

```csharp
public async Task<DashboardDto> GetDashboardDataAsync()
{
    using var conn = new OracleConnection(_connectionString);
    using var cmd = new OracleCommand("GET_SUPERADMIN_DASHBOARD", conn);

    cmd.CommandType = CommandType.StoredProcedure;

    cmd.Parameters.Add("p_stats", OracleDbType.RefCursor, ParameterDirection.Output);
    cmd.Parameters.Add("p_companies", OracleDbType.RefCursor, ParameterDirection.Output);
    cmd.Parameters.Add("p_branches", OracleDbType.RefCursor, ParameterDirection.Output);
    cmd.Parameters.Add("p_requests", OracleDbType.RefCursor, ParameterDirection.Output);

    await conn.OpenAsync();

    using var reader = await cmd.ExecuteReaderAsync();

    var dashboard = new DashboardDto();

    // 1. Stats
    if (await reader.ReadAsync())
    {
        dashboard.Stats = new DashboardStatsDto
        {
            TotalCompanies = reader.GetInt32(0),
            ActiveCompanies = reader.GetInt32(1),
            InactiveCompanies = reader.GetInt32(2),
            TotalBranches = reader.GetInt32(3),
            ActiveBranches = reader.GetInt32(4),
            PendingRequests = reader.GetInt32(5),
            TotalAdmins = reader.GetInt32(6)
        };
    }

    // 2. Move next result
    await reader.NextResultAsync();

    // Map Companies...
    // Map Branches...
    // Map Requests...

    return dashboard;
}
```

---

## 🌐 API Layer

```csharp
[Authorize(Roles = "SuperAdmin")]
[ApiController]
[Route("api/superadmin/dashboard")]
public class DashboardController : ControllerBase
{
    private readonly IMediator _mediator;

    public DashboardController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var result = await _mediator.Send(new GetSuperAdminDashboardQuery());
        return Ok(result);
    }
}
```

---

## 🔐 Security

* Ensure only **SuperAdmin** can access
* Validate token in middleware
* Never expose raw DB fields

---

## 🚀 Performance Best Practices

* ✅ Single DB call (critical)
* ✅ Use indexes on:

  * COMPANIES.IS_ACTIVE
  * REQUESTS.STATUS
* ✅ Limit results (TOP 5)
* ✅ Avoid SELECT *

---

## 🧪 Testing Checklist

* API returns all sections correctly
* Works with empty data
* Handles large data efficiently
* Response time < 300ms

---

## 🧠 Final Instruction for Claude (AUTO-ANALYSIS MODE)

Before generating code:

> Read my existing project files and:
>
> * Reuse my entities
> * Reuse naming conventions
> * Reuse repository patterns
> * Adapt stored procedure to my schema
> * Optimize queries based on my indexes

Then generate the final implementation.

---

## ✅ Final Result

You will have:

* One powerful API endpoint
* Clean Architecture compliant
* High performance Oracle integration
* Fully scalable dashboard backend
