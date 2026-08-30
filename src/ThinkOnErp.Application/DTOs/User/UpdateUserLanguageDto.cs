namespace ThinkOnErp.Application.DTOs.User;

public class UpdateUserLanguageDto
{
    /// <summary>
    /// Preferred Language ID (1 = Arabic, 2 = English, 3 = French, 4 = Turkish)
    /// </summary>
    public int DefaultLang { get; set; } = 1;
}
