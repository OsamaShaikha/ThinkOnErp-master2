using MediatR;

namespace ThinkOnErp.Application.Features.Companies.Commands.DeleteCompany;

public class DeleteCompanyCommand : IRequest<Int64>
{
    public Int64 CompanyId { get; set; }
}
