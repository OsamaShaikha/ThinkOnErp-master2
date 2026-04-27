# SYS_SAVED_SEARCH Table - Advanced Search Feature

## Overview

**SYS_SAVED_SEARCH** is a powerful feature that allows users to save complex search queries and reuse them later. Think of it like "bookmarks" for searches in your ERP system.

## Table Structure

```sql
CREATE TABLE SYS_SAVED_SEARCH (
    ROW_ID              NUMBER(19) PRIMARY KEY,
    USER_ID             NUMBER(19) NOT NULL,           -- Owner of the search
    SEARCH_NAME         NVARCHAR2(100) NOT NULL,       -- User-friendly name
    SEARCH_DESCRIPTION  NVARCHAR2(500),                -- Optional description
    SEARCH_CRITERIA     NCLOB NOT NULL,                -- JSON search parameters
    IS_PUBLIC           CHAR(1) DEFAULT 'N' NOT NULL,  -- Share with others?
    IS_DEFAULT          CHAR(1) DEFAULT 'N' NOT NULL,  -- Default search for user?
    USAGE_COUNT         NUMBER(10) DEFAULT 0 NOT NULL, -- How many times used
    LAST_USED_DATE      DATE,                          -- When last executed
    IS_ACTIVE           CHAR(1) DEFAULT 'Y' NOT NULL,  -- Active/inactive
    CREATION_USER       NVARCHAR2(100) NOT NULL,
    CREATION_DATE       DATE DEFAULT SYSDATE NOT NULL,
    UPDATE_USER         NVARCHAR2(100),
    UPDATE_DATE         DATE
);
```

---

## Purpose & Benefits

### 🎯 **Main Purpose**
Save complex search filters so users don't have to recreate them every time.

### ✅ **Key Benefits**

1. **Time Saving** - No need to rebuild complex searches
2. **Consistency** - Same search criteria every time
3. **Sharing** - Share useful searches with team members
4. **Analytics** - Track which searches are most popular
5. **User Experience** - Quick access to frequently used filters

---

## Real-World Use Cases

### 1. **Ticket Management**
```json
{
  "searchName": "My High Priority Open Tickets",
  "searchCriteria": {
    "assigneeId": 123,
    "status": ["Open", "InProgress"],
    "priority": ["High", "Critical"],
    "createdAfter": "2024-01-01"
  }
}
```

### 2. **Financial Reports**
```json
{
  "searchName": "Q1 2024 Invoices Over $10K",
  "searchCriteria": {
    "documentType": "Invoice",
    "amount": { "min": 10000 },
    "dateRange": {
      "start": "2024-01-01",
      "end": "2024-03-31"
    },
    "status": "Paid"
  }
}
```

### 3. **Customer Analysis**
```json
{
  "searchName": "VIP Customers - Last 6 Months",
  "searchCriteria": {
    "customerType": "VIP",
    "lastOrderDate": { "after": "2023-10-01" },
    "totalSpent": { "min": 50000 },
    "region": ["North", "Central"]
  }
}
```

### 4. **Inventory Management**
```json
{
  "searchName": "Low Stock Critical Items",
  "searchCriteria": {
    "category": "Critical",
    "stockLevel": { "below": "minimumStock" },
    "supplier": { "exclude": ["Supplier-X"] },
    "location": ["Warehouse-A", "Warehouse-B"]
  }
}
```

---

## How It Works

### 1. **Creating a Saved Search**

**User Action:**
1. User performs a complex search (multiple filters, date ranges, etc.)
2. User clicks "Save Search" button
3. System prompts for name and description
4. Search criteria saved as JSON

**API Call:**
```http
POST /api/saved-searches
{
  "searchName": "My Custom Search",
  "searchDescription": "All high priority tickets assigned to me",
  "searchCriteria": {
    "assigneeId": 123,
    "priority": ["High", "Critical"],
    "status": ["Open", "InProgress"]
  },
  "isPublic": false
}
```

### 2. **Using a Saved Search**

**User Action:**
1. User goes to search page
2. Selects saved search from dropdown
3. System automatically applies all filters
4. Results displayed instantly

**API Call:**
```http
GET /api/saved-searches/5/execute
```

### 3. **Sharing Searches**

**Public Searches:**
- `IS_PUBLIC = 'Y'` - Visible to all users
- `IS_PUBLIC = 'N'` - Only visible to creator

**Use Case:** Admin creates "Overdue Invoices" search and makes it public so all accounting staff can use it.

---

## Key Features

### 🔍 **Search Criteria Storage**
- **Format:** JSON stored in `SEARCH_CRITERIA` (NCLOB)
- **Flexibility:** Can store any search parameters
- **Complex Queries:** Supports nested conditions, date ranges, multiple values

### 📊 **Usage Analytics**
- **USAGE_COUNT:** Tracks how often search is used
- **LAST_USED_DATE:** When it was last executed
- **Purpose:** Identify popular searches, clean up unused ones

### 👥 **Sharing & Collaboration**
- **IS_PUBLIC:** Share searches with team
- **USER_ID:** Track ownership
- **Purpose:** Promote best practices, standardize searches

### ⭐ **Default Searches**
- **IS_DEFAULT:** User's preferred search for a screen
- **Auto-load:** Automatically applied when user visits page
- **Purpose:** Personalized experience

---

## Example Scenarios

### Scenario 1: Accounting Department
**Problem:** Accountant spends 5 minutes every morning setting up the same complex invoice search

**Solution:**
1. Create saved search: "Today's Pending Invoices"
2. Set as default search
3. Every morning, search loads automatically
4. **Time Saved:** 5 minutes daily = 20+ hours yearly

### Scenario 2: Support Team
**Problem:** Support manager needs to check "Critical tickets assigned to junior staff"

**Solution:**
1. Create saved search with complex criteria
2. Make it public for all support managers
3. Share search name with team
4. **Benefit:** Consistent monitoring across team

### Scenario 3: Sales Analysis
**Problem:** Sales director needs weekly report of "High-value deals in final stage"

**Solution:**
1. Create saved search with specific criteria
2. Execute search weekly
3. System tracks usage for reporting
4. **Benefit:** Consistent, repeatable analysis

---

## JSON Search Criteria Examples

### Simple Filter
```json
{
  "status": "Open",
  "priority": "High",
  "assigneeId": 123
}
```

### Complex Filter with Ranges
```json
{
  "status": ["Open", "InProgress", "PendingCustomer"],
  "priority": ["High", "Critical"],
  "createdDate": {
    "start": "2024-01-01",
    "end": "2024-12-31"
  },
  "amount": {
    "min": 1000,
    "max": 50000
  },
  "tags": {
    "include": ["urgent", "vip"],
    "exclude": ["test"]
  }
}
```

### Advanced Filter with Logic
```json
{
  "logic": "AND",
  "conditions": [
    {
      "field": "status",
      "operator": "in",
      "value": ["Open", "InProgress"]
    },
    {
      "logic": "OR",
      "conditions": [
        {
          "field": "priority",
          "operator": "equals",
          "value": "Critical"
        },
        {
          "field": "amount",
          "operator": "greaterThan",
          "value": 10000
        }
      ]
    }
  ]
}
```

---

## API Endpoints

### Get User's Saved Searches
```http
GET /api/saved-searches
Response: List of user's saved searches + public searches
```

### Get Public Searches
```http
GET /api/saved-searches/public
Response: List of public searches available to all users
```

### Create Saved Search
```http
POST /api/saved-searches
Body: { searchName, searchDescription, searchCriteria, isPublic }
```

### Execute Saved Search
```http
GET /api/saved-searches/{id}/execute
Response: Search results based on saved criteria
```

### Update Saved Search
```http
PUT /api/saved-searches/{id}
Body: Updated search details
```

### Delete Saved Search
```http
DELETE /api/saved-searches/{id}
```

### Set as Default
```http
POST /api/saved-searches/{id}/set-default
```

---

## Database Relationships

```sql
-- Foreign Keys
SYS_SAVED_SEARCH.USER_ID → SYS_USERS.ROW_ID

-- Indexes for Performance
IDX_SAVED_SEARCH_USER     -- (USER_ID, IS_ACTIVE)
IDX_SAVED_SEARCH_PUBLIC   -- (IS_PUBLIC, IS_ACTIVE)
IDX_SAVED_SEARCH_NAME     -- (SEARCH_NAME)
```

---

## User Interface Examples

### 1. **Search Page with Saved Searches**
```
┌─────────────────────────────────────────┐
│ 🔍 Search Tickets                      │
├─────────────────────────────────────────┤
│ Saved Searches: [My High Priority ▼]   │
│                                         │
│ ┌─ My Searches ─────────────────────┐   │
│ │ ⭐ My High Priority Open Tickets  │   │
│ │    My Overdue Tasks               │   │
│ │    Weekly Status Report           │   │
│ ├─ Public Searches ────────────────┤   │
│ │    Critical System Issues         │   │
│ │    Monthly Financial Review       │   │
│ └───────────────────────────────────┘   │
│                                         │
│ Status: [Open ▼] Priority: [High ▼]    │
│ Assignee: [Me ▼] Date: [Last 30 days]  │
│                                         │
│ [Search] [Save Search] [Clear]          │
└─────────────────────────────────────────┘
```

### 2. **Save Search Dialog**
```
┌─────────────────────────────────────────┐
│ 💾 Save Current Search                  │
├─────────────────────────────────────────┤
│ Name: [My High Priority Open Tickets]   │
│                                         │
│ Description:                            │
│ [All high priority tickets assigned to] │
│ [me that are still open or in progress] │
│                                         │
│ ☐ Make this search public               │
│ ☐ Set as my default search              │
│                                         │
│ [Cancel] [Save Search]                  │
└─────────────────────────────────────────┘
```

---

## Benefits for Different User Types

### 👤 **End Users**
- ✅ Save time on repetitive searches
- ✅ Consistent results every time
- ✅ Access to team's best practices
- ✅ Personalized default searches

### 👥 **Team Leaders**
- ✅ Share standardized searches with team
- ✅ Ensure consistent reporting
- ✅ Monitor team's search patterns
- ✅ Create templates for common tasks

### 📊 **Administrators**
- ✅ Analyze system usage patterns
- ✅ Identify popular search criteria
- ✅ Optimize system performance
- ✅ Clean up unused searches

### 🏢 **Organization**
- ✅ Knowledge sharing across teams
- ✅ Standardized business processes
- ✅ Improved user productivity
- ✅ Better data insights

---

## Summary

**SYS_SAVED_SEARCH** is like having **smart bookmarks for your business data**. It allows users to:

🔖 **Save** complex search criteria
🚀 **Reuse** them instantly
👥 **Share** with team members
📊 **Track** usage analytics
⭐ **Set** personal defaults
🎯 **Standardize** business processes

It's a productivity feature that transforms how users interact with data in your ERP system!