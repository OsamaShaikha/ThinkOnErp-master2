using MediatR;
using ThinkOnErp.Application.DTOs.SavedSearch;

namespace ThinkOnErp.Application.Features.SavedSearches.Queries.GetSavedSearches;

/// <summary>
/// Query for retrieving saved searches for a user.
/// Requirements: 8.6, 8.11
/// </summary>
public class GetSavedSearchesQuery : IRequest<List<SavedSearchDto>>
{
    public Int64 UserId { get; set; }
}
