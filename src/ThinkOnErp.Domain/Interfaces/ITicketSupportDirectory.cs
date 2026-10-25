using ThinkOnErp.Domain.Entities;
namespace ThinkOnErp.Domain.Interfaces;

public interface ITicketSupportDirectory
{
    Task<List<SysUser>> GetCompanyAdminsAsync(long companyId);
}
