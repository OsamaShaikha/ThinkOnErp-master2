using MediatR;

namespace ThinkOnErp.Application.Features.Companies.Queries.GetCompanyLogo;

public class GetCompanyLogoQuery : IRequest<byte[]?>
{
    public Int64 CompanyId { get; set; }
}