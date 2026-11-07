using System.Net;
using System.Net.Http;
using System.Text.Json;

namespace LevelVault.Providers;

/// <summary>
/// Searches Internet Archive's documented Advanced Search API.
/// Results are archive metadata references, not proof that a record contains a playable level.
/// </summary>
public sealed class InternetArchiveProvider : IArchiveProvider
{
    private static readonly HttpClient Client = CreateClient();
    private static readonly SemaphoreSlim RateGate = new(1, 1);
    private static DateTime _lastRequestUtc = DateTime.MinValue;

    public string Id => "internet-archive";
    public string DisplayName => "internet archive";
    public ProviderCapabilities Capabilities =>
        ProviderCapabilities.SearchById | ProviderCapabilities.SearchByName |
        ProviderCapabilities.SearchByCreator | ProviderCapabilities.SearchBySong |
        ProviderCapabilities.RetrieveMetadata;

    public async Task<ProviderSearchResponse> SearchAsync(
        ProviderSearchQuery query, CancellationToken cancellationToken)
    {
        var terms = new[] { query.LevelId, query.Name, query.Creator, query.SongId, query.FreeText }
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (terms.Length == 0)
            return new(Id, false, [], "enter at least one search clue.");

        var expression = string.Join(" AND ", terms.Select(term =>
            $"(title:\"{EscapeTerm(term)}\" OR description:\"{EscapeTerm(term)}\" OR identifier:\"{EscapeTerm(term)}\" OR creator:\"{EscapeTerm(term)}\")"));
        var url = "https://archive.org/advancedsearch.php?q=" + Uri.EscapeDataString(expression) +
                  "&fl[]=identifier&fl[]=title&fl[]=creator&fl[]=date&fl[]=description&rows=50&page=1&output=json";

        try
        {
            await RateGate.WaitAsync(cancellationToken);
            try
            {
                var wait = TimeSpan.FromSeconds(3) - (DateTime.UtcNow - _lastRequestUtc);
                if (wait > TimeSpan.Zero) await Task.Delay(wait, cancellationToken);
                using var response = await Client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                _lastRequestUtc = DateTime.UtcNow;
                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                    return new(Id, false, [], "internet archive rate limited the request. wait before retrying.");
                response.EnsureSuccessStatusCode();
                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                var docs = json.RootElement.GetProperty("response").GetProperty("docs");
                var results = new List<ProviderSearchResult>();
                foreach (var doc in docs.EnumerateArray())
                {
                    var identifier = ReadString(doc, "identifier");
                    if (string.IsNullOrWhiteSpace(identifier)) continue;
                    var title = ReadString(doc, "title") ?? identifier;
                    var creator = ReadString(doc, "creator");
                    var date = ReadString(doc, "date");
                    var description = ReadString(doc, "description");
                    var notes = string.Join(" · ", new[] { creator, date, description }.Where(x => !string.IsNullOrWhiteSpace(x)));
                    results.Add(new ProviderSearchResult(
                        title + (string.IsNullOrWhiteSpace(notes) ? "" : " · " + notes),
                        "https://archive.org/details/" + Uri.EscapeDataString(identifier),
                        "archive metadata",
                        Creator: creator,
                        RawDataReference: identifier));
                }
                return new(Id, true, results, $"internet archive returned {results.Count} metadata record(s). records are leads, not recovered level data.");
            }
            finally { RateGate.Release(); }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException)
        {
            return new(Id, false, [], "internet archive search failed: " + ex.Message);
        }
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("LevelVault/0.2 (public archive research; contact: github.com/Chayce22315)");
        return client;
    }

    private static string EscapeTerm(string value) => value.Replace("\\", " ").Replace("\"", " ").Replace(":", " ");
    private static string? ReadString(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value)) return null;
        if (value.ValueKind == JsonValueKind.String) return value.GetString();
        if (value.ValueKind == JsonValueKind.Array)
            return string.Join(", ", value.EnumerateArray().Select(x => x.ToString()));
        return value.ToString();
    }
}
