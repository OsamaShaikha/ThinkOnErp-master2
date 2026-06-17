namespace ThinkOnErp.Application.DTOs.SysSetting;

public class CreateSysSettingDto
{
    public int SettingCode { get; set; }
    public string SettingDesc { get; set; } = string.Empty;
    public string SettingValue { get; set; } = string.Empty;
}
