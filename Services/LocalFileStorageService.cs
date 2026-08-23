using DailyGourmet.Api.Options;
using Microsoft.Extensions.Options;

namespace DailyGourmet.Api.Services;

/// <summary>MVP file storage — local disk, no external infra needed. Trade-off: won't survive
/// horizontal scaling or an ephemeral host filesystem; swap for an AzureBlobFileStorageService
/// behind the same IFileStorageService if that becomes a requirement, no caller changes needed.</summary>
public class LocalFileStorageService(IOptions<FileStorageOptions> options, IWebHostEnvironment env) : IFileStorageService
{
    private string RootPath => Path.IsPathRooted(options.Value.RootPath)
        ? options.Value.RootPath
        : Path.Combine(env.ContentRootPath, options.Value.RootPath);

    public async Task<string> SaveAsync(string @namespace, string fileName, Stream content, CancellationToken ct = default)
    {
        var safeNamespace = string.Join("_", @namespace.Split(Path.GetInvalidFileNameChars()));
        var directory = Path.Combine(RootPath, safeNamespace);
        Directory.CreateDirectory(directory);

        var storageKey = $"{safeNamespace}/{Guid.NewGuid():N}-{Path.GetFileName(fileName)}";
        var fullPath = Path.Combine(RootPath, storageKey.Replace('/', Path.DirectorySeparatorChar));

        await using var fileStream = File.Create(fullPath);
        await content.CopyToAsync(fileStream, ct);
        return storageKey;
    }

    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct = default)
    {
        var fullPath = Path.Combine(RootPath, storageKey.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(fullPath)) throw new FileNotFoundException("Datei nicht gefunden.", storageKey);
        return Task.FromResult<Stream>(File.OpenRead(fullPath));
    }
}
