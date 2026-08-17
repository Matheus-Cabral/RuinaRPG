namespace RuinaRPG.Infrastructure.Images;

public class Image
{
    public Guid Id { get; set; }
    public required string Path { get; set; }
    public required string ContentType { get; set; }
    public Guid UploadedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
}
