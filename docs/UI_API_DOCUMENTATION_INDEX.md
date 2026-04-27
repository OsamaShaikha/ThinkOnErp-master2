# ThinkOnERP - Complete UI/API Documentation

## 📚 Documentation Overview

This comprehensive guide provides detailed information for UI developers building frontend applications for the ThinkOnERP system. Each section includes screen layouts, API endpoints, request/response formats, validation rules, and UI behavior specifications.

---

## 🗂️ Documentation Structure

### ✅ Part 1: Authentication & Authorization
**File**: [UI_API_GUIDE_PART1_AUTHENTICATION.md](./UI_API_GUIDE_PART1_AUTHENTICATION.md)

**Topics Covered**:
- Authentication system overview
- User login screen
- Super admin login screen
- Token refresh mechanism
- Authorization policies
- Security best practices
- Error handling
- Test credentials

**Key APIs**:
- `POST /api/auth/login` - User authentication
- `POST /api/auth/superadmin/login` - Super admin authentication
- `POST /api/auth/refresh` - Token refresh
- `POST /api/auth/superadmin/refresh` - Super admin token refresh

---

### ✅ Part 2: Super Admin Dashboard & Management
**File**: [UI_API_GUIDE_PART2_SUPERADMIN_DASHBOARD.md](./UI_API_GUIDE_PART2_SUPERADMIN_DASHBOARD.md)

**Topics Covered**:
- Super admin dashboard with system metrics
- System alerts and monitoring
- Performance metrics visualization
- Super admin CRUD operations
- Password management (change/reset)
- Activity logging

**Key APIs**:
- `GET /api/superadmins/dashboard` - Dashboard data
- `GET /api/superadmins` - List all super admins
- `GET /api/superadmins/{id}` - Get super admin details
- `POST /api/superadmins` - Create super admin
- `PUT /api/superadmins/{id}` - Update super admin
- `DELETE /api/superadmins/{id}` - Delete super admin
- `PUT /api/superadmins/{id}/change-password` - Change password
- `POST /api/superadmins/{id}/reset-password` - Reset password

---

### ✅ Part 3: Company Management
**File**: [UI_API_GUIDE_PART3_COMPANY_MANAGEMENT.md](./UI_API_GUIDE_PART3_COMPANY_MANAGEMENT.md)

**Topics Covered**:
- Company list with search and filters
- Create company with default branch
- Edit company information
- Company details view
- Logo management (upload/display/update/remove)
- Base64 image handling
- Company deletion with cascading effects

**Key APIs**:
- `GET /api/companies` - List all companies
- `GET /api/companies/{id}` - Get company details
- `POST /api/companies` - Create company with branch
- `PUT /api/companies/{id}` - Update company
- `DELETE /api/companies/{id}` - Delete company

---

### 📝 Part 4: Branch Management
**Status**: To be created

**Topics to Cover**:
- Branch list and filtering
- Create branch
- Edit branch
- Branch details
- Branch logo management
- Branch configuration (language, currency, rounding)
- Contact information management

**Key APIs**:
- `GET /api/branches` - List all branches
- `GET /api/branches/{id}` - Get branch details
- `GET /api/branches/company/{companyId}` - Get branches by company
- `POST /api/branches` - Create branch
- `PUT /api/branches/{id}` - Update branch
- `DELETE /api/branches/{id}` - Delete branch

---

### 📝 Part 5: User Management
**Status**: To be created

**Topics to Cover**:
- User list with advanced filtering
- Create user
- Edit user
- User details
- Password management
- Force logout functionality
- User activation/deactivation
- Role assignment

**Key APIs**:
- `GET /api/users` - List all users
- `GET /api/users/{id}` - Get user details
- `GET /api/users/branch/{branchId}` - Get users by branch
- `GET /api/users/company/{companyId}` - Get users by company
- `POST /api/users` - Create user
- `PUT /api/users/{id}` - Update user
- `DELETE /api/users/{id}` - Delete user
- `PUT /api/users/{id}/change-password` - Change password
- `POST /api/users/{id}/reset-password` - Reset password
- `POST /api/users/{id}/force-logout` - Force logout

---

### 📝 Part 6: Roles & Permissions
**Status**: To be created

**Topics to Cover**:
- Role list
- Create/edit roles
- Permission assignment
- Role-based access control
- Permission matrix

**Key APIs**:
- `GET /api/roles` - List all roles
- `GET /api/roles/{id}` - Get role details
- `POST /api/roles` - Create role
- `PUT /api/roles/{id}` - Update role
- `DELETE /api/roles/{id}` - Delete role

---

### 📝 Part 7: Currency Management
**Status**: To be created

**Topics to Cover**:
- Currency list
- Create/edit currencies
- Currency codes and symbols
- Exchange rates (if applicable)

**Key APIs**:
- `GET /api/currencies` - List all currencies
- `GET /api/currencies/{id}` - Get currency details
- `POST /api/currencies` - Create currency
- `PUT /api/currencies/{id}` - Update currency
- `DELETE /api/currencies/{id}` - Delete currency

---

### 📝 Part 8: Ticket Management System
**Status**: To be created

**Topics to Cover**:
- Ticket list with advanced filtering
- Create ticket
- Update ticket
- Ticket assignment
- Status workflow
- Comments system
- Attachments (upload/download)
- Ticket reports (volume, SLA compliance, workload)

**Key APIs**:
- `GET /api/tickets` - List tickets with filters
- `GET /api/tickets/{id}` - Get ticket details
- `POST /api/tickets` - Create ticket
- `PUT /api/tickets/{id}` - Update ticket
- `DELETE /api/tickets/{id}` - Delete ticket
- `PUT /api/tickets/{id}/assign` - Assign ticket
- `PUT /api/tickets/{id}/status` - Update status
- `POST /api/tickets/{id}/comments` - Add comment
- `GET /api/tickets/{id}/comments` - Get comments
- `POST /api/tickets/{id}/attachments` - Upload attachment
- `GET /api/tickets/{id}/attachments` - Get attachments

---

### 📝 Part 9: Audit & Compliance
**Status**: To be created

**Topics to Cover**:
- Audit log viewer
- Advanced search and filtering
- Compliance reports (GDPR, SOX, HIPAA)
- Data export
- Audit trail visualization

**Key APIs**:
- `GET /api/auditlogs` - List audit logs
- `GET /api/auditlogs/{id}` - Get audit log details
- `GET /api/auditlogs/search` - Advanced search
- `GET /api/compliance/gdpr-access` - GDPR access report
- `GET /api/compliance/gdpr-export` - GDPR data export
- `GET /api/compliance/sox-financial-access` - SOX financial access
- `GET /api/compliance/sox-segregation` - SOX segregation of duties

---

### 📝 Part 10: Monitoring & Alerts
**Status**: To be created

**Topics to Cover**:
- System monitoring dashboard
- Performance metrics
- Alert management
- Alert acknowledgment
- Health checks

**Key APIs**:
- `GET /api/monitoring/dashboard` - Monitoring dashboard
- `GET /api/monitoring/metrics` - Performance metrics
- `GET /api/alerts` - List alerts
- `PUT /api/alerts/{id}/acknowledge` - Acknowledge alert
- `GET /api/health` - Health check

---

## 🎨 UI Design Guidelines

### General Principles

1. **Responsive Design**:
   - Mobile-first approach
   - Breakpoints: 320px, 768px, 1024px, 1440px
   - Fluid layouts with flexible grids

2. **Accessibility**:
   - WCAG 2.1 Level AA compliance
   - Keyboard navigation support
   - Screen reader compatibility
   - Sufficient color contrast (4.5:1 minimum)

3. **Internationalization**:
   - Support for Arabic (RTL) and English (LTR)
   - Language toggle in header
   - Date/time formatting based on locale
   - Number formatting based on locale

4. **Consistent Components**:
   - Use design system components
   - Consistent spacing (8px grid system)
   - Consistent typography
   - Consistent color palette

### Color Palette

**Primary Colors**:
- Primary: #1976D2 (Blue)
- Secondary: #424242 (Dark Gray)
- Success: #4CAF50 (Green)
- Warning: #FF9800 (Orange)
- Error: #F44336 (Red)
- Info: #2196F3 (Light Blue)

**Neutral Colors**:
- Background: #FAFAFA
- Surface: #FFFFFF
- Text Primary: #212121
- Text Secondary: #757575
- Divider: #BDBDBD

### Typography

**Font Family**: 
- English: Roboto, sans-serif
- Arabic: Cairo, sans-serif

**Font Sizes**:
- H1: 32px / 2rem
- H2: 24px / 1.5rem
- H3: 20px / 1.25rem
- H4: 18px / 1.125rem
- Body: 16px / 1rem
- Small: 14px / 0.875rem
- Caption: 12px / 0.75rem

### Spacing

**Base Unit**: 8px

**Common Spacings**:
- xs: 4px (0.5 units)
- sm: 8px (1 unit)
- md: 16px (2 units)
- lg: 24px (3 units)
- xl: 32px (4 units)
- 2xl: 48px (6 units)

---

## 🔧 API Standards

### Base URL
```
Development: http://localhost:5000
Production: https://api.thinkonerp.com
```

### Request Headers
```
Authorization: Bearer {jwt_token}
Content-Type: application/json
Accept: application/json
Accept-Language: en-US,ar-SA
```

### Response Format

**Success Response**:
```json
{
  "success": true,
  "message": "Operation completed successfully",
  "data": { /* response data */ },
  "statusCode": 200
}
```

**Error Response**:
```json
{
  "success": false,
  "message": "Error message",
  "data": null,
  "errors": ["Detailed error 1", "Detailed error 2"],
  "statusCode": 400
}
```

### HTTP Status Codes

| Code | Meaning | Usage |
|------|---------|-------|
| 200 | OK | Successful GET, PUT, DELETE |
| 201 | Created | Successful POST |
| 400 | Bad Request | Validation errors |
| 401 | Unauthorized | Authentication required |
| 403 | Forbidden | Insufficient permissions |
| 404 | Not Found | Resource not found |
| 500 | Internal Server Error | Server error |

### Pagination

**Query Parameters**:
```
?page=1&pageSize=25&sortBy=createdAt&sortOrder=desc
```

**Response Format**:
```json
{
  "success": true,
  "data": {
    "items": [ /* array of items */ ],
    "totalCount": 100,
    "page": 1,
    "pageSize": 25,
    "totalPages": 4,
    "hasNextPage": true,
    "hasPreviousPage": false
  }
}
```

### Filtering

**Query Parameters**:
```
?status=active&companyId=1&search=test&dateFrom=2024-01-01&dateTo=2024-12-31
```

### Sorting

**Query Parameters**:
```
?sortBy=createdAt&sortOrder=desc
```

**Sort Orders**:
- `asc`: Ascending
- `desc`: Descending

---

## 🧪 Testing

### Test Data

Refer to `Database/Scripts/06_Insert_Test_Data.sql` for test data including:
- Super admin accounts
- Test companies
- Test branches
- Test users
- Test roles
- Test currencies

### API Testing Tools

**Recommended Tools**:
- Postman (collection available)
- Swagger UI (available at `/swagger`)
- curl commands

### Swagger Documentation

Access interactive API documentation at:
```
http://localhost:5000/swagger
```

Features:
- Try out API endpoints
- View request/response schemas
- Authentication support
- Example requests

---

## 📞 Support & Resources

### Documentation
- API Reference: `/swagger`
- Database Schema: `Database/Scripts/tables.sql`
- Test Data: `Database/TEST_DATA_README.md`

### Key Files
- Authentication Guide: `SUPERADMIN_COMPLETE_IMPLEMENTATION_SUMMARY.md`
- Company Creation: `CREATE_COMPANY_WITH_BRANCH_GUIDE.md`
- Force Logout: `docs/FORCE_LOGOUT_FEATURE.md`
- Deployment: `DEPLOYMENT.md`

### Contact
For questions or issues, contact the development team.

---

## 🔄 Document Updates

**Last Updated**: January 2024  
**Version**: 1.0  
**Status**: In Progress

**Completed Sections**:
- ✅ Part 1: Authentication & Authorization
- ✅ Part 2: Super Admin Dashboard & Management
- ✅ Part 3: Company Management

**Pending Sections**:
- 📝 Part 4: Branch Management
- 📝 Part 5: User Management
- 📝 Part 6: Roles & Permissions
- 📝 Part 7: Currency Management
- 📝 Part 8: Ticket Management System
- 📝 Part 9: Audit & Compliance
- 📝 Part 10: Monitoring & Alerts

---

## 📝 Contributing

To contribute to this documentation:
1. Follow the established format and structure
2. Include comprehensive API examples
3. Provide UI mockups or descriptions
4. Test all API endpoints before documenting
5. Update the index when adding new sections

---

**End of Index**
