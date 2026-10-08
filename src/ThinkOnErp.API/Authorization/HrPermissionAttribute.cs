namespace ThinkOnErp.API.Authorization;

[AttributeUsage(AttributeTargets.Method)]
public sealed class HrPermissionAttribute(string screen, string feature, bool selfService = false) : Attribute
{
    public string Screen { get; } = screen;
    public string Feature { get; } = feature;
    public bool SelfService { get; } = selfService;
}
