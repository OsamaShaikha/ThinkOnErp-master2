# Kiro Prompt: Comprehensive Multi-Tenant Permissions System

## Project Overview

Build a **comprehensive, multi-tenant permissions and access control system** for a SaaS platform that supports a Super Admin dashboard managing multiple companies. Each company has its own admin, users, systems, and granular screen-level permissions.

---

## Roles Hierarchy

```
Super Admin  (full platform control + full permission control over ALL entities)
    │
    ├── Manages Systems & Screens (global)
    ├── Manages Companies (create / suspend / delete)
    ├── Manages Company Admins (create / edit / assign permissions)
    ├── Assigns System-level access per Company
    ├── Assigns Role-level permissions per Company
    └── Assigns User-level permissions per Company
            │
            └── Company (Tenant)
                    └── Company Admin  (manages users & roles within own company only)
                            └── Company Users

---

## Actors & Responsibilities

### 1. Super Admin
The Super Admin is the highest authority in the platform with **unrestricted, full-spectrum permission control** across every layer of the system:

#### Company & System Management
- Can **create, edit, suspend, and delete companies** (tenants).
- Assigns which **systems** each company is **allowed** or **blocked** from (e.g., allow Accounting, block HR).
- Can revoke or restore system access for any company at any time.

#### Full Permission Management (all layers)
The Super Admin can manage permissions at **every level**, for **any company**, without restriction:

**System-level (per company):**
- Allow or block entire systems per company.

**Role-level (per company):**
- Create, edit, and delete roles within any company.
- Assign **View / Insert / Update / Delete** permissions per screen per role — for any company.

**User-level (per company):**
- Create, edit, deactivate any user in any company.
- Assign or remove roles for any user in any company.
- Set **direct user-level screen permission overrides** (View / Insert / Update / Delete) for any user in any company.
- These overrides take priority over role-based permissions.

#### Oversight
- Can view **audit logs across all companies**.
- Can impersonate Company Admins for support/debugging (logged action).
- Cannot be created or modified by any other role — Super Admin accounts are managed only via secure backend provisioning.

### 2. Company Admin
- Created and managed by the Super Admin.
- Has full control **within their own company only** — they cannot access or modify other companies.
- Can **create, edit, deactivate users** within their company.
- Can create and manage **roles** within their company.
- Can assign **View / Insert / Update / Delete** permissions per screen per role — but **only for systems and screens the Super Admin has granted to their company**.
- Can assign roles to users and set user-level permission overrides.
- Cannot grant access to systems that the Super Admin has blocked for their company.
- Can view audit logs scoped to their own company only.

> **Note:** The Super Admin has all the same capabilities as a Company Admin, but across ALL companies simultaneously.

### 3. Company User
- Created by the Company Admin.
- Has access **only** to the systems and screens explicitly permitted to them.
- Access is controlled at the **screen level** with four action-level permissions: **View, Insert, Update, Delete**.

---

## Core Entities & Data Model

Design and implement the following entities:

### `SuperAdmin`
- id, name, email, password_hash, created_at

### `Company` (Tenant)
- id, name, slug, logo, status (active/suspended/deleted), created_at, created_by (super_admin_id)

### `System`
- id, name, code (e.g., `accounting`, `inventory`, `hr`), description, icon, is_active

### `CompanySystem` (Company ↔ System Assignment)
- id, company_id, system_id, is_allowed (boolean), granted_by (super_admin_id), granted_at, revoked_at, notes

### `Screen`
- id, system_id, name, code (e.g., `invoices_list`, `purchase_orders`), route, description, parent_screen_id (nullable, for nested screens)

### `CompanyUser`
- id, company_id, name, email, password_hash, status (active/inactive/suspended), created_by (company_admin_id), created_at

### `Role`
- id, company_id, name (e.g., "Accountant", "Warehouse Staff"), description, is_default, created_at

### `RoleScreenPermission`
- id, role_id, screen_id, can_view (bool), can_insert (bool), can_update (bool), can_delete (bool)

### `UserRole`
- id, user_id, role_id, assigned_at, assigned_by

### `UserScreenPermission` (Optional Override)
- id, user_id, screen_id, can_view, can_insert, can_update, can_delete
- Purpose: allow per-user overrides on top of their role permissions.

### `AuditLog`
- id, actor_type (super_admin/company_admin/user), actor_id, company_id, action, entity_type, entity_id, old_value (JSON), new_value (JSON), ip_address, user_agent, created_at

---

## Permission Resolution Logic

Implement a **layered permission resolution** in this order:

```
0. Is the actor a Super Admin?
   → YES → Grant access immediately. Skip all checks below.

1. Is the system allowed for this company by Super Admin?
   → NO → Deny all access regardless of anything else.

2. Does the user belong to this company?
   → NO → Deny.

3. Is the user active (and company active)?
   → NO → Deny.

4. Check UserScreenPermission (direct override set by Super Admin OR Company Admin for this user):
   → If exists → use it (highest priority for non-super-admin actors).

5. Check RoleScreenPermission (merge permissions across ALL roles assigned to user):
   → Use OR logic: if ANY role grants can_view=true → user can view.

6. Default → Deny.
```

Expose a single function/endpoint:
```
checkPermission(actorId, actorType, screenCode, action) → boolean
```

Super Admin calls always return `true` without touching the permission tables.

---

## Features to Implement

### Super Admin Dashboard

- [ ] Login with 2FA support
- [ ] List all companies with status, system count, user count

**Company Management:**
- [ ] Create Company form:
  - Company name, slug, logo upload
  - **System Assignment panel**: list all systems with toggle (Allow / Block)
  - Notes per system assignment
- [ ] Edit Company: modify system access, suspend/restore company
- [ ] View per-company: admins, users, active systems

**Full Permission Management (per any company):**
- [ ] Enter any company's permission workspace (scoped view for that company)
- [ ] **Role Management** (within any company):
  - Create / edit / delete roles
  - Permission matrix table: rows = screens (grouped by system), columns = View / Insert / Update / Delete
  - Only show screens from systems the company is allowed to access
- [ ] **User Management** (within any company):
  - Create / edit / deactivate users
  - Assign / remove roles from users
  - Set **user-level screen permission overrides** (View / Insert / Update / Delete per screen)
  - View effective resolved permissions per user (merged from roles + overrides)
- [ ] **Company Admin Management**:
  - Create / edit / deactivate company admin accounts
  - Optionally restrict what company admins themselves can manage

**Global Tools:**
- [ ] Global audit log viewer with filters (company, actor, date range, action type)
- [ ] Manage Systems: CRUD for systems and their screens (global, applies to all companies)

### Company Admin Panel

- [ ] Login with company-scoped session
- [ ] Dashboard: user count, role count, accessible systems
- [ ] User Management:
  - Create / Edit / Deactivate users
  - Assign one or more roles to a user
  - Optional: override specific screen permissions per user
- [ ] Role Management:
  - Create roles (e.g., "Accountant", "Warehouse Staff")
  - For each role, show only the screens of systems **allowed for this company**
  - Toggle View / Insert / Update / Delete per screen
  - Visual permission matrix table (rows = screens, columns = actions)
- [ ] Company audit log viewer (scoped to own company)

### User Interface (End User)

- [ ] Login
- [ ] Dynamic sidebar/navigation: only show systems and screens the user has `can_view = true`
- [ ] Action buttons (Add, Edit, Delete) rendered conditionally based on permissions
- [ ] If user navigates directly to a blocked URL → show 403 Forbidden page

---

## API Design

### Authentication
```
POST /auth/super-admin/login
POST /auth/company/login
POST /auth/logout
POST /auth/refresh-token
```

### Super Admin APIs
```
-- Company Management --
GET    /super-admin/companies
POST   /super-admin/companies
GET    /super-admin/companies/:id
PUT    /super-admin/companies/:id
DELETE /super-admin/companies/:id
PATCH  /super-admin/companies/:id/status           → suspend / restore

-- System & Screen Management (global) --
GET    /super-admin/systems
POST   /super-admin/systems
PUT    /super-admin/systems/:id
DELETE /super-admin/systems/:id
GET    /super-admin/systems/:id/screens
POST   /super-admin/systems/:id/screens
PUT    /super-admin/screens/:id
DELETE /super-admin/screens/:id

-- Company System Assignment --
GET    /super-admin/companies/:id/systems          → get allowed/blocked systems
PUT    /super-admin/companies/:id/systems          → bulk update system assignment

-- Company Admin Management --
GET    /super-admin/companies/:id/admins
POST   /super-admin/companies/:id/admins
PUT    /super-admin/companies/:companyId/admins/:adminId
PATCH  /super-admin/companies/:companyId/admins/:adminId/status

-- Role Management (for any company) --
GET    /super-admin/companies/:id/roles
POST   /super-admin/companies/:id/roles
GET    /super-admin/companies/:companyId/roles/:roleId
PUT    /super-admin/companies/:companyId/roles/:roleId
DELETE /super-admin/companies/:companyId/roles/:roleId
GET    /super-admin/companies/:companyId/roles/:roleId/permissions
PUT    /super-admin/companies/:companyId/roles/:roleId/permissions   → bulk update screen perms

-- User Management (for any company) --
GET    /super-admin/companies/:id/users
POST   /super-admin/companies/:id/users
GET    /super-admin/companies/:companyId/users/:userId
PUT    /super-admin/companies/:companyId/users/:userId
PATCH  /super-admin/companies/:companyId/users/:userId/status
POST   /super-admin/companies/:companyId/users/:userId/roles         → assign role
DELETE /super-admin/companies/:companyId/users/:userId/roles/:roleId → remove role
GET    /super-admin/companies/:companyId/users/:userId/permissions   → effective permissions
PUT    /super-admin/companies/:companyId/users/:userId/permissions/override → direct overrides

-- Audit --
GET    /super-admin/audit-logs
```

### Company Admin APIs
```
GET    /company/users
POST   /company/users
GET    /company/users/:id
PUT    /company/users/:id
PATCH  /company/users/:id/status

GET    /company/roles
POST   /company/roles
GET    /company/roles/:id
PUT    /company/roles/:id
DELETE /company/roles/:id
GET    /company/roles/:id/permissions
PUT    /company/roles/:id/permissions          → bulk update screen permissions

POST   /company/users/:id/roles                → assign roles
DELETE /company/users/:id/roles/:roleId        → remove role

GET    /company/users/:id/permissions          → effective merged permissions
PUT    /company/users/:id/permissions/override → set user-level overrides

GET    /company/audit-logs
```

### Permission Check API (used by frontend or microservices)
```
POST /permissions/check
Body: { actorId, actorType ("super_admin" | "company_admin" | "user"), screenCode, action }
Response: { allowed: true/false, reason?: string }
→ Super Admin always returns { allowed: true }

GET /permissions/user/:id/accessible-screens
Response: { screens: [{ screenCode, canView, canInsert, canUpdate, canDelete }] }

GET /permissions/user/:id/effective
Response: full merged permission map (roles + overrides resolved)
```

---

## Technical Requirements

### Backend
- **Framework**: Node.js (NestJS) or Python (FastAPI) — choose one and be consistent
- **Database**: PostgreSQL with proper indexing on all foreign keys and (user_id, screen_id) pairs
- **ORM**: Prisma (Node) or SQLAlchemy (Python)
- **Auth**: JWT access tokens (short-lived, 15m) + refresh tokens (7d), stored in HttpOnly cookies
- **Password hashing**: bcrypt with salt rounds ≥ 12
- **2FA**: TOTP (Google Authenticator compatible) for Super Admin
- **Rate limiting**: on login endpoints
- **Input validation**: strict validation on all API inputs

### Frontend
- **Framework**: React (Next.js) with TypeScript
- **State Management**: Zustand or Redux Toolkit
- **Permission Hook**:
  ```tsx
  const { can } = usePermission();
  // Usage:
  can('invoices_list', 'insert')  // → boolean
  ```
- **Route Guard**: HOC or middleware that checks screen-level permission before rendering
- **UI Library**: shadcn/ui or Ant Design

### Security Requirements
- All endpoints require authentication (JWT)
- Super Admin endpoints must validate `role = SUPER_ADMIN` in token
- Company endpoints must validate company scope (user can only access own company's data)
- Permission resolution must be **server-side** — never trust client-side permission state alone
- Log all write operations to AuditLog
- SQL injection prevention via parameterized queries (handled by ORM)
- XSS prevention via output escaping

---

## Database Indexing Requirements

```sql
-- Critical indexes for performance
CREATE INDEX idx_company_system_company ON company_system(company_id);
CREATE INDEX idx_role_screen_perm_role ON role_screen_permission(role_id);
CREATE INDEX idx_role_screen_perm_screen ON role_screen_permission(screen_id);
CREATE INDEX idx_user_role_user ON user_role(user_id);
CREATE INDEX idx_user_screen_perm_user ON user_screen_permission(user_id);
CREATE INDEX idx_screen_system ON screen(system_id);
CREATE INDEX idx_audit_log_company ON audit_log(company_id);
CREATE INDEX idx_audit_log_actor ON audit_log(actor_id, actor_type);
```

---

## Seed Data Requirements

Seed the following for development and testing:

**Systems:**
- Accounting System (`accounting`) → Screens: Chart of Accounts, Journal Entries, Invoices, Payments, Financial Reports
- Inventory System (`inventory`) → Screens: Products, Warehouses, Stock Movements, Purchase Orders, Stock Reports
- HR System (`hr`) → Screens: Employees, Payroll, Leave Requests, Attendance, HR Reports
- CRM System (`crm`) → Screens: Customers, Leads, Opportunities, Sales Reports
- POS System (`pos`) → Screens: Point of Sale, Daily Sales, Cash Drawer, POS Reports

**Demo Setup:**
- 1 Super Admin account
- 2 Companies:
  - Company A: Allowed (Accounting, Inventory, CRM), Blocked (HR, POS)
  - Company B: Allowed (Accounting, POS), Blocked (Inventory, HR, CRM)
- Each company: 1 admin, 3 users, 2 roles with varied permissions

---

## Folder Structure (Suggested)

```
/
├── backend/
│   ├── src/
│   │   ├── auth/
│   │   ├── super-admin/
│   │   │   ├── companies/
│   │   │   ├── systems/
│   │   │   ├── roles/          ← Super Admin manages roles for any company
│   │   │   ├── users/          ← Super Admin manages users for any company
│   │   │   └── audit/
│   │   ├── company-admin/
│   │   │   ├── users/
│   │   │   ├── roles/
│   │   │   └── permissions/
│   │   ├── permissions/
│   │   │   ├── permission.service.ts   ← core resolution logic (actor-aware)
│   │   │   ├── permission.guard.ts
│   │   │   └── super-admin.bypass.ts   ← short-circuit for Super Admin
│   │   └── common/
│   │       ├── audit/
│   │       └── guards/
│   ├── prisma/
│   │   ├── schema.prisma
│   │   └── seed.ts
│   └── tests/
│
├── frontend/
│   ├── app/
│   │   ├── (super-admin)/
│   │   │   ├── companies/
│   │   │   ├── companies/[id]/
│   │   │   │   ├── systems/        ← assign/block systems
│   │   │   │   ├── roles/          ← manage roles & permission matrix
│   │   │   │   └── users/          ← manage users & overrides
│   │   │   └── systems/            ← global system & screen management
│   │   ├── (company-admin)/
│   │   └── (user)/
│   ├── components/
│   │   ├── PermissionMatrix/       ← role permission editor table (shared)
│   │   ├── UserPermissionOverride/ ← per-user direct override editor
│   │   ├── SystemAssignment/       ← company system toggles
│   │   └── ProtectedRoute/
│   ├── hooks/
│   │   └── usePermission.ts
│   └── stores/
│       └── permissionStore.ts
│
└── docs/
    ├── ERD.md
    ├── API.md
    └── PERMISSIONS_FLOW.md
```

---

## Acceptance Criteria

**Super Admin:**
- [ ] Super Admin can create a company and assign/block systems.
- [ ] Super Admin can create and manage roles in **any** company with full screen-level permissions.
- [ ] Super Admin can create and manage users in **any** company, assign roles, and set direct overrides.
- [ ] Super Admin always passes all permission checks — no screen or action is ever denied.
- [ ] Super Admin impersonation of Company Admin is logged in AuditLog.

**Company Admin:**
- [ ] Company Admin can only see and configure systems allowed by Super Admin.
- [ ] Company Admin can create roles and assign View/Insert/Update/Delete per screen.
- [ ] Company Admin can assign roles to users and set user-level overrides.
- [ ] Company Admin cannot access or modify other companies' data.

**Users:**
- [ ] Users see only screens they have `can_view = true`.
- [ ] Action buttons (Insert/Update/Delete) are hidden if user lacks that permission.
- [ ] Direct URL navigation to a forbidden screen returns 403.

**System Behavior:**
- [ ] Revoking a system from a company immediately blocks all its screens for all users in that company.
- [ ] Suspending a company blocks all logins for that company (except Super Admin access).
- [ ] All write operations (by any actor) are recorded in AuditLog with before/after values and actor identity.
- [ ] Permission check API responds in < 50ms (use Redis caching with invalidation on permission change).
- [ ] All endpoints have integration tests with ≥ 80% coverage.

---

## Additional Notes for Kiro

- Prioritize the **permission resolution engine** first — it is the core of the system.
- Use **database-level constraints** (foreign keys, unique constraints) to enforce data integrity.
- The permission matrix UI for roles should be a **spreadsheet-like table**: rows are screens grouped by system, columns are (View, Insert, Update, Delete) with checkboxes.
- Consider **caching** user permissions in Redis with cache invalidation on role/permission change.
- The system must support **future extensibility**: new systems and screens can be added without code changes — purely data-driven.
- Generate full **TypeScript types** for all entities and API responses.
- Write **Swagger/OpenAPI documentation** for all endpoints.
