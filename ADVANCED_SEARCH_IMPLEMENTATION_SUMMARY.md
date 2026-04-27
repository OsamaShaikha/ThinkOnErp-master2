# Advanced Search Functionality Implementation Summary

## Task 10.1: Create Advanced Search Functionality

**Status:** ✅ COMPLETED

**Requirements Addressed:** 8.1-8.12

## Implementation Overview

This implementation enhances the existing ticket search system with advanced capabilities including full-text search with relevance scoring, multi-criteria filtering with AND/OR logic, saved search functionality, and search result ranking.

## Components Implemented

### 1. Database Layer

#### Saved Search Tables (Script 47)
- **SYS_SAVED_SEARCH**: Stores user-defined saved searches
  - Supports private and public searches
  - Tracks usage count and last used date
  - Allows setting default search per user
  - Stores search criteria as JSON

#### Advanced Search Stored Procedure (Script 48)
- **SP_SYS_REQUEST_TICKET_ADVANCED_SEARCH**: Enhanced search with:
  - Full-text search across titles and descriptions
  - Multi-criteria filtering (status, priority, type, category, dates, SLA status)
  - AND/OR logic for combining criteria
  - Relevance scoring algorithm:
    - Exact title match: 100 points
    - Title starts with term: 80 points
    - Title contains term: 60 points
    - Description contains term: 40 points
    - Critical priority boost: +20 points
    - High priority boost: +10 points
    - Overdue tickets boost: +15 points
    - Due soon boost: +10 points
  - Support for multiple values per filter (comma-separated IDs)
  - Flexible sorting (RELEVANCE, CREATION_DATE, PRIORITY, STATUS, DUE_DATE)

#### Saved Search Procedures (Script 47)
- **SP_SYS_SAVED_SEARCH_INSERT**: Creates new saved search
- **SP_SYS_SAVED_SEARCH_UPDATE**: Updates existing saved search
- **SP_SYS_SAVED_SEARCH_SELECT_BY_USER**: Retrieves user's saved searches
- **SP_SYS_SAVED_SEARCH_SELECT_BY_ID**: Retrieves specific saved search
- **SP_SYS_SAVED_SEARCH_DELETE**: Soft deletes saved search
- **SP_SYS_SAVED_SEARCH_INCREMENT_USAGE**: Tracks search usage

### 2. Domain Layer

#### Entities
- **SysSavedSearch**: Domain entity for saved searches
  - Properties: RowId, UserId, SearchName, SearchDescription, SearchCriteria
  - Flags: IsPublic, IsDefault, IsActive
  - Tracking: UsageCount, LastUsedDate
  - Audit fields: CreationUser, CreationDate, UpdateUser, UpdateDate

#### Repository Interfaces
- **ISavedSearchRepository**: Contract for saved search operations
  - CreateAsync, UpdateAsync, GetByUserIdAsync, GetByIdAsync
  - DeleteAsync, IncrementUsageAsync

- **ITicketRepository** (Enhanced):
  - Added AdvancedSearchAsync method with comprehensive filtering

### 3. Infrastructure Layer

#### Repositories
- **SavedSearchRepository**: Implementation using Oracle stored procedures
  - Follows existing ThinkOnERP patterns
  - Uses ADO.NET with OracleDbContext
  - Proper parameter mapping and error handling

- **TicketRepository** (Enhanced):
  - Implemented AdvancedSearchAsync method
  - Calls SP_SYS_REQUEST_TICKET_ADVANCED_SEARCH
  - Maps results to SysRequestTicket entities with navigation properties
  - Handles relevance scores

#### Dependency Injection
- Registered ISavedSearchRepository → SavedSearchRepository

### 4. Application Layer

#### DTOs
- **SavedSearchDto**: Read model for saved searches
- **CreateSavedSearchDto**: Create model for saved searches
- **UpdateSavedSearchDto**: Update model for saved searches
- **TicketDto** (Enhanced): Added RelevanceScore property

#### Commands
- **CreateSavedSearchCommand**: Creates new saved search
- **CreateSavedSearchCommandHandler**: Handles saved search creation

#### Queries
- **GetSavedSearchesQuery**: Retrieves user's saved searches
- **GetSavedSearchesQueryHandler**: Handles saved search retrieval

- **GetTicketsQuery** (Enhanced): Added properties:
  - StatusIds, PriorityIds, TypeIds, CategoryIds (multi-value support)
  - FilterLogic (AND/OR)
  - UseAdvancedSearch flag

- **GetTicketsQueryHandler** (Enhanced):
  - Detects when advanced search is needed
  - Routes to AdvancedSearchAsync or GetAllAsync
  - Populates RelevanceScore in results

### 5. API Layer

#### Controllers
- **SavedSearchesController**: New controller for saved search management
  - GET /api/saved-searches: Retrieve user's saved searches
  - POST /api/saved-searches: Create new saved search
  - Requires authentication
  - Extracts user info from JWT claims

- **TicketsController** (Enhanced):
  - GET /api/tickets now supports advanced search parameters
  - Backward compatible with existing queries

## Features Delivered

### ✅ Full-Text Search with Relevance Scoring
- Searches across ticket titles (Arabic and English) and descriptions
- Intelligent relevance scoring based on match quality
- Priority and urgency boost factors
- Sortable by relevance score

### ✅ Multi-Criteria Filtering with AND/OR Logic
- Support for multiple values per filter type
- Flexible combination logic (AND/OR)
- Filters: Company, Branch, Assignee, Requester, Status, Priority, Type, Category
- Date range filters: Creation date, Due date
- SLA status filter: OnTime, AtRisk, Overdue

### ✅ Saved Search Functionality
- Users can save frequently used search criteria
- Private and public saved searches
- Default search per user
- Usage tracking for analytics
- JSON storage of search criteria for flexibility

### ✅ Search Result Ranking
- Relevance-based ranking algorithm
- Multiple sorting options
- Pagination support
- Total count for UI pagination

## Database Scripts to Execute

Execute in order:
1. `Database/Scripts/47_Create_Saved_Search_Tables.sql`
2. `Database/Scripts/48_Create_Advanced_Search_Procedure.sql`

## API Endpoints

### Saved Searches
- `GET /api/saved-searches` - Get user's saved searches
- `POST /api/saved-searches` - Create new saved search

### Enhanced Ticket Search
- `GET /api/tickets?useAdvancedSearch=true&searchTerm=...&filterLogic=OR&statusIds=1,2,3`

## Example Usage

### Advanced Search with OR Logic
```http
GET /api/tickets?useAdvancedSearch=true&searchTerm=urgent&priorityIds=3,4&statusIds=1,2&filterLogic=OR&sortBy=RELEVANCE
```

### Save a Search
```http
POST /api/saved-searches
{
  "searchName": "My High Priority Tickets",
  "searchDescription": "All high and critical priority tickets assigned to me",
  "searchCriteria": "{\"priorityIds\":\"3,4\",\"assigneeId\":123}",
  "isPublic": false,
  "isDefault": true
}
```

## Requirements Traceability

| Requirement | Implementation | Status |
|------------|----------------|--------|
| 8.1 - Full-text search | SP_SYS_REQUEST_TICKET_ADVANCED_SEARCH with LIKE queries | ✅ |
| 8.2 - Filter by status, priority, type, category | Multi-value filter parameters | ✅ |
| 8.3 - Filter by company, branch, requester, assignee | Individual filter parameters | ✅ |
| 8.4 - Date range filtering | CreatedFrom/To, DueFrom/To parameters | ✅ |
| 8.5 - SLA compliance filtering | SlaStatus parameter | ✅ |
| 8.6 - Saved search functionality | SYS_SAVED_SEARCH table + CRUD operations | ✅ |
| 8.7 - Sorting options | SortBy parameter (RELEVANCE, DATE, PRIORITY, etc.) | ✅ |
| 8.8 - Pagination | Page and PageSize parameters | ✅ |
| 8.9 - Multi-criteria with AND/OR | FilterLogic parameter | ✅ |
| 8.10 - Consistent ApiResponse format | All endpoints use ApiResponse wrapper | ✅ |
| 8.11 - Search query logging | Logger calls in handlers | ✅ |
| 8.12 - Authorization respect | User context passed to repository | ✅ |

## Testing Recommendations

1. **Unit Tests** (Optional - Task 10.4):
   - Test relevance scoring algorithm
   - Test filter logic combinations
   - Test saved search CRUD operations

2. **Integration Tests**:
   - Test advanced search with various filter combinations
   - Test AND vs OR logic
   - Test saved search persistence and retrieval

3. **Property Tests** (Optional - Task 10.3):
   - Search result consistency
   - Relevance score ordering
   - Filter combination correctness

## Performance Considerations

- Indexes created on frequently queried fields
- Pagination limits result set size
- Relevance scoring done in database for efficiency
- Saved searches reduce repeated query construction

## Security Considerations

- All endpoints require authentication
- User context enforced in queries
- Saved searches scoped to user (private) or all users (public)
- SQL injection prevented through parameterized queries

## Future Enhancements

1. Update saved search (PUT endpoint)
2. Delete saved search (DELETE endpoint)
3. Share saved search with specific users
4. Search history tracking
5. Search suggestions based on usage patterns
6. Export search results
7. Scheduled searches with notifications

## Notes

- Implementation follows existing ThinkOnERP patterns
- Backward compatible with existing search functionality
- Database scripts must be executed before using new features
- Relevance score is optional and only populated when using advanced search
