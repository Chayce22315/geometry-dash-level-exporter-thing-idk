using System.Net;
using System.Text.Json;

namespace LevelVault.Providers;

/// <summary>
/// Reads public YouTube search-page results. The HTML/embedded-data format is
/// not a stable API, so failures are reported rather than worked around.
/// Results are video leads, never proof of original level data.
/// </summary>
public sealed class YouTubeSearchProvider : IArchiveProvider
{
    private static readonly HttpClient Client = CreateClient();
    private static readonly SemaphoreSlim RateGate = new(1, 1);
    private static DateTime _lastRequestUtc = DateTime.MinValue;

    public string Id => "youtube-public-search";
    public string DisplayName => "youtube public search";
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
                var wait = TimeSpan.FromSeconds(8) - (DateTime.UtcNow - _lastRequestUtc);
                if (wait > TimeSpan.Zero) await Task.Delay(wait, cancellationToken);
                var searchTerm = term.Contains("geometry dash", StringComparison.OrdinalIgnoreCase)
                    ? term : term + " Geometry Dash";
                var url = "https://www.youtube.com/results?search_query=" + Uri.EscapeDataString(searchTerm);
                using var response = await Client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                _lastRequestUtc = DateTime.UtcNow;
                if (response.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.Forbidden)
                    return new(Id, false, [], "youtube limited or denied the public search request.");
                response.EnsureSuccessStatusCode();
                var html = await response.Content.ReadAsStringAsync(cancellationToken);
                if (!TryExtractInitialData(html, out var jsonText))
                    return new(Id, false, [], "youtube returned a page without parseable public search data. its page format may have changed or a consent page was returned.");

                using var json = JsonDocument.Parse(jsonText);
                var results = new List<ProviderSearchResult>();
                var seen = new HashSet<string>(StringComparer.Ordinal);
                CollectVideos(json.RootElement, results, seen);
                return new(Id, true, results,
                    $"youtube returned {results.Count} public video result(s). videos are references, not recovered level files.");
            }
            finally { RateGate.Release(); }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return new(Id, false, [], "youtube public search failed: " + ex.Message);
        }
    }

    private static void CollectVideos(JsonElement element, List<ProviderSearchResult> results, HashSet<string> seen)
    {
        if (results.Count >= 25) return;
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("videoRenderer", out var video) &&
                video.ValueKind == JsonValueKind.Object)
            {
                var id = ReadString(video, "videoId");
                var title = ReadRuns(video, "title");
                var channel = ReadRuns(video, "ownerText");
                var description = ReadRuns(video, "descriptionSnippet");
                var published = ReadRuns(video, "publishedTimeText");
                if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(title) && seen.Add(id))
                {
                    var detail = string.Join(" · ", new[] { channel, published, description }
                        .Where(x => !string.IsNullOrWhiteSpace(x)));
                    results.Add(new ProviderSearchResult(
                        title + (detail.Length == 0 ? "" : " · " + detail),
                        "https://www.youtube.com/watch?v=" + Uri.EscapeDataString(id),
                        "youtube video",
                        Creator: channel));
                }
                return;
            }

            foreach (var property in element.EnumerateObject())
            {
                CollectVideos(property.Value, results, seen);
                if (results.Count >= 25) return;
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                CollectVideos(item, results, seen);
                if (results.Count >= 25) return;
            }
        }
    }

    private static string? ReadString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() : null;

    private static string? ReadRuns(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value)) return null;
        if (value.TryGetProperty("simpleText", out var simple) && simple.ValueKind == JsonValueKind.String)
            return simple.GetString();
        if (!value.TryGetProperty("runs", out var runs) || runs.ValueKind != JsonValueKind.Array)
            return null;
        var text = string.Concat(runs.EnumerateArray().Select(run => ReadString(run, "text")));
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static bool TryExtractInitialData(string html, out string json)
    {
        json = "";
        var marker = html.IndexOf("ytInitialData", StringComparison.Ordinal);
        if (marker < 0) return false;
        var start = html.IndexOf('{', marker);
        if (start < 0) return false;

        var depth = 0;
        var inString = false;
        var escaped = false;
        for (var i = start; i < html.Length; i++)
        {
            var ch = html[i];
            if (inString)
            {
                if (escaped) escaped = false;
                else if (ch == '\\') escaped = true;
                else if (ch == '"') inString = false;
                continue;
            }
            if (ch == '"') inString = true;
            else if (ch == '{') depth++;
            else if (ch == '}' && --depth == 0)
            {
                json = html[start..(i + 1)];
                return true;
            }
        }
        return false;
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(25) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36 LevelVault/0.7");
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US,en;q=0.9");
        return client;
    }
}
