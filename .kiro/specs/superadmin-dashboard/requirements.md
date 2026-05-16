# Requirements Document

## Introduction

The SuperAdmin Dashboard feature provides a high-performance, single-request API endpoint that delivers comprehensive system metrics, alerts, and activity summaries for SuperAdmin users of the ThinkOnErp ERP system. This dashboard enables SuperAdmins to monitor system health, track company and branch activity, and identify pending actions requiring attention.

## Glossary

- **SuperAdmin**: A privileged user with platform-wide access to all companies and system management functions
- **Dashboard_API**: The API endpoint that returns all dashboard data in a single optimized request
- **Company**: A tenant organization within the multi-tenant ERP system
- **Branch**: A physical or logical subdivision of a Company
- **System_Alert**: A notification generated when specific conditions require SuperAdmin attention
- **Pending_Request**: A request awaiting SuperAdmin approval or action (optional feature)
- **Dashboard_Metrics**: Aggregated statistical data displayed as summary cards
- **Activity_Record**: A timestamped record of system changes or events
- **Repository**: An existing data access layer component that queries the database using stored procedures
- **Dashboard_Service**: A service layer component that aggregates data from multiple repositories

## Requirements

### Requirement 1: Dashboard Data Retrieval

**User Story:** As a SuperAdmin, I want to retrieve all dashboard data in a single API request, so that I can view system metrics and activity without performance delays.

#### Acceptance Criteria

1. WHEN a SuperAdmin requests dashboard data, THE Dashboard_API SHALL return all metrics, alerts, and activity lists in a single response
2. THE Dashboard_API SHALL complete the request within 500 milliseconds
3. THE Dashboard_Service SHALL use existing repositories (ICompanyRepository, IBranchRepository, IUserRepository) to retrieve data
4. THE Dashboard_API SHALL return metrics for total companies, active companies, inactive companies, total branches, active branches, and total system admins
5. THE Dashboard_API SHALL return the top 4 recently created companies with name, country, branch count, status, and creation date
6. THE Dashboard_API SHALL return the top 4 recent branch activities with branch name, company name, activity type, and date
7. THE Dashboard_API SHALL support optional pending requests feature if implemented in the future (top 4 when available)

### Requirement 2: Authentication and Authorization

**User Story:** As a system administrator, I want to ensure only SuperAdmin users can access the dashboard, so that sensitive system-wide data remains secure.

#### Acceptance Criteria

1. THE Dashboard_API SHALL require JWT authentication
2. WHEN a non-SuperAdmin user attempts to access the dashboard, THE Dashboard_API SHALL return HTTP 403 Forbidden
3. WHEN an unauthenticated user attempts to access the dashboard, THE Dashboard_API SHALL return HTTP 401 Unauthorized
4. THE Dashboard_API SHALL validate the SuperAdmin role claim in the JWT token

### Requirement 3: System Alerts Generation

**User Story:** As a SuperAdmin, I want to see system alerts for conditions requiring attention, so that I can proactively address issues.

#### Acceptance Criteria

1. WHEN inactive companies count is greater than zero, THE Dashboard_API SHALL include an alert message indicating the number of inactive companies
2. THE Dashboard_API SHALL return alerts as an array of string messages
3. WHEN no alert conditions exist, THE Dashboard_API SHALL return an empty alerts array
4. THE Dashboard_API SHALL support future alert types for pending requests if that feature is implemented

### Requirement 4: Company Metrics Calculation

**User Story:** As a SuperAdmin, I want to view accurate company statistics, so that I can understand the current state of tenant organizations.

#### Acceptance Criteria

1. THE Dashboard_Service SHALL use ICompanyRepository.GetAllAsync() to retrieve all companies
2. THE Dashboard_Service SHALL calculate total companies by counting all retrieved company records
3. THE Dashboard_Service SHALL calculate active companies by counting companies where IsActive equals true
4. THE Dashboard_Service SHALL calculate inactive companies by counting companies where IsActive equals false
5. THE Dashboard_API SHALL return all company metrics as integer values

### Requirement 5: Branch Metrics Calculation

**User Story:** As a SuperAdmin, I want to view accurate branch statistics, so that I can monitor the distribution of organizational units.

#### Acceptance Criteria

1. THE Dashboard_Service SHALL use IBranchRepository.GetAllAsync() to retrieve all branches
2. THE Dashboard_Service SHALL calculate total branches by counting all retrieved branch records
3. THE Dashboard_Service SHALL calculate active branches by counting branches where IsActive equals true
4. THE Dashboard_API SHALL return all branch metrics as integer values

### Requirement 6: System Admin Count

**User Story:** As a SuperAdmin, I want to know how many system administrators exist, so that I can track administrative access levels.

#### Acceptance Criteria

1. THE Dashboard_Service SHALL use IUserRepository.GetAllAsync() to retrieve all users
2. THE Dashboard_Service SHALL calculate total system admins by counting users where IsSuperAdmin equals true
3. THE Dashboard_API SHALL return the system admin count as an integer value

### Requirement 7: Recent Company Activity

**User Story:** As a SuperAdmin, I want to see recently created companies, so that I can monitor new tenant onboarding.

#### Acceptance Criteria

1. THE Dashboard_Service SHALL use ICompanyRepository.GetAllAsync() to retrieve all companies
2. THE Dashboard_Service SHALL sort companies by CreationDate in descending order and take the top 4
3. FOR EACH recent company, THE Dashboard_Service SHALL return the company name in both Arabic (RowDesc) and English (RowDescE)
4. FOR EACH recent company, THE Dashboard_Service SHALL return the country if available
5. FOR EACH recent company, THE Dashboard_Service SHALL use IBranchRepository to count the number of associated branches
6. FOR EACH recent company, THE Dashboard_Service SHALL return the status as Active or Inactive based on IsActive flag
7. FOR EACH recent company, THE Dashboard_Service SHALL return the creation date

### Requirement 8: Branch Activity Tracking

**User Story:** As a SuperAdmin, I want to see recent branch activity, so that I can monitor organizational structure changes.

#### Acceptance Criteria

1. THE Dashboard_Service SHALL use IBranchRepository.GetAllAsync() to retrieve all branches
2. THE Dashboard_Service SHALL sort branches by UpdateDate in descending order and take the top 4
3. FOR EACH branch activity, THE Dashboard_Service SHALL return the branch name in both Arabic (RowDesc) and English (RowDescE)
4. FOR EACH branch activity, THE Dashboard_Service SHALL use ICompanyRepository to retrieve and return the associated company name
5. FOR EACH branch activity, THE Dashboard_Service SHALL determine activity type as New or Update by comparing CreationDate and UpdateDate
6. FOR EACH branch activity, THE Dashboard_Service SHALL return the activity date (UpdateDate or CreationDate)

### Requirement 9: Pending Requests Management (Optional)

**User Story:** As a SuperAdmin, I want to see pending requests requiring action, so that I can prioritize administrative tasks.

#### Acceptance Criteria

1. THE Dashboard_API SHALL return an empty pending requests array for the initial implementation
2. THE Dashboard_Service SHALL be designed to support future pending requests feature
3. THE response structure SHALL include a pendingRequests array field for future extensibility
4. WHEN pending requests feature is implemented in the future, THE Dashboard_Service SHALL retrieve and return the top 4 pending requests

### Requirement 10: Clean Architecture Implementation

**User Story:** As a developer, I want the dashboard feature to follow Clean Architecture principles, so that the codebase remains maintainable and testable.

#### Acceptance Criteria

1. THE Dashboard_API SHALL implement CQRS pattern using MediatR with a GetSuperAdminDashboardQuery
2. THE Application layer SHALL contain the query handler that orchestrates dashboard data retrieval
3. THE Infrastructure layer SHALL contain the repository implementation using ADO.NET for Oracle database access
4. THE Domain layer SHALL contain entity definitions for dashboard data structures
5. THE API layer SHALL contain the controller that exposes the dashboard endpoint

### Requirement 11: Error Handling

**User Story:** As a developer, I want proper error handling for dashboard requests, so that failures are logged and communicated clearly.

#### Acceptance Criteria

1. WHEN a repository call fails, THE Dashboard_API SHALL return HTTP 500 Internal Server Error with a generic error message
2. WHEN data retrieval fails, THE Dashboard_API SHALL log the error details and return HTTP 500 Internal Server Error
3. WHEN an unexpected exception occurs, THE Dashboard_API SHALL log the exception and return HTTP 500 Internal Server Error
4. THE Dashboard_API SHALL NOT expose internal error details or database schema information in error responses
5. THE Dashboard_Service SHALL handle null or empty data gracefully and return zero counts for missing data

### Requirement 12: Response Format

**User Story:** As a frontend developer, I want a consistent JSON response format, so that I can reliably parse and display dashboard data.

#### Acceptance Criteria

1. THE Dashboard_API SHALL return a JSON response with a stats object containing all metric values
2. THE Dashboard_API SHALL return a JSON response with a recentCompanies array containing company objects
3. THE Dashboard_API SHALL return a JSON response with a recentBranches array containing branch activity objects
4. THE Dashboard_API SHALL return a JSON response with a pendingRequests array containing request objects
5. THE Dashboard_API SHALL return a JSON response with an alerts array containing alert message strings
6. THE Dashboard_API SHALL use camelCase naming convention for all JSON property names

### Requirement 13: Performance Optimization

**User Story:** As a system administrator, I want the dashboard to perform efficiently, so that it does not impact overall system performance.

#### Acceptance Criteria

1. THE Dashboard_Service SHALL execute repository calls in parallel using Task.WhenAll where possible
2. THE Dashboard_Service SHALL use LINQ operations efficiently to minimize memory allocation
3. THE Dashboard_Service SHALL limit result sets to 4 records for activity lists using Take(4)
4. THE Dashboard_API SHALL complete the request within 500 milliseconds under normal load
5. THE Dashboard_Service SHALL reuse existing repository methods without creating new database queries
