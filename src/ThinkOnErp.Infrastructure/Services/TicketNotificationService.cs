using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Interfaces;
using MailKit.Net.Smtp;
using MimeKit;

namespace ThinkOnErp.Infrastructure.Services;

/// <summary>
/// Service for handling ticket notifications.
/// Implements email notifications for ticket lifecycle events with configurable templates.
/// </summary>
public class TicketNotificationService : ITicketNotificationService
{
    private readonly ILogger<TicketNotificationService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IUserRepository _userRepository;
    private readonly ITicketTypeRepository _ticketTypeRepository;

    // Notification configuration keys
    private const string NotificationEnabledKey = "Notifications:Enabled";
    private const string SmtpServerKey = "Notifications:Smtp:Server";
    private const string SmtpPortKey = "Notifications:Smtp:Port";
    private const string SmtpUsernameKey = "Notifications:Smtp:Username";
    private const string SmtpPasswordKey = "Notifications:Smtp:Password";
    private const string FromEmailKey = "Notifications:FromEmail";
    private const string FromNameKey = "Notifications:FromName";

    public TicketNotificationService(
        ILogger<TicketNotificationService> logger,
        IConfiguration configuration,
        IUserRepository userRepository,
        ITicketTypeRepository ticketTypeRepository)
    {
        _logger = logger;
        _configuration = configuration;
        _userRepository = userRepository;
        _ticketTypeRepository = ticketTypeRepository;
    }

    public async Task SendTicketCreatedNotificationAsync(SysRequestTicket ticket)
    {
        if (!IsNotificationEnabled())
        {
            _logger.LogDebug("Notifications are disabled, skipping ticket created notification for ticket {TicketId}", ticket.RowId);
            return;
        }

        try
        {
            _logger.LogInformation("Sending ticket created notification for ticket {TicketId}", ticket.RowId);

            var recipients = await GetTicketNotificationRecipientsAsync(ticket);
            var template = GetTicketCreatedTemplate();

            foreach (var recipient in recipients)
            {
                var emailContent = await RenderTemplateAsync(template, new
                {
                    RecipientName = recipient.RowDescE ?? recipient.RowDesc,
                    TicketId = ticket.RowId,
                    TicketTitle = ticket.TitleEn ?? ticket.TitleAr,
                    Priority = ticket.TicketPriority?.PriorityNameEn ?? "Unknown",
                    CreatedBy = ticket.Requester?.RowDescE ?? ticket.Requester?.RowDesc ?? "Unknown",
                    CreatedDate = ticket.CreationDate?.ToString("yyyy-MM-dd HH:mm"),
                    TicketUrl = GenerateTicketUrl(ticket.RowId),
                    Description = TruncateText(ticket.Description, 200)
                });

                await SendEmailAsync(recipient.Email, "New Ticket Created", emailContent);
            }

            _logger.LogInformation("Ticket created notification sent successfully for ticket {TicketId}", ticket.RowId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send ticket created notification for ticket {TicketId}", ticket.RowId);
            // Don't throw - notification failures shouldn't break the main flow
        }
    }

    public async Task SendTicketAssignedNotificationAsync(SysRequestTicket ticket, Int64? previousAssigneeId = null)
    {
        if (!IsNotificationEnabled())
        {
            _logger.LogDebug("Notifications are disabled, skipping ticket assigned notification for ticket {TicketId}", ticket.RowId);
            return;
        }

        try
        {
            _logger.LogInformation("Sending ticket assigned notification for ticket {TicketId} to assignee {AssigneeId}", 
                ticket.RowId, ticket.AssigneeId);

            if (ticket.AssigneeId == null)
            {
                _logger.LogWarning("Cannot send assignment notification - ticket {TicketId} has no assignee", ticket.RowId);
                return;
            }

            var assignee = await _userRepository.GetByIdAsync(ticket.AssigneeId.Value);
            if (assignee?.Email == null)
            {
                _logger.LogWarning("Cannot send assignment notification - assignee {AssigneeId} has no email", ticket.AssigneeId);
                return;
            }

            var template = GetTicketAssignedTemplate();
            var emailContent = await RenderTemplateAsync(template, new
            {
                AssigneeName = assignee.RowDescE ?? assignee.RowDesc,
                TicketId = ticket.RowId,
                TicketTitle = ticket.TitleEn ?? ticket.TitleAr,
                Priority = ticket.TicketPriority?.PriorityNameEn ?? "Unknown",
                AssignedBy = ticket.UpdateUser ?? ticket.CreationUser,
                AssignedDate = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm"),
                TicketUrl = GenerateTicketUrl(ticket.RowId),
                Description = TruncateText(ticket.Description, 200)
            });

            await SendEmailAsync(assignee.Email, "Ticket Assigned to You", emailContent);

            _logger.LogInformation("Ticket assigned notification sent successfully for ticket {TicketId}", ticket.RowId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send ticket assigned notification for ticket {TicketId}", ticket.RowId);
        }
    }

    public async Task SendTicketStatusChangedNotificationAsync(SysRequestTicket ticket, Int64 previousStatusId)
    {
        if (!IsNotificationEnabled())
        {
            _logger.LogDebug("Notifications are disabled, skipping status change notification for ticket {TicketId}", ticket.RowId);
            return;
        }

        try
        {
            _logger.LogInformation("Sending status change notification for ticket {TicketId} from status {PreviousStatus} to {NewStatus}", 
                ticket.RowId, previousStatusId, ticket.TicketStatusId);

            var recipients = await GetTicketNotificationRecipientsAsync(ticket);
            var template = GetTicketStatusChangedTemplate();

            foreach (var recipient in recipients)
            {
                var emailContent = await RenderTemplateAsync(template, new
                {
                    RecipientName = recipient.RowDescE ?? recipient.RowDesc,
                    TicketId = ticket.RowId,
                    TicketTitle = ticket.TitleEn ?? ticket.TitleAr,
                    NewStatus = ticket.TicketStatus?.StatusNameEn ?? "Unknown",
                    UpdatedBy = ticket.UpdateUser ?? "System",
                    UpdatedDate = ticket.UpdateDate?.ToString("yyyy-MM-dd HH:mm") ?? DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm"),
                    TicketUrl = GenerateTicketUrl(ticket.RowId)
                });

                await SendEmailAsync(recipient.Email, "Ticket Status Updated", emailContent);
            }

            _logger.LogInformation("Status change notification sent successfully for ticket {TicketId}", ticket.RowId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send status change notification for ticket {TicketId}", ticket.RowId);
        }
    }

    public async Task SendCommentAddedNotificationAsync(SysRequestTicket ticket, SysTicketComment comment)
    {
        if (!IsNotificationEnabled())
        {
            _logger.LogDebug("Notifications are disabled, skipping comment notification for ticket {TicketId}", ticket.RowId);
            return;
        }

        try
        {
            _logger.LogInformation("Sending comment added notification for ticket {TicketId}", ticket.RowId);

            var recipients = await GetTicketNotificationRecipientsAsync(ticket);
            var template = GetCommentAddedTemplate();

            foreach (var recipient in recipients)
            {
                // Don't notify the comment author
                if (recipient.UserName == comment.CreationUser)
                    continue;

                var emailContent = await RenderTemplateAsync(template, new
                {
                    RecipientName = recipient.RowDescE ?? recipient.RowDesc,
                    TicketId = ticket.RowId,
                    TicketTitle = ticket.TitleEn ?? ticket.TitleAr,
                    CommentBy = comment.CreationUser,
                    CommentDate = comment.CreationDate?.ToString("yyyy-MM-dd HH:mm"),
                    CommentText = TruncateText(comment.CommentText, 300),
                    TicketUrl = GenerateTicketUrl(ticket.RowId)
                });

                await SendEmailAsync(recipient.Email, "New Comment on Ticket", emailContent);
            }

            _logger.LogInformation("Comment added notification sent successfully for ticket {TicketId}", ticket.RowId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send comment added notification for ticket {TicketId}", ticket.RowId);
        }
    }

    public async Task SendSlaEscalationAlertAsync(SysRequestTicket ticket)
    {
        if (!IsNotificationEnabled())
        {
            _logger.LogDebug("Notifications are disabled, skipping SLA escalation alert for ticket {TicketId}", ticket.RowId);
            return;
        }

        try
        {
            _logger.LogInformation("Sending SLA escalation alert for ticket {TicketId}", ticket.RowId);

            // Get admin users for escalation alerts
            var adminUsers = await GetAdminUsersAsync();
            var template = GetSlaEscalationTemplate();

            foreach (var admin in adminUsers)
            {
                if (string.IsNullOrEmpty(admin.Email))
                    continue;

                var emailContent = await RenderTemplateAsync(template, new
                {
                    AdminName = admin.RowDescE ?? admin.RowDesc,
                    TicketId = ticket.RowId,
                    TicketTitle = ticket.TitleEn ?? ticket.TitleAr,
                    Priority = ticket.TicketPriority?.PriorityNameEn ?? "Unknown",
                    ExpectedResolution = ticket.ExpectedResolutionDate?.ToString("yyyy-MM-dd HH:mm"),
                    CreatedDate = ticket.CreationDate?.ToString("yyyy-MM-dd HH:mm"),
                    AssigneeName = ticket.Assignee?.RowDescE ?? ticket.Assignee?.RowDesc ?? "Unassigned",
                    TicketUrl = GenerateTicketUrl(ticket.RowId)
                });

                await SendEmailAsync(admin.Email, "SLA Escalation Alert", emailContent);
            }

            _logger.LogInformation("SLA escalation alert sent successfully for ticket {TicketId}", ticket.RowId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send SLA escalation alert for ticket {TicketId}", ticket.RowId);
        }
    }

    public async Task SendAttachmentAddedNotificationAsync(SysRequestTicket ticket, SysTicketAttachment attachment)
    {
        if (!IsNotificationEnabled())
        {
            _logger.LogDebug("Notifications are disabled, skipping attachment notification for ticket {TicketId}", ticket.RowId);
            return;
        }

        try
        {
            _logger.LogInformation("Sending attachment added notification for ticket {TicketId}", ticket.RowId);

            var recipients = await GetTicketNotificationRecipientsAsync(ticket);
            var template = GetAttachmentAddedTemplate();

            foreach (var recipient in recipients)
            {
                // Don't notify the person who uploaded the attachment
                if (recipient.UserName == attachment.CreationUser)
                    continue;

                var emailContent = await RenderTemplateAsync(template, new
                {
                    RecipientName = recipient.RowDescE ?? recipient.RowDesc,
                    TicketId = ticket.RowId,
                    TicketTitle = ticket.TitleEn ?? ticket.TitleAr,
                    FileName = attachment.FileName,
                    FileSize = FormatFileSize(attachment.FileSize),
                    UploadedBy = attachment.CreationUser,
                    UploadedDate = attachment.CreationDate?.ToString("yyyy-MM-dd HH:mm"),
                    TicketUrl = GenerateTicketUrl(ticket.RowId)
                });

                await SendEmailAsync(recipient.Email, "New Attachment Added", emailContent);
            }

            _logger.LogInformation("Attachment added notification sent successfully for ticket {TicketId}", ticket.RowId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send attachment added notification for ticket {TicketId}", ticket.RowId);
        }
    }

    #region Private Helper Methods

    private bool IsNotificationEnabled()
    {
        return _configuration.GetValue<bool>(NotificationEnabledKey, true);
    }

    private async Task<List<SysUser>> GetTicketNotificationRecipientsAsync(SysRequestTicket ticket)
    {
        var recipients = new List<SysUser>();

        // Add requester
        if (ticket.Requester?.Email != null)
        {
            recipients.Add(ticket.Requester);
        }

        // Add assignee if different from requester
        if (ticket.Assignee?.Email != null && ticket.AssigneeId != ticket.RequesterId)
        {
            recipients.Add(ticket.Assignee);
        }

        return recipients.Where(r => !string.IsNullOrEmpty(r.Email)).ToList();
    }

    private async Task<List<SysUser>> GetAdminUsersAsync()
    {
        try
        {
            // Get all active admin users
            var adminUsers = await _userRepository.GetAdminUsersAsync();
            return adminUsers.Where(u => !string.IsNullOrEmpty(u.Email)).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting admin users for notifications");
            return new List<SysUser>();
        }
    }

    private async Task SendEmailAsync(string toEmail, string subject, string body)
    {
        try
        {
            var smtpServer = _configuration.GetValue<string>(SmtpServerKey);
            var smtpPort = _configuration.GetValue<int>(SmtpPortKey, 587);
            var smtpUsername = _configuration.GetValue<string>(SmtpUsernameKey);
            var smtpPassword = _configuration.GetValue<string>(SmtpPasswordKey);
            var fromEmail = _configuration.GetValue<string>(FromEmailKey);
            var fromName = _configuration.GetValue<string>(FromNameKey, "ThinkOnERP Support");

            // If SMTP is not configured, just log the email
            if (string.IsNullOrEmpty(smtpServer) || string.IsNullOrEmpty(fromEmail))
            {
                _logger.LogInformation("SMTP not configured. Email would be sent to {Email} with subject '{Subject}'", toEmail, subject);
                _logger.LogDebug("Email body: {Body}", body);
                return;
            }

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(fromName, fromEmail));
            message.To.Add(new MailboxAddress("", toEmail));
            message.Subject = subject;

            var bodyBuilder = new BodyBuilder
            {
                TextBody = body
            };
            message.Body = bodyBuilder.ToMessageBody();

            using var client = new SmtpClient();
            
            // Configure SSL/TLS
            await client.ConnectAsync(smtpServer, smtpPort, MailKit.Security.SecureSocketOptions.StartTls);
            
            // Authenticate if credentials are provided
            if (!string.IsNullOrEmpty(smtpUsername) && !string.IsNullOrEmpty(smtpPassword))
            {
                await client.AuthenticateAsync(smtpUsername, smtpPassword);
            }
            
            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            _logger.LogInformation("Email sent successfully to {Email} with subject '{Subject}'", toEmail, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Email} with subject '{Subject}'", toEmail, subject);
            // Don't throw - notification failures shouldn't break the main flow
        }
    }

    private async Task<string> RenderTemplateAsync(string template, object model)
    {
        // Simple template rendering - in production, consider using a proper template engine
        var result = template;
        var properties = model.GetType().GetProperties();

        foreach (var prop in properties)
        {
            var value = prop.GetValue(model)?.ToString() ?? "";
            result = result.Replace($"{{{{{prop.Name}}}}}", value);
        }

        return await Task.FromResult(result);
    }

    private string GenerateTicketUrl(Int64 ticketId)
    {
        var baseUrl = _configuration.GetValue<string>("Application:BaseUrl", "https://localhost");
        return $"{baseUrl}/tickets/{ticketId}";
    }

    private string TruncateText(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text;

        return text.Substring(0, maxLength) + "...";
    }

    private string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }

    #endregion

    #region Email Templates

    private string GetTicketCreatedTemplate()
    {
        return @"
Dear {{RecipientName}},

A new ticket has been created:

Ticket ID: {{TicketId}}
Title: {{TicketTitle}}
Priority: {{Priority}}
Created by: {{CreatedBy}}
Created on: {{CreatedDate}}

Description:
{{Description}}

You can view the ticket details at: {{TicketUrl}}

Best regards,
ThinkOnERP Support Team
";
    }

    private string GetTicketAssignedTemplate()
    {
        return @"
Dear {{AssigneeName}},

A ticket has been assigned to you:

Ticket ID: {{TicketId}}
Title: {{TicketTitle}}
Priority: {{Priority}}
Assigned by: {{AssignedBy}}
Assigned on: {{AssignedDate}}

Description:
{{Description}}

Please review and take appropriate action: {{TicketUrl}}

Best regards,
ThinkOnERP Support Team
";
    }

    private string GetTicketStatusChangedTemplate()
    {
        return @"
Dear {{RecipientName}},

The status of ticket {{TicketId}} has been updated:

Ticket: {{TicketTitle}}
New Status: {{NewStatus}}
Updated by: {{UpdatedBy}}
Updated on: {{UpdatedDate}}

View ticket details: {{TicketUrl}}

Best regards,
ThinkOnERP Support Team
";
    }

    private string GetCommentAddedTemplate()
    {
        return @"
Dear {{RecipientName}},

A new comment has been added to ticket {{TicketId}}:

Ticket: {{TicketTitle}}
Comment by: {{CommentBy}}
Comment date: {{CommentDate}}

Comment:
{{CommentText}}

View full conversation: {{TicketUrl}}

Best regards,
ThinkOnERP Support Team
";
    }

    private string GetSlaEscalationTemplate()
    {
        return @"
Dear {{AdminName}},

SLA ESCALATION ALERT

Ticket {{TicketId}} is approaching or has exceeded its SLA target:

Title: {{TicketTitle}}
Priority: {{Priority}}
Expected Resolution: {{ExpectedResolution}}
Created: {{CreatedDate}}
Assigned to: {{AssigneeName}}

Immediate attention required: {{TicketUrl}}

Best regards,
ThinkOnERP Support System
";
    }

    private string GetAttachmentAddedTemplate()
    {
        return @"
Dear {{RecipientName}},

A new file attachment has been added to ticket {{TicketId}}:

Ticket: {{TicketTitle}}
File: {{FileName}} ({{FileSize}})
Uploaded by: {{UploadedBy}}
Uploaded on: {{UploadedDate}}

View ticket and download attachment: {{TicketUrl}}

Best regards,
ThinkOnERP Support Team
";
    }

    #endregion
}