using System;
using System.Collections.Generic;

namespace ThinkOnErp.Application.DTOs.Inventory.OpeningBalance;

public sealed class UpdateOpeningBatchDto
{
    public DateTime? BatchDate { get; set; }
    public string? Description { get; set; }
    public List<CreateOpeningLineDto>? Lines { get; set; }
}
