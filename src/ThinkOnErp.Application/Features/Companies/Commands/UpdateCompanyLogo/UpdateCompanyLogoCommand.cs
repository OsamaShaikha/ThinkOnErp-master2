using MediatR;

namespace ThinkOnErp.Application.Features.Companies.Commands.UpdateCompanyLogo;

public class UpdateCompanyLogoCommand : IRequest<Int64>
{
    public Int64 CompanyId { get; set; }
    public byte[] Logo { get; set; } = Array.Empty<byte>();
    public string UpdateUser { get; set; } = string.Empty;
}