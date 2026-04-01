using MediatR;

namespace ThinkOnErp.Application.Features.Companies.Commands.DeleteCompany;

public class DeleteCompanyCommand : IRequest<int>
{
    public decimal RowId { get; set; }
}
