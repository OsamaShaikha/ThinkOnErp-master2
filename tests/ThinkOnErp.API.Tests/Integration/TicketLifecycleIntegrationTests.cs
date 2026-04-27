using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Auth;
using ThinkOnErp.Application.DTOs.Ticket;
using ThinkOnErp.Application.Features.Tickets.Commands.AddTicketComment;
using ThinkOnErp.Application.Features.Tickets.Commands.AssignTicket;
using ThinkOnErp.Application.Features.Tickets.Commands.CreateTicket;
using ThinkOnErp.Application.Features.Tickets.Commands.UpdateTicketStatus;
using ThinkOnErp.Application.Features.Tickets.Commands.UploadAttachment;
using Xunit;

namespace ThinkOnErp.API.Tests.Integration;

/// <summary>
/// Integration tests for complete ticket lifecycle workflows.
/// Validates: Requirements 20.6, 20.9
/// </summary>
public class TicketLifecycleIntegrationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly string _adminToken;

    public TicketLifecycleIntegrationTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        // Get admin token for tests
        var loginDto = new LoginDto { UserName = "admin", Password = "admin123" };
        var loginResponse = _client.PostAsJsonAsync("/api/auth/login", loginDto).GetAwaiter().GetResult();
        var loginResult = loginResponse.Content.ReadFromJsonAsync<ApiResponse<TokenDto>>().GetAwaiter().GetResult();
        _adminToken = loginResult?.Data?.AccessToken ?? "";
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);
    }

    [Fact]
    public async Task CompleteTicketLifecycle_CreateToResolution_Succeeds()
    {
        // Step 1: Create a new ticket
        var createCommand = new CreateTicketCommand
        {
            TitleAr = "طلب دعم فني",
            TitleEn = "Technical Support Request",
            Description = "Need help with system configuration and setup",
            CompanyId = 1,
            BranchId = 1,
            RequesterId = 1,
            TicketTypeId = 1,
            TicketPriorityId = 2, // Medium priority
            CreationUser = "admin"
        };

        var createResponse = await _client.PostAsJsonAsync("/api/tickets", createCommand);
        var createResult = await createResponse.Content.ReadFromJsonAsync<ApiResponse<Int64>>();
        
        Assert.True(createResponse.IsSuccessStatusCode, "Ticket creation should succeed");
        Assert.NotNull(createResult);
        Assert.True(createResult.Data > 0, "Created ticket should have valid ID");
        
        var ticketId = createResult.Data;

        // Step 2: Retrieve the created ticket
        var getResponse = await _client.GetAsync($"/api/tickets/{ticketId}");
        var getResult = await getResponse.Content.ReadFromJsonAsync<ApiResponse<TicketDetailDto>>();
        
        Assert.True(getResponse.IsSuccessStatusCode, "Ticket retrieval should succeed");
        Assert.NotNull(getResult?.Data);
        Assert.Equal("Technical Support Request", getResult.Data.TitleEn);
        Assert.Equal(1, getResult.Data.TicketStatusId); // Should be "Open" status

        // Step 3: Add a comment to the ticket
        var commentCommand = new AddTicketCommentCommand
        {
            TicketId = ticketId,
            CommentText = "Initial investigation started. Reviewing system logs.",
            IsInternal = false,
            CreationUser = "admin"
        };

        var commentResponse = await _client.PostAsJsonAsync($"/api/tickets/{ticketId}/comments", commentCommand);
        Assert.True(commentResponse.IsSuccessStatusCode, "Adding comment should succeed");

        // Step 4: Assign the ticket to an admin user
        var assignCommand = new AssignTicketCommand
        {
            TicketId = ticketId,
            AssigneeId = 1, // Assign to admin user
            AssignmentReason = "Assigned to technical support team",
            UpdateUser = "admin"
        };

        var assignResponse = await _client.PutAsJsonAsync($"/api/tickets/{ticketId}/assign", assignCommand);
        Assert.True(assignResponse.IsSuccessStatusCode, "Ticket assignment should succeed");

        // Step 5: Update ticket status to "In Progress"
        var statusCommand = new UpdateTicketStatusCommand
        {
            TicketId = ticketId,
            NewStatusId = 2, // In Progress
            StatusChangeReason = "Started working on the issue",
            UpdateUser = "admin"
        };

        var statusResponse = await _client.PutAsJsonAsync($"/api/tickets/{ticketId}/status", statusCommand);
        Assert.True(statusResponse.IsSuccessStatusCode, "Status update should succeed");

        // Step 6: Add another comment with progress update
        var progressComment = new AddTicketCommentCommand
        {
            TicketId = ticketId,
            CommentText = "Configuration issue identified. Applying fix.",
            IsInternal = false,
            CreationUser = "admin"
        };

        var progressCommentResponse = await _client.PostAsJsonAsync($"/api/tickets/{ticketId}/comments", progressComment);
        Assert.True(progressCommentResponse.IsSuccessStatusCode, "Progress comment should be added");

        // Step 7: Update status to "Resolved"
        var resolveCommand = new UpdateTicketStatusCommand
        {
            TicketId = ticketId,
            NewStatusId = 4, // Resolved
            StatusChangeReason = "Issue fixed and tested successfully",
            UpdateUser = "admin"
        };

        var resolveResponse = await _client.PutAsJsonAsync($"/api/tickets/{ticketId}/status", resolveCommand);
        Assert.True(resolveResponse.IsSuccessStatusCode, "Ticket resolution should succeed");

        // Step 8: Verify final ticket state
        var finalGetResponse = await _client.GetAsync($"/api/tickets/{ticketId}");
        var finalGetResult = await finalGetResponse.Content.ReadFromJsonAsync<ApiResponse<TicketDetailDto>>();
        
        Assert.NotNull(finalGetResult?.Data);
        Assert.Equal(4, finalGetResult.Data.TicketStatusId); // Resolved status
        Assert.NotNull(finalGetResult.Data.ActualResolutionDate); // Should have resolution date
        Assert.Equal(2, finalGetResult.Data.Comments.Count); // Should have 2 comments
        Assert.Equal(1, finalGetResult.Data.AssigneeId); // Should be assigned

        // Step 9: Verify ticket appears in list
        var listResponse = await _client.GetAsync("/api/tickets");
        var listResult = await listResponse.Content.ReadFromJsonAsync<ApiResponse<PagedResult<TicketDto>>>();
        
        Assert.NotNull(listResult?.Data);
        Assert.Contains(listResult.Data.Items, t => t.TicketId == ticketId);
    }

    [Fact]
    public async Task TicketWithAttachments_UploadAndDownload_Succeeds()
    {
        // Step 1: Create ticket with attachment
        var createCommand = new CreateTicketCommand
        {
            TitleAr = "طلب مع مرفقات",
            TitleEn = "Request with Attachments",
            Description = "This ticket includes file attachments for reference",
            CompanyId = 1,
            BranchId = 1,
            RequesterId = 1,
            TicketTypeId = 1,
            TicketPriorityId = 1,
            Attachments = new List<CreateAttachmentDto>
            {
                new CreateAttachmentDto
                {
                    FileName = "test-document.txt",
                    FileContent = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("Test file content")),
                    MimeType = "text/plain"
                }
            },
            CreationUser = "admin"
        };

        var createResponse = await _client.PostAsJsonAsync("/api/tickets", createCommand);
        var createResult = await createResponse.Content.ReadFromJsonAsync<ApiResponse<Int64>>();
        
        Assert.True(createResponse.IsSuccessStatusCode);
        var ticketId = createResult!.Data;

        // Step 2: Retrieve ticket and verify attachment
        var getResponse = await _client.GetAsync($"/api/tickets/{ticketId}");
        var getResult = await getResponse.Content.ReadFromJsonAsync<ApiResponse<TicketDetailDto>>();
        
        Assert.NotNull(getResult?.Data);
        Assert.Single(getResult.Data.Attachments);
        Assert.Equal("test-document.txt", getResult.Data.Attachments[0].FileName);

        var attachmentId = getResult.Data.Attachments[0].AttachmentId;

        // Step 3: Upload additional attachment
        var uploadCommand = new UploadAttachmentCommand
        {
            TicketId = ticketId,
            FileName = "additional-file.txt",
            FileContent = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("Additional content")),
            MimeType = "text/plain",
            CreationUser = "admin"
        };

        var uploadResponse = await _client.PostAsJsonAsync($"/api/tickets/{ticketId}/attachments", uploadCommand);
        Assert.True(uploadResponse.IsSuccessStatusCode);

        // Step 4: List all attachments
        var listAttachmentsResponse = await _client.GetAsync($"/api/tickets/{ticketId}/attachments");
        var listAttachmentsResult = await listAttachmentsResponse.Content.ReadFromJsonAsync<ApiResponse<List<TicketAttachmentDto>>>();
        
        Assert.NotNull(listAttachmentsResult?.Data);
        Assert.Equal(2, listAttachmentsResult.Data.Count);

        // Step 5: Download attachment
        var downloadResponse = await _client.GetAsync($"/api/tickets/{ticketId}/attachments/{attachmentId}");
        Assert.True(downloadResponse.IsSuccessStatusCode);
        Assert.NotNull(downloadResponse.Content.Headers.ContentType);
    }

    [Fact]
    public async Task MultipleTickets_FilteringAndPagination_WorksCorrectly()
    {
        // Create multiple tickets with different properties
        var tickets = new List<Int64>();

        for (int i = 0; i < 5; i++)
        {
            var createCommand = new CreateTicketCommand
            {
                TitleAr = $"تذكرة اختبار {i + 1}",
                TitleEn = $"Test Ticket {i + 1}",
                Description = $"Description for test ticket {i + 1}",
                CompanyId = 1,
                BranchId = 1,
                RequesterId = 1,
                TicketTypeId = 1,
                TicketPriorityId = (i % 3) + 1, // Vary priorities
                CreationUser = "admin"
            };

            var response = await _client.PostAsJsonAsync("/api/tickets", createCommand);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<Int64>>();
            
            if (result?.Data > 0)
            {
                tickets.Add(result.Data);
            }
        }

        Assert.True(tickets.Count >= 5, "Should create at least 5 tickets");

        // Test filtering by priority
        var filterResponse = await _client.GetAsync("/api/tickets?PriorityId=1&PageSize=10");
        var filterResult = await filterResponse.Content.ReadFromJsonAsync<ApiResponse<PagedResult<TicketDto>>>();
        
        Assert.NotNull(filterResult?.Data);
        Assert.All(filterResult.Data.Items, t => Assert.Equal(1, t.TicketPriorityId));

        // Test pagination
        var page1Response = await _client.GetAsync("/api/tickets?Page=1&PageSize=2");
        var page1Result = await page1Response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<TicketDto>>>();
        
        Assert.NotNull(page1Result?.Data);
        Assert.True(page1Result.Data.Items.Count <= 2);
        Assert.True(page1Result.Data.TotalCount > 0);

        // Test search
        var searchResponse = await _client.GetAsync("/api/tickets?SearchTerm=Test");
        var searchResult = await searchResponse.Content.ReadFromJsonAsync<ApiResponse<PagedResult<TicketDto>>>();
        
        Assert.NotNull(searchResult?.Data);
        Assert.True(searchResult.Data.Items.Count > 0);
    }

    [Fact]
    public async Task TicketComments_InternalAndPublic_VisibilityWorksCorrectly()
    {
        // Create a ticket
        var createCommand = new CreateTicketCommand
        {
            TitleAr = "تذكرة التعليقات",
            TitleEn = "Comments Test Ticket",
            Description = "Testing comment visibility",
            CompanyId = 1,
            BranchId = 1,
            RequesterId = 1,
            TicketTypeId = 1,
            TicketPriorityId = 2,
            CreationUser = "admin"
        };

        var createResponse = await _client.PostAsJsonAsync("/api/tickets", createCommand);
        var createResult = await createResponse.Content.ReadFromJsonAsync<ApiResponse<Int64>>();
        var ticketId = createResult!.Data;

        // Add public comment
        var publicComment = new AddTicketCommentCommand
        {
            TicketId = ticketId,
            CommentText = "This is a public comment visible to all",
            IsInternal = false,
            CreationUser = "admin"
        };

        var publicResponse = await _client.PostAsJsonAsync($"/api/tickets/{ticketId}/comments", publicComment);
        Assert.True(publicResponse.IsSuccessStatusCode);

        // Add internal comment
        var internalComment = new AddTicketCommentCommand
        {
            TicketId = ticketId,
            CommentText = "This is an internal comment for admins only",
            IsInternal = true,
            CreationUser = "admin"
        };

        var internalResponse = await _client.PostAsJsonAsync($"/api/tickets/{ticketId}/comments", internalComment);
        Assert.True(internalResponse.IsSuccessStatusCode);

        // Retrieve comments
        var commentsResponse = await _client.GetAsync($"/api/tickets/{ticketId}/comments");
        var commentsResult = await commentsResponse.Content.ReadFromJsonAsync<ApiResponse<List<TicketCommentDto>>>();
        
        Assert.NotNull(commentsResult?.Data);
        Assert.Equal(2, commentsResult.Data.Count);
        Assert.Contains(commentsResult.Data, c => !c.IsInternal && c.CommentText.Contains("public"));
        Assert.Contains(commentsResult.Data, c => c.IsInternal && c.CommentText.Contains("internal"));
    }

    [Fact]
    public async Task TicketStatusTransitions_FollowWorkflowRules_Succeeds()
    {
        // Create a ticket (starts in "Open" status)
        var createCommand = new CreateTicketCommand
        {
            TitleAr = "تذكرة سير العمل",
            TitleEn = "Workflow Test Ticket",
            Description = "Testing status workflow transitions",
            CompanyId = 1,
            BranchId = 1,
            RequesterId = 1,
            TicketTypeId = 1,
            TicketPriorityId = 3,
            CreationUser = "admin"
        };

        var createResponse = await _client.PostAsJsonAsync("/api/tickets", createCommand);
        var createResult = await createResponse.Content.ReadFromJsonAsync<ApiResponse<Int64>>();
        var ticketId = createResult!.Data;

        // Transition: Open -> In Progress
        var toInProgressCommand = new UpdateTicketStatusCommand
        {
            TicketId = ticketId,
            NewStatusId = 2, // In Progress
            StatusChangeReason = "Starting work",
            UpdateUser = "admin"
        };

        var toInProgressResponse = await _client.PutAsJsonAsync($"/api/tickets/{ticketId}/status", toInProgressCommand);
        Assert.True(toInProgressResponse.IsSuccessStatusCode);

        // Transition: In Progress -> Pending Customer
        var toPendingCommand = new UpdateTicketStatusCommand
        {
            TicketId = ticketId,
            NewStatusId = 3, // Pending Customer
            StatusChangeReason = "Waiting for customer information",
            UpdateUser = "admin"
        };

        var toPendingResponse = await _client.PutAsJsonAsync($"/api/tickets/{ticketId}/status", toPendingCommand);
        Assert.True(toPendingResponse.IsSuccessStatusCode);

        // Transition: Pending Customer -> In Progress
        var backToInProgressCommand = new UpdateTicketStatusCommand
        {
            TicketId = ticketId,
            NewStatusId = 2, // In Progress
            StatusChangeReason = "Customer provided information",
            UpdateUser = "admin"
        };

        var backToInProgressResponse = await _client.PutAsJsonAsync($"/api/tickets/{ticketId}/status", backToInProgressCommand);
        Assert.True(backToInProgressResponse.IsSuccessStatusCode);

        // Transition: In Progress -> Resolved
        var toResolvedCommand = new UpdateTicketStatusCommand
        {
            TicketId = ticketId,
            NewStatusId = 4, // Resolved
            StatusChangeReason = "Issue resolved",
            UpdateUser = "admin"
        };

        var toResolvedResponse = await _client.PutAsJsonAsync($"/api/tickets/{ticketId}/status", toResolvedCommand);
        Assert.True(toResolvedResponse.IsSuccessStatusCode);

        // Verify final state
        var finalResponse = await _client.GetAsync($"/api/tickets/{ticketId}");
        var finalResult = await finalResponse.Content.ReadFromJsonAsync<ApiResponse<TicketDetailDto>>();
        
        Assert.NotNull(finalResult?.Data);
        Assert.Equal(4, finalResult.Data.TicketStatusId);
        Assert.NotNull(finalResult.Data.ActualResolutionDate);
    }

    [Fact]
    public async Task TicketAssignment_ToAdminUser_Succeeds()
    {
        // Create an unassigned ticket
        var createCommand = new CreateTicketCommand
        {
            TitleAr = "تذكرة غير مخصصة",
            TitleEn = "Unassigned Ticket",
            Description = "This ticket needs to be assigned",
            CompanyId = 1,
            BranchId = 1,
            RequesterId = 1,
            TicketTypeId = 1,
            TicketPriorityId = 3,
            CreationUser = "admin"
        };

        var createResponse = await _client.PostAsJsonAsync("/api/tickets", createCommand);
        var createResult = await createResponse.Content.ReadFromJsonAsync<ApiResponse<Int64>>();
        var ticketId = createResult!.Data;

        // Verify ticket is unassigned
        var getResponse = await _client.GetAsync($"/api/tickets/{ticketId}");
        var getResult = await getResponse.Content.ReadFromJsonAsync<ApiResponse<TicketDetailDto>>();
        Assert.Null(getResult?.Data?.AssigneeId);

        // Assign ticket
        var assignCommand = new AssignTicketCommand
        {
            TicketId = ticketId,
            AssigneeId = 1,
            AssignmentReason = "Assigning to support team lead",
            UpdateUser = "admin"
        };

        var assignResponse = await _client.PutAsJsonAsync($"/api/tickets/{ticketId}/assign", assignCommand);
        Assert.True(assignResponse.IsSuccessStatusCode);

        // Verify assignment
        var verifyResponse = await _client.GetAsync($"/api/tickets/{ticketId}");
        var verifyResult = await verifyResponse.Content.ReadFromJsonAsync<ApiResponse<TicketDetailDto>>();
        
        Assert.NotNull(verifyResult?.Data);
        Assert.Equal(1, verifyResult.Data.AssigneeId);
    }

    [Fact]
    public async Task UnauthorizedAccess_ToTicketEndpoints_Returns401()
    {
        // Remove authorization header
        _client.DefaultRequestHeaders.Authorization = null;

        // Try to access tickets endpoint
        var response = await _client.GetAsync("/api/tickets");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TicketValidation_InvalidData_ReturnsValidationError()
    {
        // Try to create ticket with invalid data (empty title)
        var invalidCommand = new CreateTicketCommand
        {
            TitleAr = "", // Invalid: empty title
            TitleEn = "",
            Description = "Test",
            CompanyId = 1,
            BranchId = 1,
            RequesterId = 1,
            TicketTypeId = 1,
            TicketPriorityId = 1,
            CreationUser = "admin"
        };

        var response = await _client.PostAsJsonAsync("/api/tickets", invalidCommand);
        
        // Should return bad request due to validation
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
