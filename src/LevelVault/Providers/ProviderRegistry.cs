namespace LevelVault.Providers;

/// <summary>
/// Runtime registry. No provider is silently assumed to be available.
/// </summary>
public sealed class ProviderRegistry
{
    private readonly Dictionary<string, IArchiveProvider> _providers = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<IArchiveProvider> Providers => _providers.Values;

    public void Register(IArchiveProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        if (string.IsNullOrWhiteSpace(provider.Id))
            throw new ArgumentException("Provider id cannot be empty.", nameof(provider));
        _providers[provider.Id] = provider;
    }

    public bool TryGet(string id, out IArchiveProvider? provider) => _providers.TryGetValue(id, out provider);
}
