namespace RuinaRPG.Infrastructure.Images;

public interface IImageFileStore
{
    Task<string> SaveAsync(byte[] bytes, string extension);
}
