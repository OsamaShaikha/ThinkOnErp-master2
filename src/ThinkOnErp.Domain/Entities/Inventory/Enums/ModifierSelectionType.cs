namespace ThinkOnErp.Domain.Entities.Inventory.Enums;

public enum ModifierSelectionType
{
    Single = 1,      // Radio button (choose exactly one, e.g. Size, Doneness)
    Multiple = 2     // Checkbox (choose multiple up to max, e.g. Toppings, Extras)
}
