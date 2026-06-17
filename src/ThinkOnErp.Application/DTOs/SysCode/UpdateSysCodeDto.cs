namespace ThinkOnErp.Application.DTOs.SysCode;

public class UpdateSysCodeDto
{
    public string CodeDesc { get; set; } = string.Empty;
    public string CodeValue { get; set; } = string.Empty;
    public int IsActive { get; set; } = 1;
}
