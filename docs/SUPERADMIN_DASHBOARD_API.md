# SuperAdmin Dashboard API Documentation

## Overview

The SuperAdmin Dashboard API provides a high-performance, single-request endpoint that delivers comprehensive system metrics, alerts, and activity summaries for SuperAdmin users. This endpoint aggregates data from multiple sources and returns a complete dashboard view in under 500ms.

## Endpoint

### Get SuperAdmin Dashboard

Retrieves all dashboard data including system metrics, recent activity, and alerts in a single optimized request.

**Endpoint:** `GET /api/superadmin/dashboard`

**Authentication:** Required (JWT Bearer Token)

**Authorization:** SuperAdmin role required

**Response Time:** < 500ms

## Authentication Requirements

### JWT Token

All requests to the SuperAdmin Dashboard API must include a valid JWT token in the Authorization header.

**Header Format:**
```
Authorization: Bearer {jwt_token}
```

### Role Requirements

- The authenticated user must have the **SuperAdmin** role
- The JWT token must contain a valid role claim with value "SuperAdmin"
- Non-SuperAdmin users will receive a 403 Forbidden response
- Unauthenticated requests will receive a 401 Unauthorized response

### Token Validation

The API validates:
1. Token signature and expiration
2. Token issuer and audience
3. SuperAdmin role claim presence
4. User active status

## Request

### HTTP Method
```
GET /api/superadmin/dashboard
```

### Headers
```
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
Content-Type: application/json
```

### Query Parameters

None required. The endpoint returns all dashboard data without filtering.

### Request Example

```bash
curl -X GET "https://api.example.com/api/superadmin/dashboard" \
  -H "Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..." \
  -H "Content-Type: application/json"
```

## Response Structure

### Success Response (200 OK)

The API returns a JSON object containing five main sections:

1. **stats** - Aggregated system metrics
2. **recentCompanies** - Top 4 recently created companies
3. **recentBranches** - Top 4 recent branch activities
4. **pendingRequests** - Pending actions (future feature, currently empty)
5. **alerts** - System alerts requiring attention

### Response Schema

```json
{
  "stats": {
    "totalCompanies": 150,
    "activeCompanies": 142,
    "inactiveCompanies": 8,
    "totalBranches": 487,
    "activeBranches": 465,
    "totalSystemAdmins": 12
  },
  "recentCompanies": [
    {
      "nameAr": "شركة المثال",
      "nameEn": "Example Company",
      "country": "Saudi Arabia",
      "branchCount": 5,
      "status": "Active",
      "createdDate": "2024-01-15T10:30:00Z"
    }
  ],
  "recentBranches": [
    {
      "branchNameAr": "الفرع الرئيسي",
      "branchNameEn": "Main Branch",
      "companyNameAr": "شركة المثال",
      "companyNameEn": "Example Company",
      "activityType": "New",
      "activityDate": "2024-01-15T14:20:00Z"
    }
  ],
  "pendingRequests": [],
  "alerts": [
    "8 companies are currently inactive"
  ]
}
```

### Field Descriptions

#### Stats Object

| Field | Type | Description | Requirements |
|-------|------|-------------|--------------|
| `totalCompanies` | integer | Total number of companies in the system | Req 1.4, 4.2 |
| `activeCompanies` | integer | Number of companies with IsActive = true | Req 1.4, 4.3 |
| `inactiveCompanies` | integer | Number of companies with IsActive = false | Req 1.4, 4.4 |
| `totalBranches` | integer | Total number of branches in the system | Req 1.4, 5.2 |
| `activeBranches` | integer | Number of branches with IsActive = true | Req 1.4, 5.3 |
| `totalSystemAdmins` | integer | Number of users with SuperAdmin role | Req 1.4, 6.2 |

#### Recent Companies Array

Each company object contains:

| Field | Type | Description | Requirements |
|-------|------|-------------|--------------|
| `nameAr` | string | Company name in Arabic | Req 7.3 |
| `nameEn` | string | Company name in English | Req 7.3 |
| `country` | string (nullable) | Country name if available | Req 7.4 |
| `branchCount` | integer | Number of branches associated with this company | Req 7.5 |
| `status` | string | "Active" or "Inactive" based on IsActive flag | Req 7.6 |
| `createdDate` | datetime | ISO 8601 formatted creation timestamp | Req 7.7 |

**Sorting:** Companies are sorted by creation date in descending order (newest first)

**Limit:** Maximum 4 companies returned

#### Recent Branches Array

Each branch activity object contains:

| Field | Type | Description | Requirements |
|-------|------|-------------|--------------|
| `branchNameAr` | string | Branch name in Arabic | Req 8.3 |
| `branchNameEn` | string | Branch name in English | Req 8.3 |
| `companyNameAr` | string | Associated company name in Arabic | Req 8.4 |
| `companyNameEn` | string | Associated company name in English | Req 8.4 |
| `activityType` | string | "New" (if CreationDate == UpdateDate) or "Update" | Req 8.5 |
| `activityDate` | datetime | ISO 8601 formatted activity timestamp | Req 8.6 |

**Sorting:** Branches are sorted by update date in descending order (most recent first)

**Limit:** Maximum 4 branch activities returned

#### Pending Requests Array

Currently returns an empty array. This field is reserved for future implementation of pending approval requests.

**Future Implementation:** Will contain up to 4 pending requests requiring SuperAdmin action

#### Alerts Array

An array of string messages indicating conditions requiring attention.

**Current Alert Types:**
- Inactive companies alert: "{count} companies are currently inactive"

**Future Alert Types:**
- Pending requests count
- System performance warnings
- Security alerts

## Response Examples

### Example 1: Successful Response with Data

```json
{
  "stats": {
    "totalCompanies": 150,
    "activeCompanies": 142,
    "inactiveCompanies": 8,
    "totalBranches": 487,
    "activeBranches": 465,
    "totalSystemAdmins": 12
  },
  "recentCompanies": [
    {
      "nameAr": "شركة التقنية المتقدمة",
      "nameEn": "Advanced Technology Company",
      "country": "Saudi Arabia",
      "branchCount": 8,
      "status": "Active",
      "createdDate": "2024-01-20T09:15:00Z"
    },
    {
      "nameAr": "مؤسسة التجارة الدولية",
      "nameEn": "International Trade Corporation",
      "country": "United Arab Emirates",
      "branchCount": 3,
      "status": "Active",
      "createdDate": "2024-01-18T14:30:00Z"
    },
    {
      "nameAr": "شركة الخدمات المالية",
      "nameEn": "Financial Services Company",
      "country": "Kuwait",
      "branchCount": 12,
      "status": "Active",
      "createdDate": "2024-01-15T11:45:00Z"
    },
    {
      "nameAr": "مجموعة الصناعات الحديثة",
      "nameEn": "Modern Industries Group",
      "country": "Qatar",
      "branchCount": 5,
      "status": "Inactive",
      "createdDate": "2024-01-12T08:20:00Z"
    }
  ],
  "recentBranches": [
    {
      "branchNameAr": "فرع الرياض الرئيسي",
      "branchNameEn": "Riyadh Main Branch",
      "companyNameAr": "شركة التقنية المتقدمة",
      "companyNameEn": "Advanced Technology Company",
      "activityType": "New",
      "activityDate": "2024-01-20T10:00:00Z"
    },
    {
      "branchNameAr": "فرع جدة",
      "branchNameEn": "Jeddah Branch",
      "companyNameAr": "شركة التقنية المتقدمة",
      "companyNameEn": "Advanced Technology Company",
      "activityType": "New",
      "activityDate": "2024-01-20T10:05:00Z"
    },
    {
      "branchNameAr": "فرع دبي",
      "branchNameEn": "Dubai Branch",
      "companyNameAr": "مؤسسة التجارة الدولية",
      "companyNameEn": "International Trade Corporation",
      "activityType": "Update",
      "activityDate": "2024-01-19T16:30:00Z"
    },
    {
      "branchNameAr": "المركز الرئيسي",
      "branchNameEn": "Head Office",
      "companyNameAr": "شركة الخدمات المالية",
      "companyNameEn": "Financial Services Company",
      "activityType": "Update",
      "activityDate": "2024-01-18T13:15:00Z"
    }
  ],
  "pendingRequests": [],
  "alerts": [
    "8 companies are currently inactive"
  ]
}
```

### Example 2: Response with No Alerts

```json
{
  "stats": {
    "totalCompanies": 50,
    "activeCompanies": 50,
    "inactiveCompanies": 0,
    "totalBranches": 150,
    "activeBranches": 150,
    "totalSystemAdmins": 5
  },
  "recentCompanies": [
    {
      "nameAr": "شركة النجاح",
      "nameEn": "Success Company",
      "country": "Saudi Arabia",
      "branchCount": 2,
      "status": "Active",
      "createdDate": "2024-01-22T12:00:00Z"
    }
  ],
  "recentBranches": [
    {
      "branchNameAr": "الفرع الأول",
      "branchNameEn": "First Branch",
      "companyNameAr": "شركة النجاح",
      "companyNameEn": "Success Company",
      "activityType": "New",
      "activityDate": "2024-01-22T12:30:00Z"
    }
  ],
  "pendingRequests": [],
  "alerts": []
}
```

### Example 3: Response with Empty Data

```json
{
  "stats": {
    "totalCompanies": 0,
    "activeCompanies": 0,
    "inactiveCompanies": 0,
    "totalBranches": 0,
    "activeBranches": 0,
    "totalSystemAdmins": 0
  },
  "recentCompanies": [],
  "recentBranches": [],
  "pendingRequests": [],
  "alerts": []
}
```

## Error Responses

### 401 Unauthorized

**Cause:** Missing or invalid JWT token

**Response:**
```json
{
  "error": "Unauthorized"
}
```

**HTTP Status Code:** 401

**Example:**
```bash
# Request without Authorization header
curl -X GET "https://api.example.com/api/superadmin/dashboard"

# Response
HTTP/1.1 401 Unauthorized
{
  "error": "Unauthorized"
}
```

### 403 Forbidden

**Cause:** Valid JWT token but user does not have SuperAdmin role

**Response:**
```json
{
  "error": "Access denied. SuperAdmin role required."
}
```

**HTTP Status Code:** 403

**Example:**
```bash
# Request with valid token but non-SuperAdmin user
curl -X GET "https://api.example.com/api/superadmin/dashboard" \
  -H "Authorization: Bearer {valid_token_without_superadmin_role}"

# Response
HTTP/1.1 403 Forbidden
{
  "error": "Access denied. SuperAdmin role required."
}
```

### 500 Internal Server Error

**Cause:** Server-side error during data retrieval or processing

**Response:**
```json
{
  "error": "An error occurred while retrieving dashboard data."
}
```

**HTTP Status Code:** 500

**Note:** Error messages are intentionally generic to avoid exposing internal system details. Detailed error information is logged server-side for debugging.

**Example:**
```bash
# Request when database is unavailable
curl -X GET "https://api.example.com/api/superadmin/dashboard" \
  -H "Authorization: Bearer {valid_superadmin_token}"

# Response
HTTP/1.1 500 Internal Server Error
{
  "error": "An error occurred while retrieving dashboard data."
}
```

## Performance Characteristics

### Response Time

**Target:** < 500ms under normal load

**Optimization Strategies:**
- Parallel repository calls using Task.WhenAll
- Efficient LINQ operations with single-pass aggregations
- Early result limiting (Take 4) before mapping
- No N+1 query issues (all data retrieved in 3 parallel calls)

### Data Volume

**Typical Response Size:** 2-5 KB

**Maximum Response Size:** ~10 KB (with full data)

### Caching

**Current Implementation:** No caching (real-time data)

**Future Enhancement:** 30-second cache with invalidation on data changes

## Implementation Details

### Architecture

The SuperAdmin Dashboard API follows Clean Architecture principles with CQRS pattern:

1. **API Layer:** SuperAdminController exposes the endpoint
2. **Application Layer:** GetSuperAdminDashboardQueryHandler orchestrates data retrieval
3. **Infrastructure Layer:** Existing repositories (ICompanyRepository, IBranchRepository, IUserRepository)
4. **Database Layer:** Oracle database with existing stored procedures

### Data Sources

The dashboard aggregates data from three existing repositories:

| Repository | Stored Procedure | Data Retrieved |
|------------|------------------|----------------|
| ICompanyRepository | SP_SYS_COMPANY_SELECT_ALL | All companies |
| IBranchRepository | SP_SYS_BRANCH_SELECT_ALL | All branches |
| IUserRepository | SP_SYS_USERS_SELECT_ALL | All users |

### Parallel Execution

Repository calls are executed in parallel using `Task.WhenAll` to minimize total execution time:

```
Sequential: 150ms + 100ms + 50ms = 300ms
Parallel: max(150ms, 100ms, 50ms) = 150ms
Savings: 50% reduction
```

## Security Considerations

### Authentication

- JWT tokens must be valid and not expired
- Token signature is verified using the configured secret key
- Token issuer and audience are validated

### Authorization

- Only users with SuperAdmin role can access the endpoint
- Role claim is validated from the JWT token
- Non-SuperAdmin users receive 403 Forbidden

### Data Protection

- Error messages do not expose internal system details
- Database schema information is never included in responses
- Stack traces are logged server-side but not returned to clients
- Sensitive data is sanitized before logging

### Rate Limiting

**Recommendation:** Implement rate limiting to prevent abuse

**Suggested Limits:**
- 60 requests per minute per user
- 1000 requests per hour per user

## Testing

### Manual Testing with cURL

```bash
# 1. Obtain SuperAdmin JWT token (from login endpoint)
TOKEN="eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."

# 2. Request dashboard data
curl -X GET "https://api.example.com/api/superadmin/dashboard" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -w "\nResponse Time: %{time_total}s\n"

# 3. Verify response time is under 500ms
# 4. Verify response structure matches schema
# 5. Verify data accuracy against database
```

### Testing Checklist

- [ ] Dashboard loads successfully for SuperAdmin user
- [ ] Dashboard returns 403 for non-SuperAdmin user
- [ ] Dashboard returns 401 for unauthenticated request
- [ ] Metrics display correct counts
- [ ] Recent companies show top 4 by creation date
- [ ] Recent branches show top 4 by update date
- [ ] Branch count per company is accurate
- [ ] Activity type (New/Update) is correct
- [ ] Alerts appear when inactive companies exist
- [ ] Response time is under 500ms
- [ ] JSON uses camelCase naming
- [ ] Error messages don't expose sensitive data

## Integration Examples

### JavaScript/TypeScript (Fetch API)

```typescript
async function getSuperAdminDashboard(token: string) {
  try {
    const response = await fetch('https://api.example.com/api/superadmin/dashboard', {
      method: 'GET',
      headers: {
        'Authorization': `Bearer ${token}`,
        'Content-Type': 'application/json'
      }
    });

    if (!response.ok) {
      if (response.status === 401) {
        throw new Error('Unauthorized - Please login again');
      }
      if (response.status === 403) {
        throw new Error('Access denied - SuperAdmin role required');
      }
      throw new Error('Failed to load dashboard data');
    }

    const data = await response.json();
    return data;
  } catch (error) {
    console.error('Dashboard API error:', error);
    throw error;
  }
}

// Usage
const dashboard = await getSuperAdminDashboard(jwtToken);
console.log('Total Companies:', dashboard.stats.totalCompanies);
console.log('Recent Companies:', dashboard.recentCompanies);
console.log('Alerts:', dashboard.alerts);
```

### C# (HttpClient)

```csharp
public async Task<SuperAdminDashboardDto> GetSuperAdminDashboardAsync(string token)
{
    using var client = new HttpClient();
    client.DefaultRequestHeaders.Authorization = 
        new AuthenticationHeaderValue("Bearer", token);

    var response = await client.GetAsync(
        "https://api.example.com/api/superadmin/dashboard");

    if (response.StatusCode == HttpStatusCode.Unauthorized)
    {
        throw new UnauthorizedAccessException("Invalid or expired token");
    }

    if (response.StatusCode == HttpStatusCode.Forbidden)
    {
        throw new UnauthorizedAccessException("SuperAdmin role required");
    }

    response.EnsureSuccessStatusCode();

    var json = await response.Content.ReadAsStringAsync();
    var dashboard = JsonSerializer.Deserialize<SuperAdminDashboardDto>(json, 
        new JsonSerializerOptions 
        { 
            PropertyNameCaseInsensitive = true 
        });

    return dashboard;
}
```

### Python (Requests)

```python
import requests

def get_superadmin_dashboard(token):
    url = "https://api.example.com/api/superadmin/dashboard"
    headers = {
        "Authorization": f"Bearer {token}",
        "Content-Type": "application/json"
    }
    
    response = requests.get(url, headers=headers)
    
    if response.status_code == 401:
        raise Exception("Unauthorized - Please login again")
    
    if response.status_code == 403:
        raise Exception("Access denied - SuperAdmin role required")
    
    response.raise_for_status()
    
    return response.json()

# Usage
dashboard = get_superadmin_dashboard(jwt_token)
print(f"Total Companies: {dashboard['stats']['totalCompanies']}")
print(f"Alerts: {dashboard['alerts']}")
```

## Troubleshooting

### Issue: 401 Unauthorized

**Possible Causes:**
- JWT token is missing from Authorization header
- JWT token is expired
- JWT token signature is invalid
- JWT token issuer/audience mismatch

**Solutions:**
1. Verify Authorization header is present and formatted correctly
2. Check token expiration time
3. Obtain a new token from the login endpoint
4. Verify token configuration matches server settings

### Issue: 403 Forbidden

**Possible Causes:**
- User does not have SuperAdmin role
- Role claim is missing from JWT token
- User account is inactive

**Solutions:**
1. Verify user has SuperAdmin role in the database
2. Check JWT token contains correct role claim
3. Verify user account is active
4. Contact system administrator to grant SuperAdmin access

### Issue: Slow Response Time (> 500ms)

**Possible Causes:**
- Large data volume in database
- Database performance issues
- Network latency
- Server resource constraints

**Solutions:**
1. Check database query performance
2. Verify database indexes are present
3. Monitor server CPU and memory usage
4. Consider implementing caching
5. Review database connection pool settings

### Issue: Empty or Incorrect Data

**Possible Causes:**
- Database contains no data
- Repository methods returning null
- Data mapping errors
- Database connection issues

**Solutions:**
1. Verify database contains test data
2. Check repository implementations
3. Review application logs for errors
4. Test database stored procedures directly
5. Verify entity-to-DTO mapping logic

## Related Documentation

- [Permissions System](./PERMISSIONS_SYSTEM.md)
- [Permissions API Guide](./PERMISSIONS_API_GUIDE.md)
- [Refresh Token API](./REFRESH_TOKEN_API.md)
- [Force Logout Feature](./FORCE_LOGOUT_FEATURE.md)

## Changelog

### Version 1.0.0 (Initial Release)

**Features:**
- Single-request dashboard endpoint
- System metrics aggregation
- Recent companies tracking (top 4)
- Recent branch activities (top 4)
- System alerts generation
- SuperAdmin role authorization
- Sub-500ms response time

**Future Enhancements:**
- Pending requests feature
- Real-time updates via SignalR
- Dashboard data caching
- Additional alert types
- Performance metrics tracking
- User activity monitoring

## Support

For technical support or questions about the SuperAdmin Dashboard API:

1. Review this documentation thoroughly
2. Check application logs for detailed error information
3. Verify JWT token and SuperAdmin role configuration
4. Test with the provided cURL examples
5. Contact the development team with specific error details

## Requirements Traceability

This API documentation satisfies the following requirements:

- **Requirement 1.1:** Dashboard data retrieval in single request
- **Requirement 2.1:** JWT authentication required
- **Requirement 2.2:** SuperAdmin role authorization
- **Requirement 2.3:** 401 response for unauthenticated requests
- **Requirement 2.4:** 403 response for non-SuperAdmin users
- **Requirement 12.1-12.6:** Response format and JSON structure

For complete requirements, see: `.kiro/specs/superadmin-dashboard/requirements.md`
