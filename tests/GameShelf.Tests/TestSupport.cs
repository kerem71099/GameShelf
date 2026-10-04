namespace GameShelf.Tests;

/// <summary>Her test için izole geçici klasör (çıkışta silinir).</summary>
public sealed class TempDirectory : IDisposable
{
    public TempDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "gameshelf-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string CreateFile(string relativePath, byte[] content)
    {
        var full = System.IO.Path.Combine(Path, relativePath);
        var directory = System.IO.Path.GetDirectoryName(full);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllBytes(full, content);
        return full;
    }

    public string CreateTextFile(string relativePath, string content)
        => CreateFile(relativePath, System.Text.Encoding.ASCII.GetBytes(content));

    public string CreateDirectory(string relativePath)
    {
        var full = System.IO.Path.Combine(Path, relativePath);
        Directory.CreateDirectory(full);
        return full;
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
        catch (Exception)
        {
            // test temizliği kritik değil
        }
    }
}
