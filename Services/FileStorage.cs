namespace Staybnb.Services;

public interface IFileStorage
{
    string? ValidateImage(IFormFile file);
    string? ValidateDocument(IFormFile file);
    Task<string> SavePropertyImageAsync(IFormFile file);
    void DeletePropertyImage(string imageUrl);
    Task<string> SaveGuestDocumentAsync(IFormFile file);
    string GetGuestDocumentPath(string fileName);
    string GetContentType(string fileName);
    void DeleteGuestDocument(string fileName);
}

public class FileStorage(IWebHostEnvironment env) : IFileStorage
{
    private const long MaxBytes = 5 * 1024 * 1024;
    private const string PropertyImageFolder = "uploads/properties";

    private static readonly string[] ImageExtensions = [".jpg", ".jpeg", ".png", ".webp"];
    private static readonly string[] DocumentExtensions = [".jpg", ".jpeg", ".png", ".pdf"];

    // Guest ID documents are private, so they live outside wwwroot and are only served through an authorized action.
    private string DocumentFolder => Path.Combine(env.ContentRootPath, "App_Data", "documents");

    public string? ValidateImage(IFormFile file) => Validate(file, ImageExtensions);

    public string? ValidateDocument(IFormFile file) => Validate(file, DocumentExtensions);

    public async Task<string> SavePropertyImageAsync(IFormFile file)
    {
        var folder = Path.Combine(env.WebRootPath, PropertyImageFolder);
        var fileName = await SaveAsync(file, folder);
        return $"/{PropertyImageFolder}/{fileName}";
    }

    public void DeletePropertyImage(string imageUrl)
    {
        // Seeded demo images are shared, so only uploaded files are deleted
        if (!imageUrl.StartsWith($"/{PropertyImageFolder}/", StringComparison.OrdinalIgnoreCase)) return;

        var path = Path.Combine(env.WebRootPath, PropertyImageFolder, Path.GetFileName(imageUrl));
        if (File.Exists(path)) File.Delete(path);
    }

    public Task<string> SaveGuestDocumentAsync(IFormFile file) => SaveAsync(file, DocumentFolder);

    public string GetGuestDocumentPath(string fileName) => Path.Combine(DocumentFolder, Path.GetFileName(fileName));

    public string GetContentType(string fileName) => Path.GetExtension(fileName).ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf",
        ".png" => "image/png",
        ".webp" => "image/webp",
        _ => "image/jpeg"
    };

    public void DeleteGuestDocument(string fileName)
    {
        var path = GetGuestDocumentPath(fileName);
        if (File.Exists(path)) File.Delete(path);
    }

    private static string? Validate(IFormFile file, string[] allowed)
    {
        if (file.Length == 0) return $"'{file.FileName}' is empty.";
        if (file.Length > MaxBytes) return $"'{file.FileName}' is larger than 5 MB.";

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowed.Contains(ext))
            return $"'{file.FileName}' is not an allowed file type ({string.Join(", ", allowed)}).";

        return null;
    }

    private static async Task<string> SaveAsync(IFormFile file, string folder)
    {
        Directory.CreateDirectory(folder);
        var fileName = $"{Guid.NewGuid():N}{Path.GetExtension(file.FileName).ToLowerInvariant()}";
        await using var stream = File.Create(Path.Combine(folder, fileName));
        await file.CopyToAsync(stream);
        return fileName;
    }
}
