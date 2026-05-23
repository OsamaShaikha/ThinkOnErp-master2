namespace ThinkOnErp.Domain.Interfaces;

public interface IDocumentStorageService
{
    Task<string> SaveFileAsync(Stream fileStream, string relativePath);
    Task<Stream> GetFileAsync(string relativePath);
    Task DeleteFileAsync(string relativePath);
    string GetFullPath(string relativePath);
    bool ValidatePath(string relativePath);
}
