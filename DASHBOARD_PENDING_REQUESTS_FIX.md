# Dashboard Pending Requests Bug Fix

## Summary
Fixed the bug in the SuperAdmin dashboard where pending requests were not displaying the correct ticket data. The dashboard now shows pending tickets with company name, request type, priority, date, and status information as shown in the design mockup.

## Changes Made

### 1. Updated `PendingRequestDto.cs`
**File**: `src/ThinkOnErp.Application/DTOs/SuperAdmin/PendingRequestDto.cs`

**Before**: Empty placeholder DTO
```csharp
public class PendingRequestDto
{
   
}
```

**After**: Complete DTO with all required fields
```csharp
public class PendingRequestDto
{
    public long TicketId { get; set; }
    public string CompanyNameAr { get; set; } = string.Empty;
    public string CompanyNameEn { get; set; } = string.Empty;
    public string RequestTypeAr { get; set; } = string.Empty;
    public string RequestTypeEn { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string PriorityCode { get; set; } = string.Empty;
    public DateTime RequestDate { get; set; }
    public string BranchNameAr { get; set; } = string.Empty;
    public string BranchNameEn { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}
```

### 2. Updated `DashboardStatsDto.cs`
**File**: `src/ThinkOnErp.Application/DTOs/SuperAdmin/DashboardStatsDto.cs`

**Added**: `PendingRequests` property to track the count of pending requests
```csharp
/// <summary>
/// Number of pending requests requiring action
/// </summary>
public int PendingRequests { get; set; }
```

### 3. Updated `GetSuperAdminDashboardQueryHandler.cs`
**File**: `src/ThinkOnErp.Application/Features/SuperAdmins/Queries/GetSuperAdminDashboard/GetSuperAdminDashboardQueryHandler.cs`

**Changes**:

#### a. Modified ticket retrieval to use pagination
```csharp
// Get pending tickets (unresolved) with pagination
var ticketTask = _ticketRepository.GetAllAsync(
    page: 1,
    pageSize: 100, // Get enough tickets to filter
    sortBy: "CreationDate",
    sortDirection: "DESC");
```

#### b. Updated ticket result handling
```csharp
var ticketResult = ticketTask.Result;
var tickets = ticketResult.Tickets ?? new List<Domain.Entities.SysRequestTicket>();
```

#### c. Added pending tickets count to stats
```csharp
var pendingTicketsCount = tickets.Count(t => t.IsActive && !t.IsResolved);

var stats = new DashboardStatsDto
{
    // ... other properties
    PendingRequests = pendingTicketsCount
};
```

#### d. Updated pending requests query to filter and map correctly
```csharp
var recentTickets = tickets
    .Where(t => t.IsActive && !t.IsResolved) // Only active and unresolved tickets
    .OrderByDescending(t => t.CreationDate)
    .Take(8) // Get top 8 pending requests
    .Select(t => new PendingRequestDto
    {
        TicketId = t.RowId,
        CompanyNameAr = t.Company?.RowDesc ?? "",
        CompanyNameEn = t.Company?.RowDescE ?? "",
        RequestTypeAr = t.TitleAr,
        RequestTypeEn = t.TitleEn,
        Description = t.Description,
        Priority = t.TicketPriority?.PriorityNameAr ?? "متوسط",
        PriorityCode = GetPriorityCode(t.TicketPriority?.PriorityNameEn ?? "Medium"),
        RequestDate = t.CreationDate ?? DateTime.MinValue,
        BranchNameAr = t.Branch?.RowDesc ?? "",
        BranchNameEn = t.Branch?.RowDescE ?? "",
        Status = t.TicketStatus?.StatusNameAr ?? "قيد الانتظار"
    })
    .ToList();
```

#### e. Added priority code mapping helper method
```csharp
/// <summary>
/// Maps priority name to priority code for UI styling
/// </summary>
private static string GetPriorityCode(string priorityName)
{
    return priorityName?.ToLower() switch
    {
        "high" or "urgent" or "critical" => "high",
        "low" => "low",
        _ => "medium"
    };
}
```

#### f. Updated system alerts to include pending requests
```csharp
if (pendingTicketsCount > 0)
{
    alerts.Add($"{pendingTicketsCount} pending requests require Super Admin action");
}
```

## Key Features

### 1. **Proper Filtering**
- Only shows **active** tickets (`IsActive = true`)
- Only shows **unresolved** tickets (`!IsResolved`)
- Orders by creation date (newest first)
- Limits to top 8 pending requests

### 2. **Complete Data Mapping**
- **Company Name**: Both Arabic and English names from navigation property
- **Request Type**: Ticket title in both languages
- **Description**: Full ticket description
- **Priority**: Priority name with code for UI styling (high/medium/low)
- **Date**: Ticket creation date
- **Branch**: Branch name in both languages
- **Status**: Current ticket status

### 3. **Priority Code Mapping**
Maps priority names to UI-friendly codes:
- `"high"`, `"urgent"`, `"critical"` → `"high"` (red badge)
- `"low"` → `"low"` (green badge)
- Everything else → `"medium"` (yellow/orange badge)

### 4. **Dashboard Stats**
Added `PendingRequests` count to the dashboard statistics card showing the total number of pending tickets requiring action.

### 5. **System Alerts**
Added alert message when there are pending requests: "{count} pending requests require Super Admin action"

## Expected UI Display

Based on the mockup, the "Pending Requests Requiring Action" section will now display:

```
┌─────────────────────────────────────────────────────────────┐
│ Pending Requests Requiring Action              8 Pending    │
├─────────────────────────────────────────────────────────────┤
│ Nordic Innovations AB                           [High]      │
│ New Branch Request: Oslo office setup          [Review]    │
│ Feb 7, 2026                                                 │
├─────────────────────────────────────────────────────────────┤
│ Global Tech Solutions                           [Medium]    │
│ Branch Modification: Update Chicago branch...  [Review]    │
│ Feb 6, 2026                                                 │
├─────────────────────────────────────────────────────────────┤
│ Pacific Trading Ltd                             [Low]       │
│ Support Request: Fiscal year configuration...  [Review]    │
│ Feb 5, 2026                                                 │
└─────────────────────────────────────────────────────────────┘
```

## Testing

### Build Status
✅ **Application project builds successfully** with no errors related to the pending requests changes.

### Data Flow
1. Query handler fetches tickets from repository with pagination
2. Filters for active and unresolved tickets only
3. Maps ticket data to PendingRequestDto with all required fields
4. Returns top 8 pending requests ordered by creation date
5. Includes pending count in dashboard stats
6. Adds alert message for pending requests

## API Response Structure

The API will now return:

```json
{
  "stats": {
    "totalCompanies": 48,
    "activeCompanies": 42,
    "inactiveCompanies": 6,
    "totalBranches": 156,
    "activeBranches": 142,
    "totalSystemAdmins": 5,
    "pendingRequests": 8
  },
  "pendingRequests": [
    {
      "ticketId": 12345,
      "companyNameAr": "نورديك إنوفيشنز",
      "companyNameEn": "Nordic Innovations AB",
      "requestTypeAr": "طلب فرع جديد: إعداد مكتب أوسلو",
      "requestTypeEn": "New Branch Request: Oslo office setup",
      "description": "Request to setup new branch office in Oslo...",
      "priority": "عالي",
      "priorityCode": "high",
      "requestDate": "2026-02-07T10:30:00Z",
      "branchNameAr": "الفرع الرئيسي",
      "branchNameEn": "Main Branch",
      "status": "قيد الانتظار"
    }
    // ... more pending requests
  ],
  "alerts": [
    "6 companies are currently inactive",
    "8 pending requests require Super Admin action"
  ]
}
```

## Notes

- The fix uses the correct property names from `SysTicketPriority` (`PriorityNameAr`, `PriorityNameEn`) and `SysTicketStatus` (`StatusNameAr`, `StatusNameEn`)
- Navigation properties (`Company`, `Branch`, `TicketPriority`, `TicketStatus`) are loaded by the repository
- The query is optimized to fetch only necessary data with pagination
- Default values are provided for missing data to prevent null reference exceptions
- The implementation supports both Arabic and English languages

## Related Files

- `src/ThinkOnErp.Application/DTOs/SuperAdmin/PendingRequestDto.cs`
- `src/ThinkOnErp.Application/DTOs/SuperAdmin/DashboardStatsDto.cs`
- `src/ThinkOnErp.Application/Features/SuperAdmins/Queries/GetSuperAdminDashboard/GetSuperAdminDashboardQueryHandler.cs`
- `src/ThinkOnErp.Domain/Entities/SysRequestTicket.cs`
- `src/ThinkOnErp.Domain/Entities/SysTicketPriority.cs`
- `src/ThinkOnErp.Domain/Entities/SysTicketStatus.cs`
