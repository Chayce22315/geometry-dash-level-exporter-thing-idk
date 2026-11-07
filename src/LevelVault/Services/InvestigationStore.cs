using System.Text.Json;
using LevelVault.Models;

namespace LevelVault.Services;

public sealed class InvestigationStore
{
    private readonly string _root = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LevelVault", "investigations");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public InvestigationStore() => Directory.CreateDirectory(_root);

    public async Task<IReadOnlyList<Investigation>> LoadAllAsync(CancellationToken cancellationToken = default)
    {
        var items = new List<Investigation>();
        foreach (var file in Directory.EnumerateFiles(_root, "*.json"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await using var stream = File.OpenRead(file);
                var item = await JsonSerializer.DeserializeAsync<Investigation>(stream, JsonOptions, cancellationToken);
                if (item is not null) items.Add(item);
            }
            catch (JsonException) { /* preserve malformed files for manual inspection */ }
            catch (IOException) { }
        }
        return items.OrderByDescending(x => x.UpdatedUtc).ToArray();
    }

    public async Task SaveAsync(Investigation item, CancellationToken cancellationToken = default)
    {
        item.UpdatedUtc = DateTime.UtcNow;
        var path = Path.Combine(_root, item.Id.ToString("N") + ".json");
        var temp = path + ".tmp";
        await using (var stream = File.Create(temp))
            await JsonSerializer.SerializeAsync(stream, item, JsonOptions, cancellationToken);
        File.Move(temp, path, true);
    }
}
