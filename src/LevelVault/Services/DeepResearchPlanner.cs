using System.Text.RegularExpressions;
using LevelVault.Models;

namespace LevelVault.Services;

/// <summary>
/// Creates bounded, provenance-aware follow-up searches from the investigation
/// and newly discovered clues. It never invents a level id or creator.
/// </summary>
public static class DeepResearchPlanner
{
    public const int MaxQueriesPerRun = 24;
    public const int MaxDepth = 4;

    private static readonly Regex LevelId = new(@"\b(?:level\s*(?:id)?\s*[:#=]?\s*)(\d{4,12})\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex SongId = new(@"\b(?:song(?:\s*id)?|custom\s*song)\s*[:#=]\s*(\d{1,10})\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex Creator = new(@"\b(?:creator|created\s*by|author|username)\s*[:=@]\s*([A-Za-z0-9_-]{2,32})", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);

    public static IReadOnlyList<ResearchQuery> CreateSeedQueries(Investigation investigation)
    {
        var queries = new List<ResearchQuery>();
        Add(queries, investigation.Query, 0, "original investigation query");
        Add(queries, "geometry dash " + investigation.Query, 0, "game-context expansion");

        foreach (Match match in LevelId.Matches(investigation.Query))
            Add(queries, "geometry dash level " + match.Groups[1].Value, 0, "level-id expansion");
        foreach (Match match in SongId.Matches(investigation.Query))
            Add(queries, "geometry dash song " + match.Groups[1].Value, 0, "song-id expansion");
        foreach (Match match in Creator.Matches(investigation.Query))
            Add(queries, "geometry dash creator " + match.Groups[1].Value, 0, "creator expansion");

        foreach (var clue in investigation.Clues)
            foreach (var query in CreateFollowUpQueries(clue, 1))
                Add(queries, query.Text, query.Depth, query.Reason);

        return queries;
    }

    public static IReadOnlyList<ResearchQuery> CreateFollowUpQueries(Clue clue, int depth)
    {
        var queries = new List<ResearchQuery>();
        if (depth > MaxDepth) return queries;

        // A discovered title is useful as a separate search seed, but long descriptions are not.
        var title = Clean(clue.Title);
        if (title.Length >= 4 && title.Length <= 120)
            Add(queries, title + " geometry dash", depth, "discovered clue title");

        foreach (Match match in LevelId.Matches(clue.Detail + " " + clue.Title))
            Add(queries, "geometry dash level " + match.Groups[1].Value, depth, "newly discovered level id");
        foreach (Match match in SongId.Matches(clue.Detail + " " + clue.Title))
            Add(queries, "geometry dash song " + match.Groups[1].Value, depth, "newly discovered song id");
        foreach (Match match in Creator.Matches(clue.Detail + " " + clue.Title))
            Add(queries, "geometry dash creator " + match.Groups[1].Value, depth, "newly discovered creator");

        return queries;
    }

    private static void Add(List<ResearchQuery> queries, string value, int depth, string reason)
    {
        var normalized = Clean(value);
        if (normalized.Length < 3 || normalized.Length > 160) return;
        if (queries.Any(x => x.Text.Equals(normalized, StringComparison.OrdinalIgnoreCase))) return;
        queries.Add(new ResearchQuery(normalized, depth, reason));
    }

    private static string Clean(string value) =>
        Whitespace.Replace(value.Trim().Trim('"', '\'', '.', ',', ';'), " ");

    public sealed record ResearchQuery(string Text, int Depth, string Reason);
}
