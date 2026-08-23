namespace DailyGourmet.Api.Services;

public interface IFileStorageService
{
    /// <summary>Saves a stream under a key namespaced by the caller (e.g. a ticket id) and returns
    /// an opaque storage key to persist on the owning entity — never a public URL, since these
    /// files aren't meant to be directly link-accessible.</summary>
    Task<string> SaveAsync(string @namespace, string fileName, Stream content, CancellationToken ct = default);

    Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct = default);
}
