using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LevelVault.Services;

/// <summary>Small local cache for public provider responses. Cache entries expire and can be safely discarded.</summary>
public sealed class JsonResponseCache
{
    private readonly string _root = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LevelVault", "cache");
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = false };

    public JsonResponseCache() => Directory.CreateDirectory(_root);

    public async Task<T?> GetAsync<T>(string key, TimeSpan maxAge, CancellationToken cancellationToken = default)
    {
        var path = GetPath(key);
        if (!File.Exists(path)) return default;
        try
        {
            if (DateTime.UtcNow - File.GetLastWriteTimeUtc(path) > maxAge)
            {
                File.Delete(path);
                return default;
            }
            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<T>(stream, Options, cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, CancellationToken cancellationToken = default)
    {
        var path = GetPath(key);
        var temp = path + ".tmp";
        await using (var stream = File.Create(temp))
            await JsonSerializer.SerializeAsync(stream, value, Options, cancellationToken);
        File.Move(temp, path, true);
    }

    private string GetPath(string key)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
        return Path.Combine(_root, hash + ".json");
    }
}
