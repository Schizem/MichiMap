namespace MichiMap.Api.Services;

public interface IBlobStorageService
{
    Task<string?> UploadPhotoAsync(IFormFile file, string blobName);
}
