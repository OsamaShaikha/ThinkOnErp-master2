namespace ThinkOnErp.Application.Features.Auth.Commands.Login;

public class CompanyLoginCommand
{
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string CompanyCode { get; set; } = string.Empty;

    /// <summary>
    /// Optional session language ID (1 = Arabic, 2 = English, 3 = French, 4 = Spanish, 5 = Turkish, 6 = German).
    /// If omitted, falls back to the user's default language.
    /// </summary>
    public int? Language { get; set; }
}
