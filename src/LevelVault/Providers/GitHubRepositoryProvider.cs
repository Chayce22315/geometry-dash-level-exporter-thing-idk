using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace LevelVault.Providers;

/// <summary>
/// Uses GitHub's documented public repository search API. Repository metadata
/// is a lead only; this provider does not download code or bypass access controls.
/// </summary>
public sealed class GitHubRepositoryProvider : IArchiveProvider
{
    private static readonly HttpClient Client = CreateClient();
    private static readonly SemaphoreSlim RateGate = new(1, 1);
    private static DateTime _lastRequestUtc = DateTime.MinValue;
    private static readonly Regex LevelIdPattern = new(@"\blevel\s*(?:id)?\s*[:#=]\s*(\d{4,12})\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex SongIdPattern = new(@"\b(?:song(?:\s*id)?|custom\s*song)\s*[:#=]\s*(\d{1,10})\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public string Id => "github-repositories";
    public string DisplayName => "github repositories";
    public ProviderCapabilities Capabilities =>
        ProviderCapabilities.SearchById | ProviderCapabilities.SearchByName |
        ProviderCapabilities.SearchByCreator | ProviderCapabilities.RetrieveMetadata;

    public async Task<ProviderSearchResponse> SearchAsync(
        ProviderSearchQuery query, CancellationToken cancellationToken)
    {
        var term = query.FreeText ?? query.LevelId ?? query.Name ?? query.Creator ?? query.SongId;
        if (string.IsNullOrWhiteSpace(term))
            return new(Id, false, [], "enter a search clue.");

        // Repository search indexes names, descriptions, and README text. Avoid
        // forcing README-only matches, which excluded useful repository metadata.
        var search = $"{term.Trim()} geometry dash";
        var url = "https://api.github.com/search/repositories?q=" +
                  Uri.EscapeDataString(search) + "&sort=updated&order=desc&per_page=30";

        try
        {
            await RateGate.WaitAsync(cancellationToken);
            try
            {
                var wait = TimeSpan.FromSeconds(7) - (DateTime.UtcNow - _lastRequestUtc);
                if (wait > TimeSpan.Zero) await Task.Delay(wait, cancellationToken);
                using var response = await Client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                _lastRequestUtc = DateTime.UtcNow;
                if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests)
                    return new(Id, false, [], "github public search rate limit reached. wait before retrying.");
                response.EnsureSuccessStatusCode();

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                var results = new List<ProviderSearchResult>();
                if (json.RootElement.TryGetProperty("items", out var items))
                {
                    foreach (var item in items.EnumerateArray())
                    {
                        var fullName = ReadString(item, "full_name");
                        var htmlUrl = ReadString(item, "html_url");
                        if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(htmlUrl)) continue;
                        var description = ReadString(item, "description");
                        var searchableText = fullName + " " + description;
                        var levelId = LevelIdPattern.Match(searchableText);
                        var songId = SongIdPattern.Match(searchableText);
                        results.Add(new ProviderSearchResult(
                            fullName + (string.IsNullOrWhiteSpace(description) ? "" : " · " + description),
                            htmlUrl,
                            "github repository metadata",
                            LevelId: levelId.Success ? levelId.Groups[1].Value : null,
                            SongId: songId.Success ? songId.Groups[1].Value : null,
                            RawDataReference: fullName));
                    }
                }

                return new(Id, true, results,
                    $"github repository search returned {results.Count} metadata lead(s). repository contents have not been inspected.");
            }
            finally { RateGate.Release(); }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return new(Id, false, [], "github repository search failed: " + ex.Message);
        }
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("LevelVault/0.4 (public repository search; github.com/Chayce22315)");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }

    private static string? ReadString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() : null;
}
