# SYS_TICKET_CONFIG - Ticket Configuration System

## Overview

**SYS_TICKET_CONFIG** is a flexible configuration table that stores system-wide settings for the ticket management system. It allows administrators to customize ticket behavior without changing code.

## Purpose

Instead of hardcoding values in the application, this table provides a **database-driven configuration system** where settings can be changed dynamically through the admin interface.

## Table Structure

```sql
CREATE TABLE SYS_TICKET_CONFIG (
    ROW_ID          NUMBER(19) PRIMARY KEY,
    CONFIG_KEY      NVARCHAR2(100) NOT NULL UNIQUE,  -- Unique setting identifier
    CONFIG_VALUE    NCLOB NOT NULL,                  -- Setting value (can be JSON)
    CONFIG_TYPE     NVARCHAR2(50) NOT NULL,          -- Category of setting
    DESCRIPTION_AR  NVARCHAR2(500),                  -- Arabic description
    DESCRIPTION_EN  NVARCHAR2(500),                  -- English description
    IS_ACTIVE       CHAR(1) DEFAULT 'Y',
    CREATION_USER   NVARCHAR2(100),
    CREATION_DATE   DATE DEFAULT SYSDATE,
    UPDATE_USER     NVARCHAR2(100),
    UPDATE_DATE     DATE
);
```

## Configuration Types

The system supports 5 configuration categories:

### 1. **SLA** (Service Level Agreement)
Controls response and resolution time targets

### 2. **FileAttachment**
Controls file upload restrictions

### 3. **Notification**
Controls notification behavior and templates

### 4. **Workflow**
Controls ticket status transitions and automation

### 5. **General**
Miscellaneous settings

---

## Default Configuration Values

### 📊 SLA Settings

| Config Key | Value | Description |
|------------|-------|-------------|
| `SLA.Priority.Low.Hours` | 72 | Target resolution time for Low priority tickets (3 days) |
| `SLA.Priority.Medium.Hours` | 24 | Target resolution time for Medium priority tickets (1 day) |
| `SLA.Priority.High.Hours` | 8 | Target resolution time for High priority tickets (8 hours) |
| `SLA.Priority.Critical.Hours` | 2 | Target resolution time for Critical priority tickets (2 hours) |
| `SLA.Escalation.Threshold.Percentage` | 80 | When to escalate (80% of target time) |

**Example:**
- Critical ticket created at 10:00 AM
- Target resolution: 2 hours (12:00 PM)
- Escalation at: 80% of 2 hours = 1.6 hours (11:36 AM)

---

### 📎 File Attachment Settings

| Config Key | Value | Description |
|------------|-------|-------------|
| `FileAttachment.MaxSizeBytes` | 10485760 | Maximum file size (10 MB) |
| `FileAttachment.MaxCount` | 5 | Maximum attachments per ticket |
| `FileAttachment.AllowedTypes` | .pdf,.doc,.docx,.xls,.xlsx,.jpg,.jpeg,.png,.txt | Allowed file extensions |

**Example:**
- User tries to upload 15 MB file → Rejected (exceeds max size)
- User tries to upload .exe file → Rejected (not in allowed types)
- User tries to upload 6th file → Rejected (exceeds max count)

---

### 🔔 Notification Settings

| Config Key | Value | Description |
|------------|-------|-------------|
| `Notification.Enabled` | true | Enable/disable all notifications |
| `Notification.Template.TicketCreated` | "New ticket #{TicketId} has been created: {Title}" | Template for new ticket notification |
| `Notification.Template.TicketAssigned` | "Ticket #{TicketId} has been assigned to you: {Title}" | Template for assignment notification |
| `Notification.Template.TicketStatusChanged` | "Ticket #{TicketId} status changed to {Status}: {Title}" | Template for status change notification |
| `Notification.Template.CommentAdded` | "New comment added to ticket #{TicketId}: {Title}" | Template for new comment notification |

**Example:**
When ticket #123 is created with title "Login Issue":
```
Notification: "New ticket #123 has been created: Login Issue"
```

---

### 🔄 Workflow Settings

| Config Key | Value | Description |
|------------|-------|-------------|
| `Workflow.AllowedStatusTransitions` | JSON object | Defines which status changes are allowed |
| `Workflow.AutoCloseResolvedAfterDays` | 7 | Auto-close resolved tickets after 7 days |

**Allowed Status Transitions (JSON):**
```json
{
  "Open": ["InProgress", "Cancelled"],
  "InProgress": ["PendingCustomer", "Resolved", "Cancelled"],
  "PendingCustomer": ["InProgress", "Resolved", "Cancelled"],
  "Resolved": ["Closed"],
  "Closed": [],
  "Cancelled": []
}
```

**Example:**
- Ticket in "Open" status can only move to "InProgress" or "Cancelled"
- Ticket in "Closed" status cannot change to any other status (final state)
- Ticket in "Resolved" status automatically closes after 7 days

---

## How It Works in the Application

### 1. **Reading Configuration**

The application reads configuration values at runtime:

```csharp
// C# Example
var maxFileSize = await configService.GetConfigValueAsync("FileAttachment.MaxSizeBytes");
var slaHours = await configService.GetConfigValueAsync("SLA.Priority.High.Hours");
```

### 2. **Caching**

Configuration values are cached in memory for performance:
- Loaded once at application startup
- Cached for 1 hour (configurable)
- Automatically refreshed when changed

### 3. **Validation**

Before saving a ticket or attachment, the system validates against configuration:

```csharp
// Validate file size
if (fileSize > maxFileSize) {
    throw new ValidationException("File size exceeds maximum allowed");
}

// Validate file type
if (!allowedTypes.Contains(fileExtension)) {
    throw new ValidationException("File type not allowed");
}
```

### 4. **SLA Calculation**

When a ticket is created, the system calculates expected resolution date:

```csharp
var priorityHours = await configService.GetConfigValueAsync($"SLA.Priority.{priority}.Hours");
var expectedResolutionDate = DateTime.Now.AddHours(priorityHours);
```

---

## Benefits

### ✅ **Flexibility**
- Change settings without redeploying code
- Different settings per environment (dev/staging/prod)

### ✅ **Centralized Management**
- All settings in one place
- Easy to audit and track changes

### ✅ **Multilingual Support**
- Descriptions in Arabic and English
- Easy to understand for all users

### ✅ **Type Safety**
- CONFIG_TYPE categorizes settings
- Easy to find related settings

### ✅ **Extensibility**
- Add new settings without schema changes
- JSON values support complex configurations

---

## Admin Interface Usage

Administrators can manage these settings through the Configuration API:

### Get All Configurations
```http
GET /api/configuration
```

### Get Configuration by Key
```http
GET /api/configuration/SLA.Priority.High.Hours
```

### Update Configuration
```http
PUT /api/configuration/SLA.Priority.High.Hours
{
  "configValue": "6",
  "updateUser": "admin"
}
```

### Get Configurations by Type
```http
GET /api/configuration/type/SLA
```

---

## Real-World Examples

### Example 1: Changing SLA for High Priority
**Scenario:** Company wants to respond to high priority tickets faster

**Before:**
```
SLA.Priority.High.Hours = 8
```

**After:**
```
SLA.Priority.High.Hours = 4
```

**Result:** All new high priority tickets will have 4-hour target instead of 8 hours

---

### Example 2: Allowing More File Types
**Scenario:** Users need to upload .zip files

**Before:**
```
FileAttachment.AllowedTypes = .pdf,.doc,.docx,.xls,.xlsx,.jpg,.jpeg,.png,.txt
```

**After:**
```
FileAttachment.AllowedTypes = .pdf,.doc,.docx,.xls,.xlsx,.jpg,.jpeg,.png,.txt,.zip
```

**Result:** Users can now upload .zip files

---

### Example 3: Customizing Notification Template
**Scenario:** Company wants more detailed notifications

**Before:**
```
Notification.Template.TicketCreated = "New ticket #{TicketId} has been created: {Title}"
```

**After:**
```
Notification.Template.TicketCreated = "🎫 New ticket #{TicketId} created by {RequesterName} - Priority: {Priority} - {Title}"
```

**Result:** Notifications include more context

---

## Database Procedures

The system includes stored procedures for managing configuration:

1. **SP_SYS_TICKET_CONFIG_SELECT_ALL** - Get all configurations
2. **SP_SYS_TICKET_CONFIG_SELECT_BY_ID** - Get by ID
3. **SP_SYS_TICKET_CONFIG_SELECT_BY_KEY** - Get by key (most common)
4. **SP_SYS_TICKET_CONFIG_SELECT_BY_TYPE** - Get by type (SLA, FileAttachment, etc.)
5. **SP_SYS_TICKET_CONFIG_INSERT** - Create new configuration
6. **SP_SYS_TICKET_CONFIG_UPDATE** - Update existing configuration
7. **SP_SYS_TICKET_CONFIG_DELETE** - Soft delete configuration

---

## Best Practices

### ✅ DO:
- Use descriptive CONFIG_KEY names with dot notation (e.g., `SLA.Priority.High.Hours`)
- Provide both Arabic and English descriptions
- Use appropriate CONFIG_TYPE for categorization
- Store complex settings as JSON in CONFIG_VALUE
- Cache configuration values in the application
- Validate configuration values before saving

### ❌ DON'T:
- Hardcode configuration values in the application
- Use spaces in CONFIG_KEY names
- Store sensitive data (passwords, API keys) in this table
- Change configuration frequently (causes cache invalidation)
- Delete default configurations (mark as inactive instead)

---

## Summary

**SYS_TICKET_CONFIG** is a powerful, flexible configuration system that allows:
- ⚙️ Dynamic system behavior without code changes
- 📊 SLA management and escalation rules
- 📎 File attachment restrictions
- 🔔 Notification templates and settings
- 🔄 Workflow rules and automation
- 🌐 Multilingual support
- 🔒 Centralized, auditable configuration management

It's a key component of the ticket system that makes it adaptable to different business requirements!
