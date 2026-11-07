using LevelVault.Models;
using LevelVault.Providers;

namespace LevelVault.Services;

/// <summary>
/// Performs bounded, multi-round deep research. Each query is sent to every
/// registered provider, successful responses are cached, and every query is
/// checkpointed so cancellation never discards already collected clues.
/// </summary>
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
        Investigation investigation,
        IProgress<ResearchProgress>? progress = null,
        Func<CancellationToken, Task>? checkpoint = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(investigation);
        var providers = _registry.Providers.ToArray();
        var isResuming = investigation.PendingResearchQueries.Count > 0;
        if (!isResuming) investigation.CompletedResearchQueries = [];
        var savedQueue = isResuming
            ? investigation.PendingResearchQueries.Select(x =>
                new DeepResearchPlanner.ResearchQuery(x.Text, x.Depth, x.Reason))
            : DeepResearchPlanner.CreateSeedQueries(investigation);
        var queue = new Queue<DeepResearchPlanner.ResearchQuery>(savedQueue);
        var queued = new HashSet<string>(queue.Select(x => x.Text), StringComparer.OrdinalIgnoreCase);
        var completed = new HashSet<string>(investigation.CompletedResearchQueries, StringComparer.OrdinalIgnoreCase);
        var knownSources = new HashSet<string>(
            investigation.Clues.Select(x => NormalizeSource(x.SourceUrl)).Where(x => x.Length > 0),
            StringComparer.OrdinalIgnoreCase);

        var queriesExecuted = 0;
        var providersChecked = 0;
        var providersSucceeded = 0;
        var providersFailed = 0;
        var cluesAdded = 0;
        var maxDepthReached = 0;
        var messages = new List<string>();

        investigation.ResearchStatus = "running";
        investigation.LastResearchStartedUtc = DateTime.UtcNow;

        DeepResearchPlanner.ResearchQuery? activeQuery = null;
        try
        {
            if (checkpoint is not null) await checkpoint(cancellationToken);
            while (queue.Count > 0 && queriesExecuted < DeepResearchPlanner.MaxQueriesPerRun)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var item = queue.Dequeue();
                if (item.Depth > DeepResearchPlanner.MaxDepth ||
                    completed.Contains(item.Text)) continue;
                activeQuery = item;

                maxDepthReached = Math.Max(maxDepthReached, item.Depth);
                progress?.Report(new ResearchProgress(
                    "searching", item.Text, item.Depth, queriesExecuted,
                    DeepResearchPlanner.MaxQueriesPerRun, cluesAdded,
                    $"deep research · round {item.Depth + 1} · {item.Reason}"));

                var newlyDiscovered = new List<Clue>();
                foreach (var provider in providers)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    providersChecked++;
                    var cacheKey = provider.Id + "|deep-v2|" + item.Text.Trim().ToLowerInvariant();
                    ProviderSearchResponse response;
                    ProviderSearchResponse? cached = null;
                    try
                    {
                        cached = await _cache.GetAsync<ProviderSearchResponse>(
                            cacheKey, TimeSpan.FromHours(12), cancellationToken);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch
                    {
                        // A damaged or inaccessible cache entry should trigger a fresh provider request.
                    }

                    if (cached is not null)
                    {
                        response = cached;
                    }
                    else
                    {
                        try
                        {
                            response = await provider.SearchAsync(
                                new ProviderSearchQuery(FreeText: item.Text), cancellationToken);
                        }
                        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                        {
                            throw;
                        }
                        catch (Exception ex)
                        {
                            // A single unavailable provider must not abort the entire investigation.
                            response = new ProviderSearchResponse(
                                provider.Id, false, Array.Empty<ProviderSearchResult>(),
                                $"provider failed: {ex.GetType().Name}: {ex.Message}");
                        }

                        if (response.Succeeded)
                        {
                            try
                            {
                                await _cache.SetAsync(cacheKey, response, cancellationToken);
                            }
                            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                            {
                                throw;
                            }
                            catch (Exception ex)
                            {
                                messages.Add($"{provider.DisplayName}: response could not be cached ({ex.GetType().Name}).");
                            }
                        }
                    }

                    messages.Add($"{provider.DisplayName}: {response.Message}");
                    if (response.Succeeded) providersSucceeded++;
                    else providersFailed++;

                    // Some providers can return useful partial results before a rate limit or transient failure.
                    foreach (var result in response.Results)
                    {
                        var sourceKey = NormalizeSource(result.SourceUrl);
                        if (sourceKey.Length == 0 || !knownSources.Add(sourceKey)) continue;

                        var clue = new Clue
                        {
                            Kind = result.RecordType,
                            Title = result.Title,
                            Detail = $"discovered by deep research query: {item.Text}. " +
                                     (result.LevelId is null ? "" : "level id: " + result.LevelId + ". ") +
                                     (result.Creator is null ? "" : "source account/channel (unverified): " + result.Creator + ". ") +
                                     (result.SongId is null ? "" : "song id: " + result.SongId + ". ") +
                                     (result.RawDataReference is null ? "" : "archive identifier: " + result.RawDataReference + ". "),
                            SourceUrl = result.SourceUrl,
                            Confidence = "unverified"
                        };
                        investigation.Clues.Add(clue);
                        newlyDiscovered.Add(clue);
                        cluesAdded++;
                    }
                }

                completed.Add(item.Text);
                investigation.CompletedResearchQueries = completed.ToList();
                queriesExecuted++;

                foreach (var clue in newlyDiscovered)
                {
                    foreach (var followUp in DeepResearchPlanner.CreateFollowUpQueries(clue, item.Depth + 1))
                    {
                        if (completed.Contains(followUp.Text) || !queued.Add(followUp.Text)) continue;
                        queue.Enqueue(followUp);
                    }
                }

                activeQuery = null;
                investigation.ResearchQueriesExecuted += 1;
                investigation.ResearchCluesAdded += newlyDiscovered.Count;
                investigation.PendingResearchQueries = queue
                    .Select(x => new ResearchQueryState { Text = x.Text, Depth = x.Depth, Reason = x.Reason })
                    .ToList();
                if (checkpoint is not null) await checkpoint(cancellationToken);
                progress?.Report(new ResearchProgress(
                    "query complete", item.Text, item.Depth, queriesExecuted,
                    DeepResearchPlanner.MaxQueriesPerRun, cluesAdded,
                    $"{newlyDiscovered.Count} new source(s) · {queue.Count} follow-up search(es) queued"));
            }

            investigation.PendingResearchQueries = queue
                .Select(x => new ResearchQueryState { Text = x.Text, Depth = x.Depth, Reason = x.Reason })
                .ToList();
            investigation.ResearchStatus = queue.Count == 0 ? "completed" : "completed · query limit reached";
            investigation.LastResearchCompletedUtc = DateTime.UtcNow;
            if (checkpoint is not null) await checkpoint(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            investigation.PendingResearchQueries = (activeQuery is null ? queue : new[] { activeQuery }.Concat(queue))
                .Select(x => new ResearchQueryState { Text = x.Text, Depth = x.Depth, Reason = x.Reason })
                .ToList();
            investigation.ResearchStatus = "paused · cancelled";
            if (checkpoint is not null) await checkpoint(CancellationToken.None);
            throw;
        }
        catch
        {
            investigation.PendingResearchQueries = (activeQuery is null ? queue : new[] { activeQuery }.Concat(queue))
                .Select(x => new ResearchQueryState { Text = x.Text, Depth = x.Depth, Reason = x.Reason })
                .ToList();
            investigation.ResearchStatus = "paused · error";
            if (checkpoint is not null) await checkpoint(CancellationToken.None);
            throw;
        }

        return new ResearchRunSummary(
            queriesExecuted, providersChecked, providersSucceeded, providersFailed,
            cluesAdded, maxDepthReached, queue.Count, messages);
    }

    private static string NormalizeSource(string? source)
    {
        if (string.IsNullOrWhiteSpace(source)) return "";
        if (!Uri.TryCreate(source, UriKind.Absolute, out var uri)) return source.Trim();
        var builder = new UriBuilder(uri) { Fragment = "" };
        return builder.Uri.AbsoluteUri.TrimEnd('/');
    }
}

public sealed record ResearchProgress(
    string Stage,
    string CurrentQuery,
    int Depth,
    int QueriesCompleted,
    int QueryLimit,
    int CluesAdded,
    string Detail);

public sealed record ResearchRunSummary(
    int QueriesExecuted,
    int ProvidersChecked,
    int ProvidersSucceeded,
    int ProvidersFailed,
    int CluesAdded,
    int MaxDepthReached,
    int FollowUpsRemaining,
    IReadOnlyList<string> Messages);
