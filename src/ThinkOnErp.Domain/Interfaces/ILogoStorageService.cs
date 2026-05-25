namespace ThinkOnErp.Domain.Interfaces;

public interface ILogoStorageService
{
    Task<string> SaveLogoAsync(byte[] logoBytes, string entityType, long entityId);
    Task<byte[]?> GetLogoAsync(string logoPath);
    Task DeleteLogoAsync(string logoPath);
}
