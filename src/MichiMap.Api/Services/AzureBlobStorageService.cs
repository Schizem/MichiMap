using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace MichiMap.Api.Services;

public class AzureBlobStorageService(BlobServiceClient blobClient) : IBlobStorageService
{
    private const string Container = "morel-photos";

    public async Task<string?> UploadPhotoAsync(IFormFile file, string blobName)
    {
        var containerClient = blobClient.GetBlobContainerClient(Container);
        await containerClient.CreateIfNotExistsAsync(PublicAccessType.None);

        var blobHttpHeaders = new BlobHttpHeaders { ContentType = file.ContentType };
        var blob = containerClient.GetBlobClient(blobName);

        await using var stream = file.OpenReadStream();
        await blob.UploadAsync(stream, blobHttpHeaders);

        return blob.Uri.ToString();
    }
}
