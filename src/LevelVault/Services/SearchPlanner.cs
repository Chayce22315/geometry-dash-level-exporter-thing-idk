using System.Diagnostics;

namespace LevelVault.Services;

public sealed record SearchTarget(string Label, string Url);

public static class SearchPlanner
{
    public static IReadOnlyList<SearchTarget> BuildTargets(string query)
    {
        var q = Uri.EscapeDataString(query.Trim());
        if (string.IsNullOrWhiteSpace(query)) return [];
        return
        [
            new("web search · bing", $"https://www.bing.com/search?q={q}+geometry+dash"),
            new("web search · duckduckgo", $"https://duckduckgo.com/?q={q}+geometry+dash"),
            new("youtube", $"https://www.youtube.com/results?search_query={q}+geometry+dash"),
            new("tiktok public search", $"https://www.tiktok.com/search?q={q}%20geometry%20dash"),
            new("github", $"https://github.com/search?q={q}+geometry+dash&type=code"),
            new("reddit", $"https://www.reddit.com/search/?q={q}+geometry+dash"),
            new("discord indexed links", $"https://www.google.com/search?q={q}+geometry+dash+site%3Adiscord.com%2Fchannels"),
            new("gd forums", $"https://www.google.com/search?q={q}+site%3Ageode-sdk.org+OR+site%3Agithub.com")
        ];
    }

    public static void Open(SearchTarget target)
    {
        Process.Start(new ProcessStartInfo(target.Url) { UseShellExecute = true });
    }

    public static void OpenAll(IEnumerable<SearchTarget> targets)
    {
        foreach (var target in targets) Open(target);
    }
}
