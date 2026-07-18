using System.Text;
using System.Text.RegularExpressions;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Options;
using NemesisBakuApi.Services.Interfaces;
using NemesisBakuApi.Settings;

namespace NemesisBakuApi.Services.Implementations;

public class CloudinaryFileService : IFileService
{
    private const long MaxStandardImageBytes = 10 * 1024 * 1024;
    private const long MaxHeicImageBytes = 25 * 1024 * 1024;

    private static readonly HashSet<string> StandardExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".webp"
        };

    private static readonly HashSet<string> HeicExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".heic", ".heif"
        };

    private readonly Cloudinary _cloudinary;

    public CloudinaryFileService(IOptions<CloudinarySettings> options)
    {
        var settings = options.Value;

        if (string.IsNullOrWhiteSpace(settings.CloudName) ||
            string.IsNullOrWhiteSpace(settings.ApiKey) ||
            string.IsNullOrWhiteSpace(settings.ApiSecret))
        {
            throw new InvalidOperationException("Cloudinary ayarları tam deyil");
        }

        _cloudinary = new Cloudinary(new Account(
            settings.CloudName,
            settings.ApiKey,
            settings.ApiSecret));
    }

    public async Task<string> UploadImageAsync(IFormFile file, string folder)
    {
        if (file == null || file.Length == 0)
            throw new InvalidOperationException("Fayl boşdur");

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var isHeic = HeicExtensions.Contains(extension);

        if (!StandardExtensions.Contains(extension) && !isHeic)
        {
            throw new InvalidOperationException(
                "Yalnız JPG, JPEG, PNG, WEBP, HEIC və HEIF formatları qəbul olunur");
        }

        var maxBytes = isHeic ? MaxHeicImageBytes : MaxStandardImageBytes;
        if (file.Length > maxBytes)
        {
            var maxMb = maxBytes / 1024 / 1024;
            throw new InvalidOperationException($"Şəkil maksimum {maxMb} MB ola bilər");
        }

        if (!await HasValidImageSignatureAsync(file, extension))
            throw new InvalidOperationException("Faylın şəkil formatı etibarlı deyil");

        var safeFolder = SanitizeFolder(folder);
        await using var stream = file.OpenReadStream();

        var uploadParams = new ImageUploadParams
        {
            File = new FileDescription(file.FileName, stream),
            Folder = $"nemesisbaku/{safeFolder}",
            UseFilename = false,
            UniqueFilename = true,
            Overwrite = false
        };

        // Mac/iPhone HEIC fayllarını brauzerlərin hamısında açılan JPEG kimi saxla.
        if (isHeic)
            uploadParams.Format = "jpg";

        var result = await _cloudinary.UploadAsync(uploadParams);

        if (result.Error != null)
            throw new InvalidOperationException(result.Error.Message);

        if (result.SecureUrl == null)
            throw new InvalidOperationException("Cloudinary şəkil URL-i qaytarmadı");

        return result.SecureUrl.ToString();
    }

    public async Task DeleteImageAsync(string imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl)) return;

        var publicId = ExtractPublicIdFromUrl(imageUrl);
        if (string.IsNullOrWhiteSpace(publicId)) return;

        var result = await _cloudinary.DestroyAsync(new DeletionParams(publicId)
        {
            Invalidate = true
        });

        if (result.Error != null)
            throw new InvalidOperationException(result.Error.Message);
    }

    private static string SanitizeFolder(string folder)
    {
        var safeFolder = Regex.Replace(folder ?? string.Empty, @"[^a-zA-Z0-9/_-]", "");
        safeFolder = safeFolder.Trim('/');
        return string.IsNullOrWhiteSpace(safeFolder) ? "images" : safeFolder;
    }

    private static async Task<bool> HasValidImageSignatureAsync(
        IFormFile file,
        string extension)
    {
        var header = new byte[16];
        await using var stream = file.OpenReadStream();
        var read = await stream.ReadAsync(header.AsMemory(0, header.Length));

        if (read < 12) return false;

        if (extension is ".jpg" or ".jpeg")
            return header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;

        if (extension == ".png")
        {
            byte[] signature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
            return header.Take(signature.Length).SequenceEqual(signature);
        }

        if (extension == ".webp")
        {
            return Encoding.ASCII.GetString(header, 0, 4) == "RIFF" &&
                   Encoding.ASCII.GetString(header, 8, 4) == "WEBP";
        }

        if (extension is ".heic" or ".heif")
        {
            if (Encoding.ASCII.GetString(header, 4, 4) != "ftyp") return false;

            var brand = Encoding.ASCII.GetString(header, 8, 4).ToLowerInvariant();
            return brand.StartsWith("hei") ||
                   brand.StartsWith("hev") ||
                   brand is "mif1" or "msf1";
        }

        return false;
    }

    private static string? ExtractPublicIdFromUrl(string imageUrl)
    {
        try
        {
            var uri = new Uri(imageUrl);
            var segments = uri.AbsolutePath
                .Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Select(Uri.UnescapeDataString)
                .ToList();

            var uploadIndex = segments.FindIndex(
                segment => segment.Equals("upload", StringComparison.OrdinalIgnoreCase));

            if (uploadIndex < 0 || uploadIndex + 1 >= segments.Count) return null;

            var assetSegments = segments.Skip(uploadIndex + 1).ToList();
            var versionIndex = assetSegments.FindIndex(
                segment => Regex.IsMatch(segment, @"^v\d+$"));

            if (versionIndex >= 0)
                assetSegments = assetSegments.Skip(versionIndex + 1).ToList();

            if (assetSegments.Count == 0) return null;

            assetSegments[^1] = Path.GetFileNameWithoutExtension(assetSegments[^1]);
            return string.Join('/', assetSegments);
        }
        catch (UriFormatException)
        {
            return null;
        }
    }
}
