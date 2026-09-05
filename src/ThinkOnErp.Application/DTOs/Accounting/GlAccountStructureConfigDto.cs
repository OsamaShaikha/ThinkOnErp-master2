namespace ThinkOnErp.Application.DTOs.Accounting;

public class GlAccountStructureConfigDto
{
    public int LevelNumber { get; set; }
    public int DigitLength { get; set; }
    public int TotalCumulativeLength { get; set; }
    public string LevelNameLocal { get; set; } = string.Empty;
    public string LevelNameEn { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}

public class UpdateGlAccountStructureConfigDto
{
    public int LevelNumber { get; set; }
    public int DigitLength { get; set; }
    public string? LevelNameLocal { get; set; }
    public string? LevelNameEn { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}
