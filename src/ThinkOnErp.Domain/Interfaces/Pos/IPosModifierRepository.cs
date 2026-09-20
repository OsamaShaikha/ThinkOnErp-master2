using System;
using ThinkOnErp.Domain.Interfaces.Inventory;

namespace ThinkOnErp.Domain.Interfaces.Pos;

[Obsolete("Use IInvModifierRepository instead")]
public interface IPosModifierRepository : IInvModifierRepository
{
}
