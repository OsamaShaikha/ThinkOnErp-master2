using MediatR;
using ThinkOnErp.Application.DTOs.Company;

namespace ThinkOnErp.Application.Features.Companies.Queries.GetCompanyById;

public class GetCompanyByIdQuery : IRequest<CompanyDto?>
{
    public Int64 CompanyId { get; set; }
}
