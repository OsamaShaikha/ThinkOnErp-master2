using MediatR;

namespace ThinkOnErp.Application.Features.Companies.Commands.SetCompanyStatus;

public class SetCompanyStatusCommand : IRequest<bool>
{
    public long CompanyId { get; set; }
    public bool IsActive { get; set; }
    public string UpdateUser { get; set; } = string.Empty;
}
