namespace DailyGourmet.Api.Options;

public class FileStorageOptions
{
    public const string SectionName = "FileStorage";

    /// <summary>Local disk root for uploaded files (support-ticket attachments). Relative paths are
    /// resolved against the app's content root. See LocalFileStorageService — swap for an
    /// AzureBlobFileStorageService behind the same IFileStorageService if the API ever needs to
    /// scale horizontally or run on ephemeral-disk hosting.</summary>
    public string RootPath { get; set; } = "App_Data/uploads";
}
