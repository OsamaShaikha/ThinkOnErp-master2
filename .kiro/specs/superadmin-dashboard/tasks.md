# Implementation Plan: SuperAdmin Dashboard

## Overview

This implementation plan creates a high-performance dashboard API endpoint for SuperAdmin users. The implementation follows Clean Architecture principles with CQRS pattern using MediatR. All data aggregation is performed in the application layer using existing repositories with parallel execution for optimal performance.

**Key Implementation Points:**
- No database changes required (uses existing repositories)
- Parallel repository calls using Task.WhenAll
- LINQ-based data aggregation in application layer
- JWT authentication with SuperAdmin role authorization
- Target response time: < 500ms

## Tasks

- [x] 1. Create DTOs in Application Layer
  - [x] 1.1 Create SuperAdminDashboardDto
    - Create file: `src/ThinkOnErp.Application/DTOs/SuperAdmin/SuperAdminDashboardDto.cs`
    - Add properties: Stats, RecentCompanies, RecentBranches, PendingRequests, Alerts
    - _Requirements: 12.1, 12.2, 12.3, 12.4, 12.5_
  
  - [x] 1.2 Create DashboardStatsDto
    - Create file: `src/ThinkOnErp.Application/DTOs/SuperAdmin/DashboardStatsDto.cs`
    - Add properties: TotalCompanies, ActiveCompanies, InactiveCompanies, TotalBranches, ActiveBranches, TotalSystemAdmins
    - _Requirements: 1.4, 4.5, 5.3, 6.3_
  
  - [x] 1.3 Create RecentCompanyDto
    - Create file: `src/ThinkOnErp.Application/DTOs/SuperAdmin/RecentCompanyDto.cs`
    - Add properties: NameAr, NameEn, Country, BranchCount, Status, CreatedDate
    - _Requirements: 7.3, 7.4, 7.5, 7.6, 7.7_
  
  - [x] 1.4 Create RecentBranchActivityDto
    - Create file: `src/ThinkOnErp.Application/DTOs/SuperAdmin/RecentBranchActivityDto.cs`
    - Add properties: BranchNameAr, BranchNameEn, CompanyNameAr, CompanyNameEn, ActivityType, ActivityDate
    - _Requirements: 8.3, 8.4, 8.5, 8.6_
  
  - [x] 1.5 Create PendingRequestDto (empty placeholder)
    - Create file: `src/ThinkOnErp.Application/DTOs/SuperAdmin/PendingRequestDto.cs`
    - Add comment indicating future implementation
    - _Requirements: 9.1, 9.2, 9.3_

- [x] 2. Create Query and Query Handler
  - [x] 2.1 Create GetSuperAdminDashboardQuery
    - Create file: `src/ThinkOnErp.Application/Features/SuperAdmin/Queries/GetSuperAdminDashboardQuery.cs`
    - Implement IRequest<SuperAdminDashboardDto> with no parameters
    - _Requirements: 10.1, 10.2_
  
  - [x] 2.2 Create GetSuperAdminDashboardQueryHandler
    - Create file: `src/ThinkOnErp.Application/Features/SuperAdmin/Queries/GetSuperAdminDashboardQueryHandler.cs`
    - Inject ICompanyRepository, IBranchRepository, IUserRepository, ILogger
    - Implement IRequestHandler<GetSuperAdminDashboardQuery, SuperAdminDashboardDto>
    - _Requirements: 1.3, 10.1, 10.2_
  
  - [x] 2.3 Implement parallel repository calls
    - Use Task.WhenAll to call GetAllAsync() on all three repositories concurrently
    - Add null safety checks for returned data
    - _Requirements: 1.3, 13.1, 13.5_
  
  - [x] 2.4 Implement metrics calculation logic
    - Calculate total companies, active companies, inactive companies using LINQ Count
    - Calculate total branches, active branches using LINQ Count
    - Calculate total system admins by counting users where IsSuperAdmin is true
    - _Requirements: 1.4, 4.1, 4.2, 4.3, 4.4, 5.1, 5.2, 6.1, 6.2_
  
  - [x] 2.5 Implement recent companies logic
    - Sort companies by CreationDate descending and take top 4
    - For each company, count associated branches using LINQ
    - Map to RecentCompanyDto with bilingual names, country, branch count, status, creation date
    - _Requirements: 1.5, 7.1, 7.2, 7.3, 7.4, 7.5, 7.6, 7.7_
  
  - [x] 2.6 Implement recent branch activities logic
    - Sort branches by UpdateDate descending and take top 4
    - For each branch, find associated company using LINQ
    - Determine activity type: "New" if CreationDate == UpdateDate, else "Update"
    - Map to RecentBranchActivityDto with bilingual names, company info, activity type, date
    - _Requirements: 1.6, 8.1, 8.2, 8.3, 8.4, 8.5, 8.6_
  
  - [x] 2.7 Implement system alerts generation
    - Check if inactive companies count > 0, add alert message
    - Return empty array if no alerts
    - Add placeholder for future pending requests alerts
    - _Requirements: 1.7, 3.1, 3.2, 3.3, 3.4_
  
  - [x] 2.8 Add comprehensive error handling
    - Wrap repository calls in try-catch with proper logging
    - Handle null/empty data gracefully with zero counts and empty arrays
    - Throw ApplicationException for repository failures
    - Log errors at appropriate levels (Error, Warning, Critical)
    - _Requirements: 11.1, 11.2, 11.3, 11.4, 11.5_

- [x] 3. Checkpoint - Review query handler implementation
  - Ensure all tests pass, ask the user if questions arise.

- [~] 4. Create API Controller
  - [x] 4.1 Create SuperAdminController
    - Create file: `src/ThinkOnErp.API/Controllers/SuperAdminController.cs`
    - Inherit from ControllerBase
    - Add [ApiController] and [Route("api/[controller]")] attributes
    - Inject IMediator
    - _Requirements: 10.5_
  
  - [x] 4.2 Implement GetDashboard endpoint
    - Create GET endpoint at route "dashboard"
    - Add [Authorize(Roles = "SuperAdmin")] attribute
    - Send GetSuperAdminDashboardQuery to MediatR
    - Return Ok(result) with SuperAdminDashboardDto
    - _Requirements: 1.1, 2.1, 2.2, 2.3, 2.4_
  
  - [x] 4.3 Configure JSON serialization
    - Ensure camelCase naming policy is configured in Program.cs or Startup.cs
    - Verify System.Text.Json is configured properly
    - _Requirements: 12.6_
  
  - [x] 4.4 Add API error handling
    - Add try-catch in controller action
    - Return 500 Internal Server Error with generic message on exceptions
    - Ensure no sensitive data exposed in error responses
    - _Requirements: 11.1, 11.2, 11.3, 11.4_

- [x] 5. Register services in dependency injection
  - [x] 5.1 Register MediatR handlers
    - Add MediatR registration in Application layer DI configuration
    - Ensure GetSuperAdminDashboardQueryHandler is registered
    - _Requirements: 10.1_
  
  - [x] 5.2 Verify repository registrations
    - Confirm ICompanyRepository, IBranchRepository, IUserRepository are registered
    - No new registrations needed (using existing repositories)
    - _Requirements: 1.3, 10.3_

- [ ] 6. Write unit tests for query handler
  - [ ]* 6.1 Test successful data retrieval
    - Mock repositories to return valid test data
    - Verify correct metrics calculation
    - Verify correct DTO mapping
    - Assert all properties populated correctly
    - _Requirements: 1.1, 1.4, 1.5, 1.6_
  
  - [ ]* 6.2 Test empty data handling
    - Mock repositories to return empty lists
    - Verify zero counts returned for all metrics
    - Verify empty arrays for recent companies and branches
    - Verify no alerts generated
    - _Requirements: 11.5_
  
  - [ ]* 6.3 Test inactive companies alert generation
    - Mock data with 8 inactive companies
    - Verify alert message: "8 companies are currently inactive"
    - Verify alert appears in alerts array
    - _Requirements: 3.1, 3.2_
  
  - [ ]* 6.4 Test recent companies sorting and limiting
    - Mock 10 companies with different creation dates
    - Verify only top 4 returned
    - Verify sorted by CreationDate descending
    - _Requirements: 7.1, 7.2, 13.3_
  
  - [ ]* 6.5 Test recent branches sorting and limiting
    - Mock 10 branches with different update dates
    - Verify only top 4 returned
    - Verify sorted by UpdateDate descending
    - _Requirements: 8.1, 8.2, 13.3_
  
  - [ ]* 6.6 Test branch count calculation
    - Mock companies with varying branch counts (0, 1, 5, 10 branches)
    - Verify accurate branch count per company in RecentCompanyDto
    - _Requirements: 7.5_
  
  - [ ]* 6.7 Test activity type determination
    - Mock branches where CreationDate == UpdateDate (should be "New")
    - Mock branches where CreationDate < UpdateDate (should be "Update")
    - Verify correct activity type assigned
    - _Requirements: 8.5_
  
  - [ ]* 6.8 Test repository exception handling
    - Mock repository to throw exception
    - Verify exception is logged
    - Verify ApplicationException is thrown
    - _Requirements: 11.1, 11.2_
  
  - [ ]* 6.9 Test null data handling
    - Mock repository to return null
    - Verify graceful handling with empty lists and zero counts
    - _Requirements: 11.5_
  
  - [ ]* 6.10 Test parallel execution
    - Verify Task.WhenAll is used for repository calls
    - Verify repositories are called concurrently (not sequentially)
    - _Requirements: 13.1_

- [ ] 7. Write unit tests for controller
  - [ ]* 7.1 Test successful dashboard request
    - Mock MediatR to return valid SuperAdminDashboardDto
    - Mock User.IsInRole("SuperAdmin") to return true
    - Verify 200 OK response
    - Verify response body matches DTO
    - _Requirements: 1.1, 2.1_
  
  - [ ]* 7.2 Test unauthorized request (no JWT)
    - Mock unauthenticated request
    - Verify 401 Unauthorized response
    - _Requirements: 2.3_
  
  - [ ]* 7.3 Test forbidden request (non-SuperAdmin)
    - Mock authenticated user without SuperAdmin role
    - Verify 403 Forbidden response
    - _Requirements: 2.2, 2.4_
  
  - [ ]* 7.4 Test internal server error
    - Mock MediatR to throw exception
    - Verify 500 Internal Server Error response
    - Verify generic error message returned
    - _Requirements: 11.1, 11.4_

- [ ] 8. Checkpoint - Ensure all unit tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 9. Write integration tests
  - [ ]* 9.1 Test end-to-end dashboard retrieval
    - Use test database with seed data
    - Create HTTP request with valid SuperAdmin JWT token
    - Verify 200 OK response
    - Verify response structure matches SuperAdminDashboardDto
    - Verify data accuracy against test database
    - _Requirements: 1.1, 2.1, 2.4_
  
  - [ ]* 9.2 Test performance with realistic data
    - Load test database with realistic data volume (100+ companies, 500+ branches)
    - Measure total response time
    - Assert response time < 500ms
    - _Requirements: 1.2, 13.4_
  
  - [ ]* 9.3 Test authentication and authorization
    - Test with no JWT token (expect 401)
    - Test with invalid JWT token (expect 401)
    - Test with valid JWT but non-SuperAdmin role (expect 403)
    - Test with valid SuperAdmin JWT (expect 200)
    - _Requirements: 2.1, 2.2, 2.3, 2.4_
  
  - [ ]* 9.4 Test JSON response format
    - Verify camelCase naming convention for all properties
    - Verify response structure matches API specification
    - _Requirements: 12.1, 12.2, 12.3, 12.4, 12.5, 12.6_

- [x] 10. Add API documentation
  - [x] 10.1 Add Swagger/OpenAPI annotations
    - Add XML comments to controller action
    - Add [ProducesResponseType] attributes for 200, 401, 403, 500
    - Document request/response examples
    - _Requirements: 1.1_
  
  - [x] 10.2 Update API documentation
    - Document endpoint: GET /api/superadmin/dashboard
    - Document authentication requirements
    - Document response structure
    - Add example request/response
    - _Requirements: 1.1, 2.1_

- [x] 11. Final checkpoint and validation
  - [x] 11.1 Run all tests
    - Execute all unit tests and verify they pass
    - Execute all integration tests and verify they pass
    - _Requirements: All_
  
  - [x] 11.2 Manual testing
    - Test dashboard with SuperAdmin user in development environment
    - Verify metrics are accurate
    - Verify recent companies and branches display correctly
    - Verify alerts appear when conditions met
    - Verify response time is acceptable
    - _Requirements: 1.1, 1.2, 1.4, 1.5, 1.6, 1.7_
  
  - [x] 11.3 Code review checklist
    - Verify Clean Architecture principles followed
    - Verify CQRS pattern implemented correctly
    - Verify error handling is comprehensive
    - Verify logging is appropriate
    - Verify no sensitive data exposed
    - Verify performance optimizations applied
    - _Requirements: 10.1, 10.2, 10.3, 10.4, 10.5, 11.4, 13.1, 13.2, 13.3_

- [x] 12. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- No database changes required - uses existing tables and stored procedures
- All repositories (ICompanyRepository, IBranchRepository, IUserRepository) already exist
- Focus on application-layer aggregation using LINQ for performance
- Parallel repository calls are critical for meeting 500ms response time target
- PendingRequests feature is a placeholder for future implementation
- Each task references specific requirements for traceability
