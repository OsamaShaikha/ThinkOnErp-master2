using ThinkOnErp.Application.DTOs.Feature;
using ThinkOnErp.Application.DTOs.Module;
using ThinkOnErp.Application.DTOs.Screen;

namespace ThinkOnErp.Application.DTOs.BranchProvisioning;

public class BranchProvisioningStateDto
{
    public List<ModuleDto> Systems { get; set; } = new();
    public List<ScreenDto> Screens { get; set; } = new();
    public List<long> RevokedScreenIds { get; set; } = new();
    public List<RevokedFeatureDto> RevokedFeatures { get; set; } = new();
}
