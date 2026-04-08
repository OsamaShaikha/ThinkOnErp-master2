using MediatR;
using ThinkOnErp.Application.DTOs.User;

namespace ThinkOnErp.Application.Features.Users.Queries.GetUsersByCompanyId;

public class GetUsersByCompanyIdQuery : IRequest<List<UserDto>>
{
    public Int64 CompanyId { get; set; }
}
