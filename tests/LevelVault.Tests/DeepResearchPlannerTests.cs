using LevelVault.Models;
using LevelVault.Services;
using Xunit;

namespace LevelVault.Tests;

public sealed class DeepResearchPlannerTests
{
    [Fact]
    public void NumericLevelIdCreatesTargetedGeometryDashSearch()
    {
        var investigation = new Investigation { Query = "133678584" };

        var queries = DeepResearchPlanner.CreateSeedQueries(investigation);

        Assert.Contains(queries, query =>
            query.Text.Equals("geometry dash level 133678584", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ExplicitLevelIdInMixedClueCreatesTargetedSearch()
    {
        var investigation = new Investigation { Query = "old mines level id: 133678584" };

        var queries = DeepResearchPlanner.CreateSeedQueries(investigation);

        Assert.Contains(queries, query =>
            query.Text.Equals("geometry dash level 133678584", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void FollowUpUsesDiscoveredTitleWithoutInventingIdentifiers()
    {
        var clue = new Clue { Title = "a vanished geometry dash level" };

        var queries = DeepResearchPlanner.CreateFollowUpQueries(clue, 1);

        Assert.Contains(queries, query => query.Text == "a vanished geometry dash level geometry dash");
        Assert.DoesNotContain(queries, query => query.Text.Contains("level id:", StringComparison.OrdinalIgnoreCase));
    }
}
