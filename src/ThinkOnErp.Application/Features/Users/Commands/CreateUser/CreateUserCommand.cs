using MediatR;

namespace ThinkOnErp.Application.Features.Users.Commands.CreateUser;

public class CreateUserCommand : IRequest<decimal>
{
    public string RowDesc { get; set; } = string.Empty;
    public string RowDescE { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Phone2 { get; set; }
    public decimal? Role { get; set; }
    public decimal? BranchId { get; set; }
    public string? Email { get; set; }
    public bool IsAdmin { get; set; }
    public string CreationUser { get; set; } = string.Empty;
}
