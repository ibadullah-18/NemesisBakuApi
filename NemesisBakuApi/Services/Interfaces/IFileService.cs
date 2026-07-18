namespace NemesisBakuApi.Services.Interfaces;

public interface IFileService
{
    Task<string> UploadImageAsync(IFormFile file, string folder);
    Task DeleteImageAsync(string imageUrl);
}
