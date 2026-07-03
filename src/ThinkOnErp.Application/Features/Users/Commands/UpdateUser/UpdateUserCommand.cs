using System.Text.Json.Serialization;
using MediatR;

namespace ThinkOnErp.Application.Features.Users.Commands.UpdateUser;

public class UpdateUserCommand : IRequest<Int64>
{
    [JsonIgnore]
    public Int64 UserId { get; set; }
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Phone2 { get; set; }
    public Int64? RoleId { get; set; }
    public Int64? BranchId { get; set; }
    public string? Email { get; set; }
    public bool IsAdmin { get; set; }
    [JsonIgnore]
    public string UpdateUser { get; set; } = string.Empty;
}
