namespace MichiMap.Api.Services;

// Used in local development when AzureBlobStorage connection string is not configured.
public class NoOpBlobStorageService : IBlobStorageService
{
    public Task<string?> UploadPhotoAsync(IFormFile file, string blobName) =>
        Task.FromResult<string?>(null);
}
