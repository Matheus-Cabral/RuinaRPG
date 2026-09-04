using Microsoft.AspNetCore.Components.Forms;

namespace RuinaRPG.Tests.Client.Shared;

/// <summary>
/// A real file picked through an &lt;InputFile&gt; can't be reproduced by bUnit (no browser, no
/// DOM), so components under test that take IBrowserFile directly (like ImageAttachmentField's
/// FilesChanged handler) are exercised through a test-only seam fed one of these instead.
/// </summary>
public class FakeBrowserFile : IBrowserFile
{
    private readonly byte[] _content;

    public FakeBrowserFile(string name, string content = "conteudo")
    {
        Name = name;
        _content = System.Text.Encoding.UTF8.GetBytes(content);
    }

    public string Name { get; }
    public DateTimeOffset LastModified => DateTimeOffset.UtcNow;
    public long Size => _content.Length;
    public string ContentType => "image/png";

    public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default)
        => new MemoryStream(_content);
}
