# Permissions System API Guide

## Overview

This guide provides detailed information on using the ThinkOnErp Permissions System API. The system provides granular screen-level access control with support for Super Admin, Company Admin, and regular users.

## Authentication

All endpoints require JWT authentication via the `Authorization` header:

```
Authorization: Bearer <your-jwt-token>
```

Admin-only endpoints additionally require the user to have admin privileges (IS_ADMIN = '1').

## Base URL

```
https://your-api-domain.com/api/permissions
```

## Endpoints

### 1. Check Permission

Check if a user has permission to perform an action on a screen.

**Endpoint:** `POST /api/permissions/check`

**Request Body:**
```json
{
  "userId": 123,
  "screenCode": "invoices",
  "action": "VIEW"
}
```

**Actions:** `VIEW`, `INSERT`, `UPDATE`, `DELETE`

**Response:**
```json
{
  "success": true,
  "statusCode": 200,
  "message": "Permission check completed",
  "data": {
    "allowed": true,
    "reason": null
  }
}
```

**Example Usage:**
```bash
curl -X POST https://api.example.com/api/permissions/check \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "userId": 123,
    "screenCode": "invoices",
    "action": "VIEW"
  }'
```

---

### 2. Get All Systems

Retrieve all active systems/modules.

**Endpoint:** `GET /api/permissions/systems`

**Response:**
```json
{
  "success": true,
  "statusCode": 200,
  "message": "Systems retrieved successfully",
  "data": [
    {
      "systemId": 1,
      "systemCode": "accounting",
      "systemNameAr": "نظام المحاسبة",
      "systemNameEn": "Accounting System",
      "descriptionAr": "إدارة الحسابات والمعاملات المالية",
      "descriptionEn": "Manage accounts and financial transactions",
      "icon": "calculator",
      "displayOrder": 1,
      "isActive": true
    }
  ]
}
```

---

### 3. Get Screens by System

Retrieve all screens for a specific system.

**Endpoint:** `GET /api/permissions/systems/{systemId}/screens`

**Response:**
```json
{
  "success": true,
  "statusCode": 200,
  "message": "Screens retrieved successfully",
  "data": [
    {
      "screenId": 1,
      "systemId": 1,
      "parentScreenId": null,
      "screenCode": "invoices",
      "screenNameAr": "الفواتير",
      "screenNameEn": "Invoices",
      "route": "/accounting/invoices",
      "descriptionAr": "إدارة الفواتير",
      "descriptionEn": "Manage invoices",
      "icon": "file-text",
      "displayOrder": 3,
      "isActive": true
    }
  ]
}
```

---

### 4. Get User Roles

Retrieve all roles assigned to a user.

**Endpoint:** `GET /api/permissions/users/{userId}/roles`

**Response:**
```json
{
  "success": true,
  "statusCode": 200,
  "message": "User roles retrieved successfully",
  "data": [
    {
      "userId": 123,
      "roleId": 7,
      "roleNameAr": "محاسب",
      "roleNameEn": "Accountant",
      "assignedBy": 1,
      "assignedDate": "2026-04-14T10:30:00Z"
    }
  ]
}
```

---

### 5. Assign Role to User

Assign a role to a user. **Requires Admin privileges.**

**Endpoint:** `POST /api/permissions/users/{userId}/roles/{roleId}`

**Response:**
```json
{
  "success": true,
  "statusCode": 200,
  "message": "Role assigned successfully",
  "data": null
}
```

**Example:**
```bash
curl -X POST https://api.example.com/api/permissions/users/123/roles/7 \
  -H "Authorization: Bearer YOUR_ADMIN_TOKEN"
```

---

### 6. Remove Role from User

Remove a role from a user. **Requires Admin privileges.**

**Endpoint:** `DELETE /api/permissions/users/{userId}/roles/{roleId}`

**Response:**
```json
{
  "success": true,
  "statusCode": 200,
  "message": "Role removed successfully",
  "data": null
}
```

---

### 7. Get Role Screen Permissions

Retrieve all screen permissions for a role.

**Endpoint:** `GET /api/permissions/roles/{roleId}/permissions`

**Response:**
```json
{
  "success": true,
  "statusCode": 200,
  "message": "Role permissions retrieved successfully",
  "data": [
    {
      "roleId": 7,
      "screenId": 1,
      "screenCode": "invoices",
      "screenNameAr": "الفواتير",
      "screenNameEn": "Invoices",
      "systemId": 1,
      "canView": true,
      "canInsert": true,
      "canUpdate": true,
      "canDelete": false
    }
  ]
}
```

---

### 8. Set Role Screen Permission

Set screen permission for a role. **Requires Admin privileges.**

**Endpoint:** `PUT /api/permissions/roles/{roleId}/permissions`

**Request Body:**
```json
{
  "screenId": 1,
  "canView": true,
  "canInsert": true,
  "canUpdate": true,
  "canDelete": false
}
```

**Response:**
```json
{
  "success": true,
  "statusCode": 200,
  "message": "Role permission set successfully",
  "data": null
}
```

---

### 9. Set User Screen Permission Override

Set screen permission override for a specific user. **Requires Admin privileges.**

**Endpoint:** `PUT /api/permissions/users/{userId}/permissions`

**Request Body:**
```json
{
  "screenId": 1,
  "canView": true,
  "canInsert": false,
  "canUpdate": false,
  "canDelete": false,
  "notes": "Temporary view-only access"
}
```

**Response:**
```json
{
  "success": true,
  "statusCode": 200,
  "message": "User permission override set successfully",
  "data": null
}
```

---

### 10. Set Company System Access

Allow or block a system for a company. **Requires Admin privileges.**

**Endpoint:** `PUT /api/permissions/companies/{companyId}/systems/{systemId}?isAllowed=true`

**Query Parameters:**
- `isAllowed` (boolean): `true` to allow, `false` to block

**Response:**
```json
{
  "success": true,
  "statusCode": 200,
  "message": "System access granted successfully",
  "data": null
}
```

**Example:**
```bash
# Allow system
curl -X PUT "https://api.example.com/api/permissions/companies/1/systems/1?isAllowed=true" \
  -H "Authorization: Bearer YOUR_ADMIN_TOKEN"

# Block system
curl -X PUT "https://api.example.com/api/permissions/companies/1/systems/1?isAllowed=false" \
  -H "Authorization: Bearer YOUR_ADMIN_TOKEN"
```

---

## Permission Resolution Logic

The system uses a layered permission resolution:

1. **Super Admin Check**: If user has `IS_SUPER_ADMIN = '1'`, grant all permissions immediately
2. **System Allowed**: Check if the system is allowed for the user's company
3. **User Active**: Check if user and company are active
4. **User Override**: Check for direct user-level permission overrides (highest priority)
5. **Role Permissions**: Merge permissions from all assigned roles using OR logic
6. **Default**: Deny if no permissions found

## Common Use Cases

### Use Case 1: Check if User Can View Invoices

```javascript
const response = await fetch('/api/permissions/check', {
  method: 'POST',
  headers: {
    'Authorization': `Bearer ${token}`,
    'Content-Type': 'application/json'
  },
  body: JSON.stringify({
    userId: currentUser.id,
    screenCode: 'invoices',
    action: 'VIEW'
  })
});

const result = await response.json();
if (result.data.allowed) {
  // Show invoices screen
} else {
  // Show access denied message
}
```

### Use Case 2: Assign Accountant Role to User

```javascript
const response = await fetch(`/api/permissions/users/${userId}/roles/${accountantRoleId}`, {
  method: 'POST',
  headers: {
    'Authorization': `Bearer ${adminToken}`
  }
});

if (response.ok) {
  console.log('Role assigned successfully');
}
```

### Use Case 3: Set Role Permissions for All Accounting Screens

```javascript
// Get all accounting screens
const systemsResponse = await fetch('/api/permissions/systems/1/screens', {
  headers: { 'Authorization': `Bearer ${token}` }
});
const screens = await systemsResponse.json();

// Set permissions for each screen
for (const screen of screens.data) {
  await fetch(`/api/permissions/roles/${roleId}/permissions`, {
    method: 'PUT',
    headers: {
      'Authorization': `Bearer ${adminToken}`,
      'Content-Type': 'application/json'
    },
    body: JSON.stringify({
      screenId: screen.screenId,
      canView: true,
      canInsert: true,
      canUpdate: true,
      canDelete: false
    })
  });
}
```

### Use Case 4: Grant Temporary View-Only Access

```javascript
// Override user permissions for specific screen
await fetch(`/api/permissions/users/${userId}/permissions`, {
  method: 'PUT',
  headers: {
    'Authorization': `Bearer ${adminToken}`,
    'Content-Type': 'application/json'
  },
  body: JSON.stringify({
    screenId: sensitiveScreenId,
    canView: true,
    canInsert: false,
    canUpdate: false,
    canDelete: false,
    notes: 'Temporary access for audit - expires 2026-05-01'
  })
});
```

## Error Responses

All endpoints return standardized error responses:

```json
{
  "success": false,
  "statusCode": 400,
  "message": "Error description",
  "data": null,
  "errors": ["Detailed error message"],
  "timestamp": "2026-04-14T12:00:00Z",
  "traceId": "unique-trace-id"
}
```

**Common Status Codes:**
- `200` - Success
- `400` - Bad Request (validation errors)
- `401` - Unauthorized (missing or invalid token)
- `403` - Forbidden (insufficient privileges)
- `404` - Not Found
- `500` - Internal Server Error

## Best Practices

1. **Cache Permission Checks**: Cache permission check results on the client side for better performance
2. **Batch Operations**: When setting permissions for multiple screens, consider batching requests
3. **Use Screen Codes**: Always use screen codes (not IDs) for permission checks as they're more stable
4. **Audit Trail**: The system automatically logs all permission changes in `SYS_AUDIT_LOG`
5. **Super Admin Bypass**: Super admins bypass all permission checks - use this role carefully
6. **Role-Based First**: Prefer role-based permissions over user-level overrides for easier management
7. **OR Logic**: Users with multiple roles get combined permissions (any role granting access is sufficient)

## Testing

### Test Permission Check

```bash
# Test as regular user
curl -X POST http://localhost:5000/api/permissions/check \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "userId": 0,
    "screenCode": "invoices",
    "action": "VIEW"
  }'
```

### Test Get Systems

```bash
curl -X GET http://localhost:5000/api/permissions/systems \
  -H "Authorization: Bearer YOUR_TOKEN"
```

### Test Assign Role (Admin)

```bash
curl -X POST http://localhost:5000/api/permissions/users/0/roles/7 \
  -H "Authorization: Bearer YOUR_ADMIN_TOKEN"
```

## Database Setup

Before using the API, ensure you've run these database scripts in order:

1. `08_Create_Permissions_Tables.sql`
2. `09_Create_Permissions_Sequences.sql`
3. `10_Create_Permissions_Procedures.sql`
4. `11_Insert_Permissions_Seed_Data.sql`

## Support

For issues or questions:
- Check the main documentation: `docs/PERMISSIONS_SYSTEM.md`
- Review the database schema: `Database/Scripts/08_Create_Permissions_Tables.sql`
- Examine stored procedures: `Database/Scripts/10_Create_Permissions_Procedures.sql`
