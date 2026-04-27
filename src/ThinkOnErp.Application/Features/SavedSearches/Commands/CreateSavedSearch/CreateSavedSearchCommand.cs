using MediatR;

namespace ThinkOnErp.Application.Features.SavedSearches.Commands.CreateSavedSearch;

/// <summary>
/// Command for creating a new saved search.
/// Requirements: 8.6, 8.11
/// </summary>
public class CreateSavedSearchCommand : IRequest<Int64>
{
    public Int64 UserId { get; set; }
    public string SearchName { get; set; } = string.Empty;
    public string? SearchDescription { get; set; }
    public string SearchCriteria { get; set; } = string.Empty;
    public bool IsPublic { get; set; }
    public bool IsDefault { get; set; }
    public string CreationUser { get; set; } = string.Empty;
}
