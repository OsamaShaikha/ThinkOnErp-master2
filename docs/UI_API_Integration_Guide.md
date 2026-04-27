# ThinkOnErp UI-to-API Integration Guide

This document is the comprehensive reference guide for Frontend and UI developers. It outlines the system's screen architecture and maps every user interface to its corresponding REST API endpoints, detailing exactly what operations the frontend should perform to hydrate screens and submit data.

---

## 1. Authentication & Security Context
All application endpoints (except login and token refresh) require a valid JWT Bearer token in the `Authorization: Bearer <token>` header. 

### Screen: Login & Session Management
- **`POST /api/auth/login`** 
  - **Action**: Authenticates standard users.
  - **UI Usage**: Submit username/password, store the returned JWT and Refresh Token in memory or secure storage.
- **`POST /api/auth/super-admin/login`**
  - **Action**: Special authentication for super administrators.
- **`POST /api/auth/refresh-token`**
  - **Action**: Issues a new JWT without requiring re-login.
  - **UI Usage**: Call silently when the current JWT is near expiration.
- **`POST /api/auth/logout`**
  - **Action**: Invalidates current tokens securely.

---

## 2. Core Administration Screens
These screens are used by System Administrators to configure access control and organization boundaries.

### 2.1. Users Management Screen
- **`GET /api/users`** - Fetch all system users (table view).
- **`GET /api/users/{id}`** - Fetch user details (edit form).
- **`POST /api/users`** - Create a new user.
- **`PUT /api/users/{id}`** - Update user details.
- **`DELETE /api/users/{id}`** - Soft-delete user (archive).
- **`PUT /api/users/{id}/change-password`** - User self-service password update.
- **`POST /api/users/{id}/force-logout`** - Admin action to kill active user sessions.
- **`POST /api/users/{id}/reset-password`** - Admin action to generate a temporary password for a user.

### 2.2. Roles & Screen Permissions Setup
- **`GET /api/roles`** - List all roles.
- **`POST /api/roles`** - Create a new role.
- **`PUT /api/roles/{id}`** - Update role name/description.
- **`DELETE /api/roles/{id}`** - Delete role.
- **`GET /api/permissions/systems`** - Fetch all top-level ERP Systems (Accounting, HR, etc.).
- **`GET /api/permissions/systems/{systemId}/screens`** - Fetch screens available under a specific system.
- **`GET /api/permissions/roles/{roleId}/permissions`** - Get current (View/Insert/Update/Delete) capabilities for a role.
- **`PUT /api/permissions/roles/{roleId}/permissions`** - Save modified V/I/U/D permissions for a role.
- **`PUT /api/permissions/users/{userId}/permissions`** - Set user-specific permission overrides (bypassing role defaults).
- **`POST /api/permissions/check`** - **CRITICAL UI ROUTE**: Used dynamically by the frontend router or component wrapper to verify if the current user has permission (`V`, `I`, `U`, or `D`) for a specific screen code before rendering it.

### 2.3. Companies & Branches Configuration
- **`GET /api/company`** - List all parent companies.
- **`POST /api/company`** - Create company (UI must send company logo as a **Base64 encoded string**).
- **`PUT /api/company/{id}`** - Update company details.
- **`GET /api/branch`** - List all branches.
- **`GET /api/branch/company/{companyId}`** - List branches belonging to a specific company (cascading dropdowns).
- **`PUT /api/permissions/companies/{companyId}/systems/{systemId}`** - Toggle a company's subscription to a specific ERP System module.

---

## 3. Helpdesk & Ticketing Screens
This module allows users to report issues and support staff to manage resolutions.

### 3.1. Tickets Dashboard
- **`GET /api/tickets`** - Fetch paginated list of tickets (supports filtering by status, priority, and date).
- **`GET /api/tickets/{id}`** - Fetch full ticket details for the ticket view page.
- **`POST /api/tickets`** - Submit a new support ticket.
- **`PUT /api/tickets/{id}`** - Edit ticket properties.
- **`DELETE /api/tickets/{id}`** - Archive/Delete ticket.

### 3.2. Ticket Interactions & Workflow
- **`PUT /api/tickets/{id}/assign`** - Assign a ticket to a staff member.
- **`PUT /api/tickets/{id}/status`** - Advance ticket workflow status.
- **`POST /api/tickets/{id}/comments`** - Add a discussion comment to the ticket timeline.
- **`POST /api/tickets/{id}/attachments`** - Upload a file (Base64) to the ticket.
- **`GET /api/tickets/{id}/attachments/{attachmentId}`** - Download attachment file.

### 3.3. Support Configuration Screens
- **`GET /api/configuration/all`** - Fetch all global ticket settings.
- **`GET /api/configuration/sla-settings`** - Fetch SLA compliance targets.
- **`PUT /api/configuration/sla-settings`** - Update SLA thresholds and hours.
- **`GET /api/configuration/file-attachments`** - Fetch max file size and allowed extensions for the UI file upload component.

---

## 4. Traceability & System Monitoring Dashboards
High-level dashboard screens for auditors and administrators to monitor system health and security.

### 4.1. Audit Trail Explorer
- **`GET /api/audit-logs`** - Search and filter through historical entity changes (Before/After JSON comparisons).
- **`GET /api/audit-trail`** - View chronologically structured system events.

### 4.2. Security & Compliance 
- **`GET /api/alerts`** - Fetch active system security alerts (e.g., unauthorized access attempts).
- **`GET /api/compliance/report`** - Generate compliance status reports.

### 4.3. Infrastructure Monitoring
- **`GET /api/monitoring/metrics`** - Data feed for UI Charts displaying CPU, Memory, and Request volume.
- **`GET /api/monitoring/active-sessions`** - Data feed for displaying currently connected users.

---

## 5. Core ERP System Modules (Screen Mappings)

The UI is divided into 5 distinct Business Systems. Depending on the Company Subscription (`SYS_COMPANY_SYSTEM`), these modules will be visible or hidden in the navigation sidebar.

### System 1: Accounting (`/accounting/*`)
*Requires Accounting System Subscription*
* **Chart of Accounts** - `route: /accounting/chart-of-accounts`
* **Journal Entries** - `route: /accounting/journal-entries`
* **Invoices** - `route: /accounting/invoices`
* **Payments** - `route: /accounting/payments`
* **Financial Reports** - `route: /accounting/reports`

### System 2: Inventory (`/inventory/*`)
*Requires Inventory System Subscription*
* **Products** - `route: /inventory/products`
* **Warehouses** - `route: /inventory/warehouses`
* **Stock Movements** - `route: /inventory/stock-movements`
* **Purchase Orders** - `route: /inventory/purchase-orders`
* **Stock Reports** - `route: /inventory/reports`

### System 3: HR (`/hr/*`)
*Requires HR System Subscription*
* **Employees** - `route: /hr/employees`
* **Payroll** - `route: /hr/payroll`
* **Leave Requests** - `route: /hr/leave-requests`
* **Attendance** - `route: /hr/attendance`
* **HR Reports** - `route: /hr/reports`

### System 4: CRM (`/crm/*`)
*Requires CRM System Subscription*
* **Customers** - `route: /crm/customers`
* **Leads** - `route: /crm/leads`
* **Opportunities** - `route: /crm/opportunities`
* **Sales Reports** - `route: /crm/reports`

### System 5: POS (`/pos/*`)
*Requires POS System Subscription*
* **Point of Sale Terminal** - `route: /pos/sale`
* **Daily Sales** - `route: /pos/daily-sales`
* **Cash Drawer Management** - `route: /pos/cash-drawer`
* **POS Reports** - `route: /pos/reports`

> [!IMPORTANT]
> **UI Authorization Implementation Note:** 
> Before rendering any route in Sections 5.1 through 5.5, the frontend router MUST evaluate the user's permissions via `GET /api/permissions/users/{userId}/effective` or dynamically verify actions using `POST /api/permissions/check`. Buttons related to Data Mutation (Save, Delete, New) must be conditionally hidden if the `I`, `U`, or `D` flags are missing for that user on that screen.
