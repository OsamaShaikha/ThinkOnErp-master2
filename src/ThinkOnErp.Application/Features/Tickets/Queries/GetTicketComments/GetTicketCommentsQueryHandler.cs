using MediatR;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Application.DTOs.Ticket;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.Tickets.Queries.GetTicketComments;

/// <summary>
/// Handler for GetTicketCommentsQuery.
/// Retrieves comments for a specific ticket with visibility rules.
/// </summary>
public class GetTicketCommentsQueryHandler : IRequestHandler<GetTicketCommentsQuery, List<TicketCommentDto>>
{
    private readonly ITicketRepository _ticketRepository;
    private readonly ITicketCommentRepository _commentRepository;
    private readonly ILogger<GetTicketCommentsQueryHandler> _logger;

    public GetTicketCommentsQueryHandler(
        ITicketRepository ticketRepository,
        ITicketCommentRepository commentRepository,
        ILogger<GetTicketCommentsQueryHandler> logger)
    {
        _ticketRepository = ticketRepository;
        _commentRepository = commentRepository;
        _logger = logger;
    }

    public async Task<List<TicketCommentDto>> Handle(GetTicketCommentsQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Retrieving comments for ticket {TicketId}", request.TicketId);

        try
        {
            // Verify the ticket exists
            var ticket = await _ticketRepository.GetByIdAsync(request.TicketId);
            if (ticket == null)
            {
                throw new ArgumentException($"Ticket with ID {request.TicketId} not found.");
            }

            // Get comments for the ticket (filtering is handled by repository)
            var comments = await _commentRepository.GetByTicketIdAsync(request.TicketId, request.IncludeInternalComments);

            // Map to DTOs and sort
            var commentDtos = comments.Select(c => new TicketCommentDto
            {
                CommentId = c.RowId,
                TicketId = c.TicketId,
                CommentText = c.CommentText,
                IsInternal = c.IsInternal,
                CreationUser = c.CreationUser,
                CreationUserName = c.CreationUser, // Using CreationUser from entity
                CreationDate = c.CreationDate
            }).ToList();

            // Sort comments
            if (request.SortDirection.ToUpperInvariant() == "DESC")
            {
                commentDtos = commentDtos.OrderByDescending(c => c.CreationDate).ToList();
            }
            else
            {
                commentDtos = commentDtos.OrderBy(c => c.CreationDate).ToList();
            }

            _logger.LogInformation("Retrieved {Count} comments for ticket {TicketId}", 
                commentDtos.Count, request.TicketId);

            return commentDtos;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving comments for ticket {TicketId}", request.TicketId);
            throw;
        }
    }
}