using MediatR;
using ThinkOnErp.Application.DTOs.FiscalYear;
using ThinkOnErp.Domain.Interfaces;

namespace ThinkOnErp.Application.Features.FiscalYears.Queries.GetFiscalYearsByBranch;

public class GetFiscalYearsByBranchQueryHandler : IRequestHandler<GetFiscalYearsByBranchQuery, List<FiscalYearDto>>
{
    private readonly IFiscalYearRepository _fiscalYearRepository;

    public GetFiscalYearsByBranchQueryHandler(IFiscalYearRepository fiscalYearRepository)
    {
        _fiscalYearRepository = fiscalYearRepository;
    }

    public async Task<List<FiscalYearDto>> Handle(GetFiscalYearsByBranchQuery request, CancellationToken cancellationToken)
    {
        var fiscalYears = await _fiscalYearRepository.GetByBranchIdAsync(request.BranchId);

        return fiscalYears.Select(fy => new FiscalYearDto
        {
            FiscalYearId = fy.Id,
            BranchId = fy.BranchId,
            FiscalYearCode = fy.FiscalYearCode,
            FiscalYearNameAr = fy.FiscalYearNameAr,
            FiscalYearNameEn = fy.FiscalYearNameEn,
            StartDate = fy.StartDate,
            EndDate = fy.EndDate,
            IsClosed = fy.IsClosed,
            IsActive = fy.IsActive,
            CreationUser = fy.CreationUser,
            CreationDate = fy.CreationDate,
            UpdateUser = fy.UpdateUser,
            UpdateDate = fy.UpdateDate
        }).ToList();
    }
}
