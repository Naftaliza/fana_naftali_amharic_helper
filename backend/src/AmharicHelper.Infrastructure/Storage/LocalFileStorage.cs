using AmharicHelper.Application.Abstractions;

namespace AmharicHelper.Infrastructure.Storage;

/// <summary>Stores uploaded files on local disk. Swap for blob storage (S3/Azure Blob) in prod.</summary>
public class LocalFileStorage(string rootPath) : IFileStorage
{
    public async Task<string> SaveAsync(byte[] content, string fileName, CancellationToken ct = default)
    {
        Directory.CreateDirectory(rootPath);
        var safeName = $"{Guid.NewGuid():N}_{Path.GetFileName(fileName)}";
        var fullPath = Path.Combine(rootPath, safeName);
        await File.WriteAllBytesAsync(fullPath, content, ct);
        return fullPath;
    }

    public async Task<byte[]> ReadAsync(string path, CancellationToken ct = default)
        => await File.ReadAllBytesAsync(path, ct);

    public Task DeleteAsync(string path, CancellationToken ct = default)
    {
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }
}
