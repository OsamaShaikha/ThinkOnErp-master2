namespace ThinkOnErp.Domain.Entities;

public class SysSetting
{
    public int SettingCode { get; set; }
    public string SettingDesc { get; set; } = string.Empty;
    public string SettingValue { get; set; } = string.Empty;
}
