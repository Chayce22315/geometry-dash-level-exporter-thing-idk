using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace LevelVault.Providers;

/// <summary>
/// Searches DuckDuckGo's public HTML results page. Results are unverified web
/// references only; this provider does not bypass access controls or download level files.
/// </summary>
public sealed class DuckDuckGoSearchProvider : IArchiveProvider
{
    private static readonly HttpClient Client = CreateClient();
    private static readonly SemaphoreSlim RateGate = new(1, 1);
    private static DateTime _lastRequestUtc = DateTime.MinValue;
    private static readonly Regex ResultLink = new(
        @"<a[^>]+class=""result__a""[^>]+href=""([^""]+)""[^>]*>(.*?)</a>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex ResultSnippet = new(
        @"<a[^>]+class=""result__snippet""[^>]*>(.*?)</a>|<div[^>]+class=""result__snippet""[^>]*>(.*?)</div>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex Tags = new(@"<[^>]+>", RegexOptions.Compiled);

    public string Id => "duckduckgo-web";
    public string DisplayName => "duckduckgo web search";
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
                var wait = TimeSpan.FromSeconds(5) - (DateTime.UtcNow - _lastRequestUtc);
                if (wait > TimeSpan.Zero) await Task.Delay(wait, cancellationToken);
                var url = "https://html.duckduckgo.com/html/?q=" + Uri.EscapeDataString(term + " Geometry Dash");
                using var response = await Client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                _lastRequestUtc = DateTime.UtcNow;
                if (response.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.Forbidden)
                    return new(Id, false, [], "duckduckgo temporarily limited the public search request.");
                response.EnsureSuccessStatusCode();
                var html = await response.Content.ReadAsStringAsync(cancellationToken);
                var snippets = ResultSnippet.Matches(html).Cast<Match>()
                    .Select(m => Clean(m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value))
                    .ToArray();
                var results = new List<ProviderSearchResult>();
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var index = 0;
                foreach (Match match in ResultLink.Matches(html))
                {
                    var target = DecodeTarget(WebUtility.HtmlDecode(match.Groups[1].Value));
                    if (!Uri.TryCreate(target, UriKind.Absolute, out var parsed) ||
                        parsed.Scheme is not ("http" or "https") || !seen.Add(parsed.AbsoluteUri))
                        continue;
                    var title = Clean(match.Groups[2].Value);
                    if (title.Length == 0) continue;
                    var snippet = index < snippets.Length ? snippets[index] : "";
                    index++;
                    var text = title + " " + snippet;
                    results.Add(new ProviderSearchResult(
                        title + (snippet.Length == 0 ? "" : " · " + snippet),
                        parsed.AbsoluteUri,
                        "public web search result",
                        LevelId: Extract(@"\blevel\s*(?:id)?\s*[:#=]?\s*(\d{4,12})\b", text),
                        SongId: Extract(@"\b(?:song(?:\s*id)?|custom\s*song)\s*[:#=]\s*(\d{1,10})\b", text)));
                    if (results.Count >= 25) break;
                }

                return new(Id, true, results,
                    $"duckduckgo returned {results.Count} public web result(s). results are leads and must be corroborated.");
            }
            finally { RateGate.Release(); }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return new(Id, false, [], "duckduckgo web search failed: " + ex.Message);
        }
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("LevelVault/0.5 (public web research; github.com/Chayce22315)");
        return client;
    }

    private static string DecodeTarget(string value)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out var direct) &&
            direct.Host.EndsWith("duckduckgo.com", StringComparison.OrdinalIgnoreCase) &&
            direct.Query.Length > 1)
        {
            var parameters = direct.Query.TrimStart('?').Split('&');
            var target = parameters.FirstOrDefault(x => x.StartsWith("uddg=", StringComparison.OrdinalIgnoreCase));
            if (target is not null) return Uri.UnescapeDataString(target[5..]);
        }
        return value.StartsWith("//", StringComparison.Ordinal) ? "https:" + value : value;
    }

    private static string Clean(string value) =>
        WebUtility.HtmlDecode(Tags.Replace(value, " ")).Replace("\\n", " ").Trim();

    private static string? Extract(string pattern, string value)
    {
        var match = Regex.Match(value, pattern, RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : null;
    }
}
