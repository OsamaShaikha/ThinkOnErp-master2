namespace ThinkOnErp.Application.DTOs.SysCode;

public class SysCodeDto
{
    public int CodeMgr { get; set; }
    public int CodeMnr { get; set; }
    public int CodeLang { get; set; }
    public string CodeDesc { get; set; } = string.Empty;
    public string CodeValue { get; set; } = string.Empty;
    public int IsActive { get; set; } = 1;
    public string CreationUser { get; set; } = string.Empty;
    public DateTime? CreationDate { get; set; }
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }
}
