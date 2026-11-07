using LevelVault.Services;
using Xunit;

namespace LevelVault.Tests;

public sealed class SearchPlannerTests
{
    [Fact]
    public void SearchSetIncludesTikTokAndPubliclyIndexedDiscordLinks()
    {
        var targets = SearchPlanner.BuildTargets("133678584");

        var tiktok = Assert.Single(targets.Where(x => x.Label == "tiktok public search"));
        Assert.Equal("www.tiktok.com", new Uri(tiktok.Url).Host);
        Assert.Contains("133678584", tiktok.Url);

        var discord = Assert.Single(targets.Where(x => x.Label == "discord indexed links"));
        Assert.Equal("www.google.com", new Uri(discord.Url).Host);
        Assert.Contains("site%3Adiscord.com%2Fchannels", discord.Url);
        Assert.Contains("133678584", discord.Url);
    }

    [Fact]
    public void SearchSetIsEmptyForBlankQuery()
    {
        Assert.Empty(SearchPlanner.BuildTargets("   "));
    }
}
