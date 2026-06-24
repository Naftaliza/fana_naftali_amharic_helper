using AmharicHelper.Domain.Entities;

namespace AmharicHelper.Application.Abstractions;

/// <summary>Issues and validates JWT access tokens.</summary>
public interface IJwtService
{
    string CreateAccessToken(User user);
    string CreateRefreshToken();
}

/// <summary>Hashes and verifies user passwords.</summary>
public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

/// <summary>Persists uploaded files to a storage backend (local disk in the MVP).</summary>
public interface IFileStorage
{
    /// <summary>Save the file and return the storage path/key.</summary>
    Task<string> SaveAsync(byte[] content, string fileName, CancellationToken ct = default);
    Task<byte[]> ReadAsync(string path, CancellationToken ct = default);
    Task DeleteAsync(string path, CancellationToken ct = default);
}
