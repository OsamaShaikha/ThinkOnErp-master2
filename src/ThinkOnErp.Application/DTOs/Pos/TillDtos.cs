using System;

namespace ThinkOnErp.Application.DTOs.Pos;

public class CreateTillDto
{
    public long BranchId { get; set; }
    public string TillCode { get; set; } = string.Empty;
    public string TillName { get; set; } = string.Empty;
    public string? MachineIdentifier { get; set; }
    public string? IpAddress { get; set; }
    public decimal DefaultFloatAmount { get; set; }
}

public class UpdateTillDto
{
    public string TillName { get; set; } = string.Empty;
    public string? MachineIdentifier { get; set; }
    public string? IpAddress { get; set; }
    public decimal DefaultFloatAmount { get; set; }
    public bool IsActive { get; set; } = true;
}

public class TillDto
{
    public long Id { get; set; }
    public long BranchId { get; set; }
    public string TillCode { get; set; } = string.Empty;
    public string TillName { get; set; } = string.Empty;
    public string? MachineIdentifier { get; set; }
    public string? IpAddress { get; set; }
    public decimal DefaultFloatAmount { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreationDate { get; set; }
}
