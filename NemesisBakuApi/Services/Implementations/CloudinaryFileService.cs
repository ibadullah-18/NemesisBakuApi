using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Options;
using NemesisBakuApi.Services.Interfaces;
using NemesisBakuApi.Settings;

namespace NemesisBakuApi.Services.Implementations;

public class CloudinaryFileService : IFileService
{
    private readonly Cloudinary _cloudinary;

    public CloudinaryFileService(IOptions<CloudinarySettings> options)
    {
        var settings = options.Value;

        var account = new Account(
            settings.CloudName,
            settings.ApiKey,
            settings.ApiSecret);

        _cloudinary = new Cloudinary(account);
    }

    public async Task<string> UploadImageAsync(IFormFile file, string folder)
    {
        if (file == null || file.Length == 0)
            throw new Exception("Fayl boşdur");

        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
        var extension = Path.GetExtension(file.FileName).ToLower();

        if (!allowedExtensions.Contains(extension))
            throw new Exception("Yalnız jpg, jpeg, png, webp formatları qəbul olunur");

        if (file.Length > 10 * 1024 * 1024)
            throw new Exception("Şəkil maksimum 10MB ola bilər");

        await using var stream = file.OpenReadStream();

        var uploadParams = new ImageUploadParams
        {
            File = new FileDescription(file.FileName, stream),
            Folder = $"nemesisbaku/{folder}",
            UseFilename = false,
            UniqueFilename = true,
            Overwrite = false
        };

        var result = await _cloudinary.UploadAsync(uploadParams);

        if (result.Error != null)
            throw new Exception(result.Error.Message);

        return result.SecureUrl.ToString();
    }

    public async Task DeleteImageAsync(string imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
            return;

        var publicId = ExtractPublicIdFromUrl(imageUrl);

        if (string.IsNullOrWhiteSpace(publicId))
            return;

        var deleteParams = new DeletionParams(publicId);
        await _cloudinary.DestroyAsync(deleteParams);
    }

    private string? ExtractPublicIdFromUrl(string imageUrl)
    {
        try
        {
            var uri = new Uri(imageUrl);
            var parts = uri.AbsolutePath.Split("/upload/");

            if (parts.Length < 2)
                return null;

            var path = parts[1];

            var versionIndex = path.IndexOf("v", StringComparison.Ordinal);

            var segments = path.Split('/').ToList();

            if (segments.Count > 0 && segments[0].StartsWith("v"))
                segments.RemoveAt(0);

            var withoutExtension = string.Join("/", segments);
            withoutExtension = Path.ChangeExtension(withoutExtension, null);

            return withoutExtension;
        }
        catch
        {
            return null;
        }
    }
}