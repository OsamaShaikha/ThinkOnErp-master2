using MediatR;

namespace ThinkOnErp.Application.Features.Permissions.Commands.GrantSystemToCompany;

public class GrantSystemToCompanyCommand : IRequest<Unit>
{
    public Int64 CompanyId { get; set; }
    public Int64 SystemId { get; set; }
    public Int64? GrantedBy { get; set; }
    public string? Notes { get; set; }
    public string CreationUser { get; set; } = string.Empty;
}
