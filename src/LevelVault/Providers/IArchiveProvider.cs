namespace LevelVault.Providers;

/// <summary>
/// Contract for independently implemented, public/authorized archive sources.
/// Implementations must document their access method and enforce source rate limits.
/// </summary>
public interface IArchiveProvider
{
    string Id { get; }
    string DisplayName { get; }
    ProviderCapabilities Capabilities { get; }
    Task<ProviderSearchResponse> SearchAsync(ProviderSearchQuery query, CancellationToken cancellationToken);
}

[Flags]
public enum ProviderCapabilities
{
    None = 0,
    SearchById = 1,
    SearchByName = 2,
    SearchByCreator = 4,
    SearchBySong = 8,
    RetrieveLevelData = 16,
    RetrieveMetadata = 32
}

public sealed record ProviderSearchQuery(
    string? LevelId = null,
    string? Name = null,
    string? Creator = null,
    string? SongId = null,
    string? FreeText = null);

public sealed record ProviderSearchResponse(
    string ProviderId,
    bool Succeeded,
    IReadOnlyList<ProviderSearchResult> Results,
    string? Message = null);

public sealed record ProviderSearchResult(
    string Title,
    string SourceUrl,
    string RecordType,
    string? LevelId = null,
    string? Creator = null,
    string? SongId = null,
    string? RawDataReference = null);
