# ThinkOnERP - UI/API Comprehensive Guide
## Part 2: Super Admin Dashboard & Management

---

## Table of Contents
1. [Super Admin Dashboard](#super-admin-dashboard)
2. [Super Admin Management](#super-admin-management)
3. [System Overview](#system-overview)

---

## Super Admin Dashboard

### Screen Name: Super Admin Dashboard
**Route**: `/superadmin/dashboard`  
**Access**: SuperAdmin only  
**Authorization**: AdminOnly policy

### Purpose
Provides system-wide overview including metrics, alerts, recent activity, and system health monitoring.

### API Integration
**Endpoint**: `GET /api/superadmins/dashboard`

**Headers**:
```
Authorization: Bearer {jwt_token}
```

**Success Response** (200 OK):
```json
{
  "success": true,
  "message": "SuperAdmin dashboard data retrieved successfully",
  "data": {
    "systemMetrics": {
      "totalCompanies": 25,
      "activeCompanies": 23,
      "totalBranches": 150,
      "totalUsers": 1250,
      "activeUsers": 1100
    },
    "recentCompanies": [
      {
        "companyId": 123,
        "nameAr": "شركة الاختبار",
        "nameEn": "Test Company",
        "companyCode": "TC001",
        "status": "Active",
        "createdAt": "2024-01-15T10:30:00Z",
        "branchCount": 5,
        "userCount": 45
      }
    ],
    "recentBranchActivity": [
      {
        "branchId": 456,
        "branchNameAr": "الفرع الرئيسي",
        "branchNameEn": "Main Branch",
        "companyNameAr": "شركة الاختبار",
        "companyNameEn": "Test Company",
        "activityType": "UserCreated",
        "activityDate": "2024-01-20T14:25:00Z",
        "details": "New user 'john.doe' created"
      }
    ],
    "systemAlerts": [
      {
        "alertId": 789,
        "severity": "Warning",
        "category": "Performance",
        "message": "Database connection pool usage at 85%",
        "timestamp": "2024-01-20T15:00:00Z",
        "isAcknowledged": false
      }
    ],
    "performanceMetrics": {
      "avgResponseTime": 125.5,
      "requestsPerMinute": 450,
      "errorRate": 0.02,
      "cpuUsage": 45.3,
      "memoryUsage": 62.8
    }
  },
  "statusCode": 200
}
```

### UI Components

#### 1. System Metrics Cards (Top Row)
Display key metrics in card format:

**Total Companies Card**:
- Icon: Building icon
- Value: `systemMetrics.totalCompanies`
- Label: "Total Companies"
- Subtext: `systemMetrics.activeCompanies` + " Active"
- Color: Blue

**Total Branches Card**:
- Icon: Branch icon
- Value: `systemMetrics.totalBranches`
- Label: "Total Branches"
- Color: Green

**Total Users Card**:
- Icon: Users icon
- Value: `systemMetrics.totalUsers`
- Label: "Total Users"
- Subtext: `systemMetrics.activeUsers` + " Active"
- Color: Purple

**System Health Card**:
- Icon: Heart/Health icon
- Value: "Healthy" or "Warning" or "Critical"
- Based on: `performanceMetrics` and `systemAlerts`
- Color: Green/Yellow/Red

#### 2. Recent Companies Table
**Columns**:
- Company Code
- Company Name (English/Arabic toggle)
- Status (Badge: Active/Inactive)
- Branches Count
- Users Count
- Created Date
- Actions (View Details button)

**Data Source**: `recentCompanies` array

**Features**:
- Sortable columns
- Click row to view company details
- Status badge with color coding
- Date formatting (relative time)

#### 3. Recent Activity Timeline
**Display**: Vertical timeline with cards

**Each Activity Card Shows**:
- Activity icon (based on `activityType`)
- Branch name (English/Arabic)
- Company name
- Activity description
- Timestamp (relative time)

**Data Source**: `recentBranchActivity` array

**Activity Types**:
- `UserCreated`: User icon, blue
- `BranchCreated`: Branch icon, green
- `CompanyCreated`: Building icon, purple
- `PasswordReset`: Key icon, orange
- `UserDeactivated`: User-X icon, red

#### 4. System Alerts Panel
**Display**: Alert list with severity indicators

**Each Alert Shows**:
- Severity badge (Critical/Warning/Info)
- Category
- Message
- Timestamp
- Acknowledge button (if not acknowledged)

**Data Source**: `systemAlerts` array

**Severity Colors**:
- Critical: Red
- Warning: Yellow
- Info: Blue

**Features**:
- Filter by severity
- Filter by category
- Mark as acknowledged
- Auto-refresh every 30 seconds

#### 5. Performance Metrics Chart
**Display**: Line/Area chart showing real-time metrics

**Metrics Displayed**:
- Average Response Time (ms)
- Requests Per Minute
- Error Rate (%)
- CPU Usage (%)
- Memory Usage (%)

**Data Source**: `performanceMetrics` object

**Features**:
- Real-time updates (WebSocket or polling)
- Time range selector (Last hour/day/week)
- Metric toggle (show/hide specific metrics)

---

## Super Admin Management

### Screen Name: Super Admin List
**Route**: `/superadmin/manage`  
**Access**: SuperAdmin only

### 1. Get All Super Admins

**API Endpoint**: `GET /api/superadmins`

**Success Response** (200 OK):
```json
{
  "success": true,
  "message": "Super admins retrieved successfully",
  "data": [
    {
      "rowId": 1,
      "nameAr": "المسؤول الأعلى",
      "nameEn": "Super Administrator",
      "userName": "superadmin",
      "email": "superadmin@thinkonerp.com",
      "phone": "+966501234567",
      "isActive": true,
      "createdAt": "2024-01-01T00:00:00Z",
      "lastLoginAt": "2024-01-20T09:15:00Z"
    }
  ],
  "statusCode": 200
}
```

### UI Components

#### Super Admin Table
**Columns**:
- ID
- Name (English/Arabic toggle)
- Username
- Email
- Phone
- Status (Active/Inactive badge)
- Last Login
- Actions (Edit, Delete, Reset Password, Change Password)

**Features**:
- Search by name/username/email
- Filter by status (Active/Inactive)
- Sort by any column
- Pagination (10/25/50/100 per page)
- Bulk actions (Activate/Deactivate selected)

**Action Buttons**:
- **Edit**: Opens edit modal
- **Delete**: Soft delete with confirmation
- **Reset Password**: Generates temporary password
- **Change Password**: Opens change password modal

---

### 2. Create Super Admin

**Screen**: Create Super Admin Modal/Page  
**API Endpoint**: `POST /api/superadmins`

**Request Body**:
```json
{
  "nameAr": "أحمد محمد",
  "nameEn": "Ahmed Mohammed",
  "userName": "ahmed.mohammed",
  "password": "SecurePass@123",
  "email": "ahmed@thinkonerp.com",
  "phone": "+966501234567"
}
```

**Success Response** (201 Created):
```json
{
  "success": true,
  "message": "Super admin created successfully",
  "data": 5,
  "statusCode": 201
}
```

**Validation Errors** (400 Bad Request):
```json
{
  "success": false,
  "message": "One or more validation errors occurred",
  "errors": [
    "Username must be at least 3 characters",
    "Password must contain uppercase, lowercase, number and special character",
    "Email format is invalid"
  ],
  "statusCode": 400
}
```

#### Form Fields:

**Name (Arabic)** - Text Input
- Label: "الاسم بالعربية"
- Validation: Required, max 100 characters
- Placeholder: "أدخل الاسم بالعربية"

**Name (English)** - Text Input
- Label: "Name (English)"
- Validation: Required, max 100 characters
- Placeholder: "Enter name in English"

**Username** - Text Input
- Label: "Username"
- Validation: Required, min 3 characters, unique
- Placeholder: "Enter username"
- Help Text: "Used for login"

**Password** - Password Input
- Label: "Password"
- Validation: Required, min 8 characters, must contain uppercase, lowercase, number, special character
- Placeholder: "Enter secure password"
- Show/Hide toggle
- Password strength indicator

**Email** - Email Input
- Label: "Email"
- Validation: Required, valid email format
- Placeholder: "email@example.com"

**Phone** - Tel Input
- Label: "Phone"
- Validation: Optional, valid phone format
- Placeholder: "+966XXXXXXXXX"

**Buttons**:
- **Create** (Primary): Submit form
- **Cancel** (Secondary): Close modal/return to list

---

### 3. Update Super Admin

**Screen**: Edit Super Admin Modal/Page  
**API Endpoint**: `PUT /api/superadmins/{id}`

**Request Body**:
```json
{
  "nameAr": "أحمد محمد المحدث",
  "nameEn": "Ahmed Mohammed Updated",
  "email": "ahmed.updated@thinkonerp.com",
  "phone": "+966509876543"
}
```

**Success Response** (200 OK):
```json
{
  "success": true,
  "message": "Super admin updated successfully",
  "data": true,
  "statusCode": 200
}
```

#### Form Fields:
Same as Create form, but:
- Username field is disabled (cannot be changed)
- Password field is not shown (use Change Password instead)
- Form is pre-populated with existing data

---

### 4. Delete Super Admin

**API Endpoint**: `DELETE /api/superadmins/{id}`

**Success Response** (200 OK):
```json
{
  "success": true,
  "message": "Super admin deleted successfully",
  "data": true,
  "statusCode": 200
}
```

#### UI Flow:
1. User clicks Delete button
2. Show confirmation dialog:
   - Title: "Delete Super Admin?"
   - Message: "Are you sure you want to delete [Name]? This action cannot be undone."
   - Buttons: "Delete" (danger), "Cancel"
3. On confirm, call API
4. On success, show success message and refresh list
5. On error, show error message

---

### 5. Change Password

**Screen**: Change Password Modal  
**API Endpoint**: `PUT /api/superadmins/{id}/change-password`

**Request Body**:
```json
{
  "currentPassword": "OldPass@123",
  "newPassword": "NewPass@123",
  "confirmPassword": "NewPass@123"
}
```

**Success Response** (200 OK):
```json
{
  "success": true,
  "message": "Password changed successfully",
  "data": true,
  "statusCode": 200
}
```

**Error Response** (400 Bad Request):
```json
{
  "success": false,
  "message": "Current password is incorrect",
  "statusCode": 400
}
```

#### Form Fields:

**Current Password** - Password Input
- Label: "Current Password"
- Validation: Required
- Placeholder: "Enter current password"

**New Password** - Password Input
- Label: "New Password"
- Validation: Required, min 8 characters, strong password
- Placeholder: "Enter new password"
- Password strength indicator

**Confirm Password** - Password Input
- Label: "Confirm New Password"
- Validation: Required, must match new password
- Placeholder: "Re-enter new password"

**Buttons**:
- **Change Password** (Primary): Submit form
- **Cancel** (Secondary): Close modal

---

### 6. Reset Password (Admin Action)

**Screen**: Confirmation Dialog  
**API Endpoint**: `POST /api/superadmins/{id}/reset-password`

**Success Response** (200 OK):
```json
{
  "success": true,
  "message": "Password reset successfully",
  "data": {
    "temporaryPassword": "TempPass@789Xyz",
    "message": "Password has been reset successfully. Please provide this temporary password to the user and ask them to change it immediately."
  },
  "statusCode": 200
}
```

#### UI Flow:
1. User clicks Reset Password button
2. Show confirmation dialog:
   - Title: "Reset Password?"
   - Message: "Generate a temporary password for [Name]?"
   - Buttons: "Reset" (warning), "Cancel"
3. On confirm, call API
4. On success, show temporary password in a modal:
   - Display temporary password (with copy button)
   - Warning message about security
   - "Close" button
5. On error, show error message

---

## View Super Admin Details

**Screen**: Super Admin Details Page  
**Route**: `/superadmin/manage/{id}`  
**API Endpoint**: `GET /api/superadmins/{id}`

**Success Response** (200 OK):
```json
{
  "success": true,
  "message": "Super admin retrieved successfully",
  "data": {
    "rowId": 1,
    "nameAr": "المسؤول الأعلى",
    "nameEn": "Super Administrator",
    "userName": "superadmin",
    "email": "superadmin@thinkonerp.com",
    "phone": "+966501234567",
    "isActive": true,
    "createdAt": "2024-01-01T00:00:00Z",
    "createdBy": "system",
    "updatedAt": "2024-01-15T10:30:00Z",
    "updatedBy": "admin",
    "lastLoginAt": "2024-01-20T09:15:00Z"
  },
  "statusCode": 200
}
```

### UI Layout:

#### Header Section:
- Name (large, prominent)
- Status badge (Active/Inactive)
- Action buttons: Edit, Delete, Reset Password, Change Password

#### Details Section (Card Layout):

**Personal Information Card**:
- Name (Arabic): [value]
- Name (English): [value]
- Username: [value]
- Email: [value] (with mailto link)
- Phone: [value] (with tel link)

**System Information Card**:
- Status: [Active/Inactive badge]
- Created At: [formatted date]
- Created By: [username]
- Updated At: [formatted date]
- Updated By: [username]
- Last Login: [formatted date with relative time]

**Activity Log Card** (Optional):
- Recent actions performed by this super admin
- Login history
- Changes made

---

## Next Steps

Continue to:
- [Part 3: Company Management](./UI_API_GUIDE_PART3_COMPANY.md)
- [Part 4: Branch Management](./UI_API_GUIDE_PART4_BRANCH.md)
- [Part 5: User Management](./UI_API_GUIDE_PART5_USERS.md)
