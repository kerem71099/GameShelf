using System.Security.Cryptography;
using System.Text;
using GameShelf.Application.Abstractions;

namespace GameShelf.Infrastructure.OS;

/// <summary>
/// Büyük ISO/CHD dosyaları için hızlı parmak izi: <c>boyut:ilkParca:sonParca</c>.
/// Amaç duplicate tespiti — kriptografik doğrulama değil.
/// </summary>
public sealed class PartialFileHasher : IFileHasher
{
    private const int ChunkSize = 512 * 1024;

    public async Task<string> ComputeAsync(string path, CancellationToken cancellationToken = default)
    {
        if (Directory.Exists(path))
        {
            return ComputeDirectoryFingerprint(path);
        }

        if (!File.Exists(path))
        {
            return string.Empty;
        }

        var info = new FileInfo(path);

        try
        {
            using var stream = new FileStream(
                path, FileMode.Open, FileAccess.Read, FileShare.Read, ChunkSize, useAsync: true);

            var head = new byte[(int)Math.Min(ChunkSize, info.Length)];
            await stream.ReadExactlyAsync(head, cancellationToken).ConfigureAwait(false);

            var headHash = ShortHash(head);

            if (info.Length <= ChunkSize)
            {
                return $"{info.Length}:{headHash}:";
            }

            var tail = new byte[ChunkSize];
            stream.Seek(-ChunkSize, SeekOrigin.End);
            await stream.ReadExactlyAsync(tail, cancellationToken).ConfigureAwait(false);

            return $"{info.Length}:{headHash}:{ShortHash(tail)}";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Parmak izi alınamasa da oyun eklenebilir; duplicate tespiti bu tur atlanır.
            return $"{info.Length}:unavailable";
        }
    }

    /// <summary>PS3 gibi klasör oyunları için: dosya adları + boyutlardan parmak izi.</summary>
    private static string ComputeDirectoryFingerprint(string directory)
    {
        try
        {
            var builder = new StringBuilder();
            long total = 0;

            foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
                         .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                         .Take(200))
            {
                var info = new FileInfo(file);
                total += info.Length;
                builder.Append(Path.GetRelativePath(directory, file)).Append(':').Append(info.Length).Append(';');
            }

            return $"dir:{total}:{ShortHash(Encoding.UTF8.GetBytes(builder.ToString()))}";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return "dir:unavailable";
        }
    }

    private static string ShortHash(byte[] data) =>
        Convert.ToHexString(SHA256.HashData(data))[..16].ToLowerInvariant();
}
