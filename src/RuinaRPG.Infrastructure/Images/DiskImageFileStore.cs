using Microsoft.Extensions.Options;

namespace RuinaRPG.Infrastructure.Images;

public class DiskImageFileStore(IOptions<ImageStorageOptions> options) : IImageFileStore
{
    public async Task<string> SaveAsync(byte[] bytes, string extension)
    {
        Directory.CreateDirectory(options.Value.ImagesPath);
        var fileName = $"{Guid.NewGuid()}{extension}";
        var fullPath = Path.Combine(options.Value.ImagesPath, fileName);
        await File.WriteAllBytesAsync(fullPath, bytes);
        return fileName;
    }
}
