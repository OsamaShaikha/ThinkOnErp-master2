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
    public List<long> BranchIds { get; set; } = new();
    public long PrimaryBranchId { get; set; }
    public string? Email { get; set; }
    public bool IsAdmin { get; set; }
    public int DefaultLang { get; set; } = 1;
    [JsonIgnore]
    public string UpdateUser { get; set; } = string.Empty;
}
