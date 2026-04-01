using MediatR;
using ThinkOnErp.Application.DTOs.Company;

namespace ThinkOnErp.Application.Features.Companies.Queries.GetAllCompanies;

public class GetAllCompaniesQuery : IRequest<List<CompanyDto>>
{
}
