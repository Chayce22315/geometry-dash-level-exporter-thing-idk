using LevelVault.Models;
using LevelVault.Providers;

namespace LevelVault.Services;

/// <summary>Runs registered providers, caches normalized results, and adds source-linked clues to an investigation.</summary>
public sealed class InvestigationResearchService
{
    private readonly ProviderRegistry _registry;
    private readonly JsonResponseCache _cache;

    public InvestigationResearchService(ProviderRegistry registry, JsonResponseCache cache)
    {
        _registry = registry;
        _cache = cache;
    }

    public async Task<ResearchRunSummary> ResearchAsync(
        Investigation investigation, CancellationToken cancellationToken = default)
    {
        var providers = _registry.Providers.ToArray();
        var added = 0;
        var failed = 0;
        var messages = new List<string>();
        foreach (var provider in providers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var query = new ProviderSearchQuery(FreeText: investigation.Query);
            var cacheKey = provider.Id + "|" + investigation.Query.Trim().ToLowerInvariant();
            ProviderSearchResponse? response = await _cache.GetAsync<ProviderSearchResponse>(
                cacheKey, TimeSpan.FromHours(12), cancellationToken);
            if (response is null)
            {
                response = await provider.SearchAsync(query, cancellationToken);
                if (response.Succeeded)
                    await _cache.SetAsync(cacheKey, response, cancellationToken);
            }
            messages.Add(response.Message ?? provider.DisplayName);
            if (!response.Succeeded) { failed++; continue; }

            foreach (var result in response.Results)
            {
                if (investigation.Clues.Any(c =>
                    string.Equals(c.SourceUrl, result.SourceUrl, StringComparison.OrdinalIgnoreCase)))
                    continue;
                investigation.Clues.Add(new Clue
                {
                    Kind = result.RecordType,
                    Title = result.Title,
                    Detail = "automatically discovered metadata lead. " +
                             (result.LevelId is null ? "" : "level id: " + result.LevelId + ". ") +
                             (result.Creator is null ? "" : "creator: " + result.Creator + ". ") +
                             (result.SongId is null ? "" : "song id: " + result.SongId + ". ") +
                             (result.RawDataReference is null ? "" : "archive identifier: " + result.RawDataReference + ". "),
                    SourceUrl = result.SourceUrl,
                    Confidence = "unverified"
                });
                added++;
            }
        }
        return new ResearchRunSummary(providers.Length, providers.Length - failed, failed, added, messages);
    }
}

public sealed record ResearchRunSummary(
    int ProvidersChecked, int ProvidersSucceeded, int ProvidersFailed,
    int CluesAdded, IReadOnlyList<string> Messages);
