using MediatR;
using ThinkOnErp.Application.Common;

namespace ThinkOnErp.Application.Features.Documents.Commands.DeleteDocument;

public class SoftDeleteDocumentCommand : IRequest<ApiResponse<bool>>
{
    public long[] Ids { get; set; } = Array.Empty<long>();
    public string UpdateUser { get; set; } = string.Empty;
}
