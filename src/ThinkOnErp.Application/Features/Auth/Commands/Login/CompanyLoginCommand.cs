using ThinkOnErp.Application.DTOs.Auth;

namespace ThinkOnErp.Application.Features.Auth.Commands.Login;

public class CompanyLoginCommand
{
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string CompanyCode { get; set; } = string.Empty;
}
