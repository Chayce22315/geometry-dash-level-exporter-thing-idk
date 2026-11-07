using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace LevelVault.Providers;

/// <summary>
/// Performs an exact-id lookup against Geometry Dash's public Boomlings level-list endpoint.
/// It records current server metadata only; it does not claim that metadata is level data.
/// </summary>
public sealed class BoomlingsLevelProvider : IArchiveProvider
{
    private const string Endpoint = "https://www.boomlings.com/database/getGJLevels21.php";
    private const string CommonSecret = "Wmfd2893gb7";
    private const int GameVersion = 22;
    private const int BinaryVersion = 51;
    private static readonly HttpClient Client = CreateClient();
    private static readonly SemaphoreSlim RateGate = new(1, 1);
    private static DateTime _lastRequestUtc = DateTime.MinValue;
    private static readonly ConcurrentDictionary<string, (DateTime CachedUtc, ProviderSearchResponse Response)> Cache = new();
    private static readonly Regex IdPattern = new(@"(?<!\d)(\d{7,12})(?!\d)", RegexOptions.Compiled);

    public string Id => "boomlings-exact-id";
    public string DisplayName => "boomlings exact-id lookup";
    public ProviderCapabilities Capabilities =>
        ProviderCapabilities.SearchById | ProviderCapabilities.RetrieveMetadata;

    public async Task<ProviderSearchResponse> SearchAsync(
        ProviderSearchQuery query, CancellationToken cancellationToken)
    {
        var levelId = ExtractExactId(query);
        if (levelId is null)
            return new(Id, true, [], "skipped: Boomlings lookup requires one explicit level id.");

        if (Cache.TryGetValue(levelId, out var cached) &&
            DateTime.UtcNow - cached.CachedUtc < TimeSpan.FromMinutes(10))
            return cached.Response;

        try
        {
            await RateGate.WaitAsync(cancellationToken);
            try
            {
                if (Cache.TryGetValue(levelId, out cached) &&
                    DateTime.UtcNow - cached.CachedUtc < TimeSpan.FromMinutes(10))
                    return cached.Response;

                var wait = TimeSpan.FromSeconds(1) - (DateTime.UtcNow - _lastRequestUtc);
                if (wait > TimeSpan.Zero) await Task.Delay(wait, cancellationToken);

                using var content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["gameVersion"] = GameVersion.ToString(),
                    ["binaryVersion"] = BinaryVersion.ToString(),
                    ["type"] = "10",
                    ["str"] = levelId,
                    ["secret"] = CommonSecret
                });
                using var response = await Client.PostAsync(Endpoint, content, cancellationToken);
                _lastRequestUtc = DateTime.UtcNow;

                if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests)
                    return new(Id, false, [], $"Boomlings denied or rate-limited the exact-id request (HTTP {(int)response.StatusCode}). no access-control workaround was attempted.");
                response.EnsureSuccessStatusCode();

                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                if (body.Length > 1024 * 1024)
                    return new(Id, false, [], "Boomlings response exceeded the 1 MiB metadata limit.");

                var parsed = ParseLevelResponse(body, levelId);
                if (!parsed.Succeeded) return new(Id, false, [], parsed.Message);

                var result = new ProviderSearchResponse(Id, true, parsed.Result is null ? [] : [parsed.Result],
                    parsed.Message);
                Cache[levelId] = (DateTime.UtcNow, result);
                return result;
            }
            finally { RateGate.Release(); }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return new(Id, false, [], "Boomlings exact-id lookup failed: " + ex.Message);
        }
    }

    public static string? ExtractExactId(ProviderSearchQuery query)
    {
        if (!string.IsNullOrWhiteSpace(query.LevelId) &&
            Regex.IsMatch(query.LevelId.Trim(), @"^\d{7,12}$"))
            return query.LevelId.Trim();

        var text = string.Join(" ", new[] { query.Name, query.FreeText }
            .Where(x => !string.IsNullOrWhiteSpace(x)));
        var matches = IdPattern.Matches(text).Select(x => x.Value)
            .Distinct(StringComparer.Ordinal).ToArray();
        if (matches.Length != 1) return null;

        var hasLevelContext = Regex.IsMatch(text, @"\b(level|geometry\s*dash|gmd)\b", RegexOptions.IgnoreCase);
        return hasLevelContext || text.Trim() == matches[0] ? matches[0] : null;
    }

    public static (bool Succeeded, ProviderSearchResult? Result, string Message) ParseLevelResponse(
        string body, string expectedLevelId)
    {
        if (string.IsNullOrWhiteSpace(body) || body.Trim() == "-1")
            return (false, null, "Boomlings returned -1 or an empty response. this does not establish whether the level existed historically.");

        var levelSection = body.Split('#', 2)[0];
        if (string.IsNullOrWhiteSpace(levelSection))
            return (true, null, "Boomlings returned no level objects for this exact id.");

        foreach (var record in levelSection.Split('|', StringSplitOptions.RemoveEmptyEntries))
        {
            var fields = record.Split(':');
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var i = 0; i + 1 < fields.Length; i += 2)
                values[fields[i]] = fields[i + 1];

            if (!values.TryGetValue("1", out var returnedId) || returnedId != expectedLevelId)
                continue;

            values.TryGetValue("2", out var name);
            values.TryGetValue("6", out var creatorId);
            values.TryGetValue("35", out var songId);
            var title = string.IsNullOrWhiteSpace(name) ? expectedLevelId : name;
            return (true, new ProviderSearchResult(
                "Boomlings exact-id match: " + title,
                Endpoint,
                "boomlings server metadata",
                LevelId: returnedId,
                Creator: creatorId,
                SongId: songId,
                RawDataReference: "getGJLevels21.php · type=10 · exact level id=" + expectedLevelId),
                "Boomlings returned a matching level metadata object. this is current server metadata, not the original level string.");
        }

        return (true, null, "Boomlings returned a successful response but no exact matching level object. this describes current availability only.");
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        // Use a normal, identifiable client. If the server denies access, report it rather than
        // changing fingerprints or attempting to evade Cloudflare or rate limits.
        client.DefaultRequestHeaders.UserAgent.ParseAdd("LevelVault/0.8 (read-only exact-id lookup; github.com/Chayce22315)");
        client.DefaultRequestHeaders.Accept.ParseAdd("text/plain");
        return client;
    }
}
