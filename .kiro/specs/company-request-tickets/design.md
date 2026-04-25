# Design Document

## System Architecture

The Company Request Tickets system follows Clean Architecture principles with clear separation of concerns across four layers:

### Architecture Layers

```
┌─────────────────────────────────────────────────────────────┐
│                        API Layer                            │
│  - TicketsController                                        │
│  - TicketTypesController                                    │
│  - JWT Authentication & Authorization                       │
└─────────────────────────────────────────────────────────────┘
                                │
┌─────────────────────────────────────────────────────────────┐
│                   Application Layer                         │
│  - CQRS Commands & Queries (MediatR)                      │
│  - FluentValidation                                        │
│  - DTOs & Mapping                                          │
│  - Business Logic Services                                 │
└─────────────────────────────────────────────────────────────┘
                                │
┌─────────────────────────────────────────────────────────────┐
│                     Domain Layer                            │
│  - Entities (SysRequestTicket, SysTicketType, etc.)       │
│  - Repository Interfaces                                   │
│  - Domain Services                                         │
│  - Business Rules & Validation                             │
└─────────────────────────────────────────────────────────────┘
                                │
┌─────────────────────────────────────────────────────────────┐
│                 Infrastructure Layer                        │
│  - Oracle Repository Implementations                       │
│  - Stored Procedure Calls                                 │
│  - Email Notification Service                             │
│  - File Attachment Handling                               │
└─────────────────────────────────────────────────────────────┘
```

## Database Design

### Core Tables

#### SYS_REQUEST_TICKET
```sql
CREATE TABLE SYS_REQUEST_TICKET (
    ROW_ID NUMBER(19) PRIMARY KEY,
    TITLE_AR NVARCHAR2(200) NOT NULL,
    TITLE_EN NVARCHAR2(200) NOT NULL,
    DESCRIPTION NCLOB NOT NULL,
    COMPANY_ID NUMBER(19) NOT NULL,
    BRANCH_ID NUMBER(19) NOT NULL,
    REQUESTER_ID NUMBER(19) NOT NULL,
    ASSIGNEE_ID NUMBER(19) NULL,
    TICKET_TYPE_ID NUMBER(19) NOT NULL,
    TICKET_STATUS_ID NUMBER(19) NOT NULL,
    TICKET_PRIORITY_ID NUMBER(19) NOT NULL,
    TICKET_CATEGORY_ID NUMBER(19) NULL,
    EXPECTED_RESOLUTION_DATE DATE NULL,
    ACTUAL_RESOLUTION_DATE DATE NULL,
    IS_ACTIVE CHAR(1) DEFAULT 'Y' NOT NULL,
    CREATION_USER NVARCHAR2(100) NOT NULL,
    CREATION_DATE DATE DEFAULT SYSDATE NOT NULL,
    UPDATE_USER NVARCHAR2(100) NULL,
    UPDATE_DATE DATE NULL,
    
    CONSTRAINT FK_TICKET_COMPANY FOREIGN KEY (COMPANY_ID) REFERENCES SYS_COMPANY(ROW_ID),
    CONSTRAINT FK_TICKET_BRANCH FOREIGN KEY (BRANCH_ID) REFERENCES SYS_BRANCH(ROW_ID),
    CONSTRAINT FK_TICKET_REQUESTER FOREIGN KEY (REQUESTER_ID) REFERENCES SYS_USERS(ROW_ID),
    CONSTRAINT FK_TICKET_ASSIGNEE FOREIGN KEY (ASSIGNEE_ID) REFERENCES SYS_USERS(ROW_ID),
    CONSTRAINT FK_TICKET_TYPE FOREIGN KEY (TICKET_TYPE_ID) REFERENCES SYS_TICKET_TYPE(ROW_ID),
    CONSTRAINT FK_TICKET_STATUS FOREIGN KEY (TICKET_STATUS_ID) REFERENCES SYS_TICKET_STATUS(ROW_ID),
    CONSTRAINT FK_TICKET_PRIORITY FOREIGN KEY (TICKET_PRIORITY_ID) REFERENCES SYS_TICKET_PRIORITY(ROW_ID),
    CONSTRAINT FK_TICKET_CATEGORY FOREIGN KEY (TICKET_CATEGORY_ID) REFERENCES SYS_TICKET_CATEGORY(ROW_ID)
);

CREATE SEQUENCE SEQ_SYS_REQUEST_TICKET START WITH 1 INCREMENT BY 1;
```

#### SYS_TICKET_TYPE
```sql
CREATE TABLE SYS_TICKET_TYPE (
    ROW_ID NUMBER(19) PRIMARY KEY,
    TYPE_NAME_AR NVARCHAR2(100) NOT NULL,
    TYPE_NAME_EN NVARCHAR2(100) NOT NULL,
    DEFAULT_PRIORITY_ID NUMBER(19) NOT NULL,
    SLA_TARGET_HOURS NUMBER(10,2) NOT NULL,
    IS_ACTIVE CHAR(1) DEFAULT 'Y' NOT NULL,
    CREATION_USER NVARCHAR2(100) NOT NULL,
    CREATION_DATE DATE DEFAULT SYSDATE NOT NULL,
    UPDATE_USER NVARCHAR2(100) NULL,
    UPDATE_DATE DATE NULL,
    
    CONSTRAINT FK_TYPE_DEFAULT_PRIORITY FOREIGN KEY (DEFAULT_PRIORITY_ID) REFERENCES SYS_TICKET_PRIORITY(ROW_ID)
);

CREATE SEQUENCE SEQ_SYS_TICKET_TYPE START WITH 1 INCREMENT BY 1;
```

#### SYS_TICKET_STATUS
```sql
CREATE TABLE SYS_TICKET_STATUS (
    ROW_ID NUMBER(19) PRIMARY KEY,
    STATUS_NAME_AR NVARCHAR2(50) NOT NULL,
    STATUS_NAME_EN NVARCHAR2(50) NOT NULL,
    STATUS_CODE NVARCHAR2(20) NOT NULL UNIQUE,
    DISPLAY_ORDER NUMBER(3) NOT NULL,
    IS_FINAL_STATUS CHAR(1) DEFAULT 'N' NOT NULL,
    IS_ACTIVE CHAR(1) DEFAULT 'Y' NOT NULL,
    CREATION_USER NVARCHAR2(100) NOT NULL,
    CREATION_DATE DATE DEFAULT SYSDATE NOT NULL
);

CREATE SEQUENCE SEQ_SYS_TICKET_STATUS START WITH 1 INCREMENT BY 1;
```

#### SYS_TICKET_PRIORITY
```sql
CREATE TABLE SYS_TICKET_PRIORITY (
    ROW_ID NUMBER(19) PRIMARY KEY,
    PRIORITY_NAME_AR NVARCHAR2(50) NOT NULL,
    PRIORITY_NAME_EN NVARCHAR2(50) NOT NULL,
    PRIORITY_LEVEL NUMBER(1) NOT NULL,
    SLA_TARGET_HOURS NUMBER(10,2) NOT NULL,
    ESCALATION_THRESHOLD_HOURS NUMBER(10,2) NOT NULL,
    IS_ACTIVE CHAR(1) DEFAULT 'Y' NOT NULL,
    CREATION_USER NVARCHAR2(100) NOT NULL,
    CREATION_DATE DATE DEFAULT SYSDATE NOT NULL
);

CREATE SEQUENCE SEQ_SYS_TICKET_PRIORITY START WITH 1 INCREMENT BY 1;
```

#### SYS_TICKET_COMMENT
```sql
CREATE TABLE SYS_TICKET_COMMENT (
    ROW_ID NUMBER(19) PRIMARY KEY,
    TICKET_ID NUMBER(19) NOT NULL,
    COMMENT_TEXT NCLOB NOT NULL,
    IS_INTERNAL CHAR(1) DEFAULT 'N' NOT NULL,
    CREATION_USER NVARCHAR2(100) NOT NULL,
    CREATION_DATE DATE DEFAULT SYSDATE NOT NULL,
    
    CONSTRAINT FK_COMMENT_TICKET FOREIGN KEY (TICKET_ID) REFERENCES SYS_REQUEST_TICKET(ROW_ID)
);

CREATE SEQUENCE SEQ_SYS_TICKET_COMMENT START WITH 1 INCREMENT BY 1;
```

#### SYS_TICKET_ATTACHMENT
```sql
CREATE TABLE SYS_TICKET_ATTACHMENT (
    ROW_ID NUMBER(19) PRIMARY KEY,
    TICKET_ID NUMBER(19) NOT NULL,
    FILE_NAME NVARCHAR2(255) NOT NULL,
    FILE_SIZE NUMBER(19) NOT NULL,
    MIME_TYPE NVARCHAR2(100) NOT NULL,
    FILE_CONTENT BLOB NOT NULL,
    CREATION_USER NVARCHAR2(100) NOT NULL,
    CREATION_DATE DATE DEFAULT SYSDATE NOT NULL,
    
    CONSTRAINT FK_ATTACHMENT_TICKET FOREIGN KEY (TICKET_ID) REFERENCES SYS_REQUEST_TICKET(ROW_ID)
);

CREATE SEQUENCE SEQ_SYS_TICKET_ATTACHMENT START WITH 1 INCREMENT BY 1;
```

### Database Indexes

```sql
-- Performance indexes for frequent queries
CREATE INDEX IDX_TICKET_COMPANY_BRANCH ON SYS_REQUEST_TICKET(COMPANY_ID, BRANCH_ID);
CREATE INDEX IDX_TICKET_STATUS_PRIORITY ON SYS_REQUEST_TICKET(TICKET_STATUS_ID, TICKET_PRIORITY_ID);
CREATE INDEX IDX_TICKET_ASSIGNEE ON SYS_REQUEST_TICKET(ASSIGNEE_ID);
CREATE INDEX IDX_TICKET_CREATION_DATE ON SYS_REQUEST_TICKET(CREATION_DATE);
CREATE INDEX IDX_TICKET_RESOLUTION_DATE ON SYS_REQUEST_TICKET(ACTUAL_RESOLUTION_DATE);
CREATE INDEX IDX_TICKET_ACTIVE ON SYS_REQUEST_TICKET(IS_ACTIVE);

-- Full-text search indexes
CREATE INDEX IDX_TICKET_TITLE_AR ON SYS_REQUEST_TICKET(TITLE_AR);
CREATE INDEX IDX_TICKET_TITLE_EN ON SYS_REQUEST_TICKET(TITLE_EN);
```

## Domain Models

### Core Entities

#### SysRequestTicket
```csharp
public class SysRequestTicket
{
    public Int64 RowId { get; set; }
    public string TitleAr { get; set; } = string.Empty;
    public string TitleEn { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Int64 CompanyId { get; set; }
    public Int64 BranchId { get; set; }
    public Int64 RequesterId { get; set; }
    public Int64? AssigneeId { get; set; }
    public Int64 TicketTypeId { get; set; }
    public Int64 TicketStatusId { get; set; }
    public Int64 TicketPriorityId { get; set; }
    public Int64? TicketCategoryId { get; set; }
    public DateTime? ExpectedResolutionDate { get; set; }
    public DateTime? ActualResolutionDate { get; set; }
    public bool IsActive { get; set; }
    public string CreationUser { get; set; } = string.Empty;
    public DateTime? CreationDate { get; set; }
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }

    // Navigation properties
    public SysCompany? Company { get; set; }
    public SysBranch? Branch { get; set; }
    public SysUser? Requester { get; set; }
    public SysUser? Assignee { get; set; }
    public SysTicketType? TicketType { get; set; }
    public SysTicketStatus? TicketStatus { get; set; }
    public SysTicketPriority? TicketPriority { get; set; }
    public List<SysTicketComment> Comments { get; set; } = new();
    public List<SysTicketAttachment> Attachments { get; set; } = new();
}
```

#### SysTicketType
```csharp
public class SysTicketType
{
    public Int64 RowId { get; set; }
    public string TypeNameAr { get; set; } = string.Empty;
    public string TypeNameEn { get; set; } = string.Empty;
    public Int64 DefaultPriorityId { get; set; }
    public decimal SlaTargetHours { get; set; }
    public bool IsActive { get; set; }
    public string CreationUser { get; set; } = string.Empty;
    public DateTime? CreationDate { get; set; }
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }

    // Navigation properties
    public SysTicketPriority? DefaultPriority { get; set; }
}
```

## API Design

### RESTful Endpoints

#### Tickets Controller
```csharp
[ApiController]
[Route("api/tickets")]
[Authorize]
public class TicketsController : ControllerBase
{
    // GET /api/tickets - Get tickets with filtering and pagination
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<TicketDto>>>> GetTickets(
        [FromQuery] GetTicketsQuery query)

    // GET /api/tickets/{id} - Get specific ticket
    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<TicketDetailDto>>> GetTicketById(Int64 id)

    // POST /api/tickets - Create new ticket
    [HttpPost]
    public async Task<ActionResult<ApiResponse<Int64>>> CreateTicket(
        [FromBody] CreateTicketCommand command)

    // PUT /api/tickets/{id} - Update ticket
    [HttpPut("{id}")]
    public async Task<ActionResult<ApiResponse<Int64>>> UpdateTicket(
        Int64 id, [FromBody] UpdateTicketCommand command)

    // DELETE /api/tickets/{id} - Soft delete ticket (AdminOnly)
    [HttpDelete("{id}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<ApiResponse<Int64>>> DeleteTicket(Int64 id)

    // PUT /api/tickets/{id}/assign - Assign ticket (AdminOnly)
    [HttpPut("{id}/assign")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<ApiResponse<Int64>>> AssignTicket(
        Int64 id, [FromBody] AssignTicketCommand command)

    // PUT /api/tickets/{id}/status - Update ticket status
    [HttpPut("{id}/status")]
    public async Task<ActionResult<ApiResponse<Int64>>> UpdateTicketStatus(
        Int64 id, [FromBody] UpdateTicketStatusCommand command)

    // POST /api/tickets/{id}/comments - Add comment
    [HttpPost("{id}/comments")]
    public async Task<ActionResult<ApiResponse<Int64>>> AddComment(
        Int64 id, [FromBody] AddTicketCommentCommand command)

    // GET /api/tickets/{id}/comments - Get ticket comments
    [HttpGet("{id}/comments")]
    public async Task<ActionResult<ApiResponse<List<TicketCommentDto>>>> GetTicketComments(Int64 id)

    // POST /api/tickets/{id}/attachments - Upload attachment
    [HttpPost("{id}/attachments")]
    public async Task<ActionResult<ApiResponse<Int64>>> UploadAttachment(
        Int64 id, [FromBody] UploadAttachmentCommand command)

    // GET /api/tickets/{id}/attachments - List attachments
    [HttpGet("{id}/attachments")]
    public async Task<ActionResult<ApiResponse<List<TicketAttachmentDto>>>> GetAttachments(Int64 id)

    // GET /api/tickets/{id}/attachments/{attachmentId} - Download attachment
    [HttpGet("{id}/attachments/{attachmentId}")]
    public async Task<IActionResult> DownloadAttachment(Int64 id, Int64 attachmentId)
}
```

#### Ticket Types Controller
```csharp
[ApiController]
[Route("api/ticket-types")]
[Authorize]
public class TicketTypesController : ControllerBase
{
    // GET /api/ticket-types - Get all ticket types
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<TicketTypeDto>>>> GetTicketTypes()

    // GET /api/ticket-types/{id} - Get specific ticket type
    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<TicketTypeDto>>> GetTicketTypeById(Int64 id)

    // POST /api/ticket-types - Create ticket type (AdminOnly)
    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<ApiResponse<Int64>>> CreateTicketType(
        [FromBody] CreateTicketTypeCommand command)

    // PUT /api/ticket-types/{id} - Update ticket type (AdminOnly)
    [HttpPut("{id}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<ApiResponse<Int64>>> UpdateTicketType(
        Int64 id, [FromBody] UpdateTicketTypeCommand command)

    // DELETE /api/ticket-types/{id} - Delete ticket type (AdminOnly)
    [HttpDelete("{id}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<ApiResponse<Int64>>> DeleteTicketType(Int64 id)
}
```

## Application Services (CQRS)

### Commands

#### CreateTicketCommand
```csharp
public class CreateTicketCommand : IRequest<Int64>
{
    public string TitleAr { get; set; } = string.Empty;
    public string TitleEn { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Int64 CompanyId { get; set; }
    public Int64 BranchId { get; set; }
    public Int64 RequesterId { get; set; }
    public Int64 TicketTypeId { get; set; }
    public Int64 TicketPriorityId { get; set; }
    public Int64? TicketCategoryId { get; set; }
    public List<CreateAttachmentDto>? Attachments { get; set; }
    public string CreationUser { get; set; } = string.Empty;
}

public class CreateTicketCommandHandler : IRequestHandler<CreateTicketCommand, Int64>
{
    private readonly ITicketRepository _ticketRepository;
    private readonly INotificationService _notificationService;
    private readonly ILogger<CreateTicketCommandHandler> _logger;

    public async Task<Int64> Handle(CreateTicketCommand request, CancellationToken cancellationToken)
    {
        // Business logic for ticket creation
        // SLA calculation
        // Notification sending
        // Audit logging
    }
}
```

#### UpdateTicketStatusCommand
```csharp
public class UpdateTicketStatusCommand : IRequest<Int64>
{
    public Int64 TicketId { get; set; }
    public Int64 NewStatusId { get; set; }
    public string? StatusChangeReason { get; set; }
    public string UpdateUser { get; set; } = string.Empty;
}

public class UpdateTicketStatusCommandHandler : IRequestHandler<UpdateTicketStatusCommand, Int64>
{
    // Status transition validation
    // SLA compliance checking
    // Automatic resolution date setting
    // Notification triggers
}
```

### Queries

#### GetTicketsQuery
```csharp
public class GetTicketsQuery : IRequest<PagedResult<TicketDto>>
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SearchTerm { get; set; }
    public Int64? CompanyId { get; set; }
    public Int64? BranchId { get; set; }
    public Int64? AssigneeId { get; set; }
    public Int64? StatusId { get; set; }
    public Int64? PriorityId { get; set; }
    public Int64? TypeId { get; set; }
    public DateTime? CreatedFrom { get; set; }
    public DateTime? CreatedTo { get; set; }
    public string SortBy { get; set; } = "CreationDate";
    public string SortDirection { get; set; } = "DESC";
}

public class GetTicketsQueryHandler : IRequestHandler<GetTicketsQuery, PagedResult<TicketDto>>
{
    // Authorization filtering
    // Search implementation
    // Pagination
    // Sorting
}
```

## Security Design

### JWT Integration
```csharp
// Existing JWT authentication is used
// User claims provide CompanyId, BranchId, IsAdmin
// Authorization policies:
// - AdminOnly: User.IsAdmin == true
// - CompanyUser: User.CompanyId matches resource
// - BranchUser: User.BranchId matches resource
```

### Authorization Rules
```csharp
public class TicketAuthorizationService
{
    public bool CanViewTicket(ClaimsPrincipal user, SysRequestTicket ticket)
    {
        // Admin can view all tickets
        if (user.IsAdmin()) return true;
        
        // Users can view tickets from their company/branch
        var userCompanyId = user.GetCompanyId();
        var userBranchId = user.GetBranchId();
        
        return ticket.CompanyId == userCompanyId && ticket.BranchId == userBranchId;
    }

    public bool CanAssignTicket(ClaimsPrincipal user)
    {
        return user.IsAdmin();
    }

    public bool CanCommentOnTicket(ClaimsPrincipal user, SysRequestTicket ticket)
    {
        // Admin can comment on any ticket
        if (user.IsAdmin()) return true;
        
        // Requester can comment on their own tickets
        var userId = user.GetUserId();
        if (ticket.RequesterId == userId) return true;
        
        // Assignee can comment on assigned tickets
        if (ticket.AssigneeId == userId) return true;
        
        return false;
    }
}
```

## Data Flow

### Ticket Creation Flow
```
1. User submits CreateTicketCommand via API
2. FluentValidation validates input
3. Authorization checks user permissions
4. Command handler processes business logic:
   - Calculate SLA dates based on priority
   - Set initial status to "Open"
   - Process file attachments (Base64 → BLOB)
   - Create audit trail entry
5. Repository saves to database via stored procedure
6. Notification service sends alerts
7. API returns success response with ticket ID
```

### Status Update Flow
```
1. User submits UpdateTicketStatusCommand
2. Validation checks status transition rules
3. Authorization verifies user can update status
4. Handler processes status change:
   - Validate transition is allowed
   - Update resolution date if status = "Resolved"
   - Calculate SLA compliance
   - Create audit trail entry
5. Repository updates database
6. Notification service sends status change alerts
7. API returns success response
```

## File Attachment System

### Base64 Storage Design
```csharp
public class AttachmentService
{
    public async Task<Int64> SaveAttachmentAsync(string fileName, string base64Content, string mimeType)
    {
        // Validate file type
        if (!IsAllowedFileType(fileName, mimeType))
            throw new ArgumentException("File type not allowed");
        
        // Validate file size
        var fileBytes = Convert.FromBase64String(base64Content);
        if (fileBytes.Length > MaxFileSizeBytes)
            throw new ArgumentException("File size exceeds limit");
        
        // Save to database
        var attachment = new SysTicketAttachment
        {
            FileName = fileName,
            FileSize = fileBytes.Length,
            MimeType = mimeType,
            FileContent = fileBytes,
            CreationUser = currentUser,
            CreationDate = DateTime.Now
        };
        
        return await _attachmentRepository.CreateAsync(attachment);
    }
    
    private readonly string[] AllowedFileTypes = { 
        ".pdf", ".doc", ".docx", ".xls", ".xlsx", 
        ".jpg", ".jpeg", ".png", ".txt" 
    };
    
    private const int MaxFileSizeBytes = 10 * 1024 * 1024; // 10MB
}
```

## Notification System

### Email Notification Service
```csharp
public class TicketNotificationService : INotificationService
{
    public async Task SendTicketCreatedNotificationAsync(SysRequestTicket ticket)
    {
        var template = await GetNotificationTemplate("TicketCreated");
        var recipients = await GetNotificationRecipients(ticket);
        
        foreach (var recipient in recipients)
        {
            var emailContent = template.Render(new
            {
                TicketId = ticket.RowId,
                Title = ticket.TitleEn,
                Priority = ticket.TicketPriority?.PriorityNameEn,
                CreatedBy = ticket.Requester?.UserName,
                TicketUrl = GenerateTicketUrl(ticket.RowId)
            });
            
            await _emailService.SendAsync(recipient.Email, emailContent);
        }
    }
    
    public async Task SendSlaEscalationAlertAsync(SysRequestTicket ticket)
    {
        // Send escalation alerts for tickets approaching SLA deadline
    }
}
```

## Performance Considerations

### Caching Strategy
```csharp
public class CachedTicketTypeService
{
    private readonly IMemoryCache _cache;
    private readonly ITicketTypeRepository _repository;
    
    public async Task<List<TicketTypeDto>> GetAllTicketTypesAsync()
    {
        const string cacheKey = "ticket-types-all";
        
        if (_cache.TryGetValue(cacheKey, out List<TicketTypeDto> cachedTypes))
            return cachedTypes;
        
        var types = await _repository.GetAllAsync();
        var dtos = _mapper.Map<List<TicketTypeDto>>(types);
        
        _cache.Set(cacheKey, dtos, TimeSpan.FromMinutes(30));
        return dtos;
    }
}
```

### Pagination Implementation
```csharp
public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
}
```

## Stored Procedures

### Key Stored Procedures
```sql
-- SP_SYS_REQUEST_TICKET_INSERT
-- SP_SYS_REQUEST_TICKET_UPDATE
-- SP_SYS_REQUEST_TICKET_SELECT_ALL
-- SP_SYS_REQUEST_TICKET_SELECT_BY_ID
-- SP_SYS_REQUEST_TICKET_SELECT_FILTERED
-- SP_SYS_REQUEST_TICKET_UPDATE_STATUS
-- SP_SYS_REQUEST_TICKET_ASSIGN
-- SP_SYS_TICKET_COMMENT_INSERT
-- SP_SYS_TICKET_ATTACHMENT_INSERT
-- SP_SYS_TICKET_TYPE_SELECT_ALL
-- SP_SYS_TICKET_REPORTS_VOLUME
-- SP_SYS_TICKET_REPORTS_SLA_COMPLIANCE
```

## Integration Points

### Existing System Integration
```csharp
// Uses existing infrastructure:
// - OracleDbContext for database connections
// - JWT authentication middleware
// - ApiResponse wrapper format
// - Serilog logging configuration
// - FluentValidation pipeline
// - MediatR CQRS pattern
// - Exception handling middleware

// Integrates with existing tables:
// - SYS_COMPANY (foreign key)
// - SYS_BRANCH (foreign key)
// - SYS_USERS (foreign keys for requester/assignee)
```

## Error Handling

### Exception Handling Strategy
```csharp
public class TicketExceptionHandler : IExceptionHandler
{
    public async Task<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var response = exception switch
        {
            TicketNotFoundException ex => ApiResponse<object>.CreateFailure(ex.Message, 404),
            InvalidStatusTransitionException ex => ApiResponse<object>.CreateFailure(ex.Message, 400),
            UnauthorizedTicketAccessException ex => ApiResponse<object>.CreateFailure(ex.Message, 403),
            AttachmentSizeExceededException ex => ApiResponse<object>.CreateFailure(ex.Message, 400),
            _ => null
        };
        
        if (response != null)
        {
            context.Response.StatusCode = response.StatusCode;
            await context.Response.WriteAsJsonAsync(response, cancellationToken);
            return true;
        }
        
        return false;
    }
}
```

## Testing Strategy

### Unit Tests
```csharp
public class CreateTicketCommandHandlerTests
{
    [Fact]
    public async Task Handle_ValidCommand_CreatesTicketWithCorrectSla()
    {
        // Arrange
        var command = new CreateTicketCommand { /* test data */ };
        var handler = new CreateTicketCommandHandler(mockRepo, mockNotification, mockLogger);
        
        // Act
        var result = await handler.Handle(command, CancellationToken.None);
        
        // Assert
        Assert.True(result > 0);
        mockRepo.Verify(r => r.CreateAsync(It.IsAny<SysRequestTicket>()), Times.Once);
    }
}
```

### Integration Tests
```csharp
public class TicketsControllerIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task CreateTicket_ValidRequest_ReturnsCreatedTicket()
    {
        // Test full API endpoint with database
    }
}
```

This design provides a comprehensive, scalable, and maintainable ticket management system that integrates seamlessly with the existing ThinkOnERP architecture while following established patterns and conventions.