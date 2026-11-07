using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace LevelVault.Providers;

/// <summary>
/// Searches Reddit's publicly accessible JSON search endpoint. It only records
/// public post references; it does not log in, vote, post, or bypass restrictions.
/// </summary>
public sealed class RedditSearchProvider : IArchiveProvider
{
    private static readonly HttpClient Client = CreateClient();
    private static readonly SemaphoreSlim RateGate = new(1, 1);
    private static DateTime _lastRequestUtc = DateTime.MinValue;
    private static readonly Regex LevelIdPattern = new(@"\blevel\s*(?:id)?\s*[:#=]?\s*(\d{4,12})\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex SongIdPattern = new(@"\b(?:song(?:\s*id)?|custom\s*song)\s*[:#=]\s*(\d{1,10})\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public string Id => "reddit-public-search";
    public string DisplayName => "reddit public search";
    public ProviderCapabilities Capabilities =>
        ProviderCapabilities.SearchById | ProviderCapabilities.SearchByName |
        ProviderCapabilities.SearchByCreator | ProviderCapabilities.SearchBySong |
        ProviderCapabilities.RetrieveMetadata;

    public async Task<ProviderSearchResponse> SearchAsync(
        ProviderSearchQuery query, CancellationToken cancellationToken)
    {
        var term = string.Join(" ", new[] { query.LevelId, query.Name, query.Creator, query.SongId, query.FreeText }
            .Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase));
        if (term.Length == 0)
            return new(Id, false, [], "enter a search clue.");

        try
        {
            await RateGate.WaitAsync(cancellationToken);
            try
            {
                var wait = TimeSpan.FromSeconds(4) - (DateTime.UtcNow - _lastRequestUtc);
                if (wait > TimeSpan.Zero) await Task.Delay(wait, cancellationToken);
                var searchTerm = term.Contains("geometry dash", StringComparison.OrdinalIgnoreCase)
                    ? term : term + " geometry dash";
                var url = "https://www.reddit.com/search.json?q=" + Uri.EscapeDataString(searchTerm) +
                          "&sort=relevance&t=all&limit=25&raw_json=1";
                using var response = await Client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                _lastRequestUtc = DateTime.UtcNow;
                if (response.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.Forbidden)
                    return new(Id, false, [], "reddit limited or denied public search. no login or access-control workaround was attempted.");
                response.EnsureSuccessStatusCode();

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                var results = new List<ProviderSearchResult>();
                if (json.RootElement.TryGetProperty("data", out var data) &&
                    data.TryGetProperty("children", out var children))
                {
                    foreach (var child in children.EnumerateArray())
                    {
                        if (!child.TryGetProperty("data", out var post)) continue;
                        var title = ReadString(post, "title");
                        var permalink = ReadString(post, "permalink");
                        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(permalink)) continue;
                        var subreddit = ReadString(post, "subreddit");
                        var author = ReadString(post, "author");
                        var body = ReadString(post, "selftext") ?? "";
                        var searchableText = title + " " + body;
                        var levelId = LevelIdPattern.Match(searchableText);
                        var songId = SongIdPattern.Match(searchableText);
                        var source = permalink.StartsWith("/", StringComparison.Ordinal)
                            ? "https://www.reddit.com" + permalink : permalink;
                        results.Add(new ProviderSearchResult(
                            title + (string.IsNullOrWhiteSpace(subreddit) ? "" : " · r/" + subreddit) + (string.IsNullOrWhiteSpace(author) ? "" : " · u/" + author),
                            source,
                            "reddit discussion",
                            LevelId: levelId.Success ? levelId.Groups[1].Value : null,
                            SongId: songId.Success ? songId.Groups[1].Value : null,
                            RawDataReference: subreddit));
                        if (results.Count >= 25) break;
                    }
                }

                return new(Id, true, results,
                    $"reddit returned {results.Count} public discussion result(s). post authors are not assumed to be level creators.");
            }
            finally { RateGate.Release(); }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return new(Id, false, [], "reddit public search failed: " + ex.Message);
        }
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("desktop:LevelVault:0.7 (public research; github.com/Chayce22315)");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        return client;
    }

    private static string? ReadString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() : null;
}
