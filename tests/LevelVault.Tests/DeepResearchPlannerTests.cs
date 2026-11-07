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
    public void NumericIdAddsExactAndFileSearchVariants()
    {
        var queries = DeepResearchPlanner.CreateSeedQueries(new Investigation { Query = "133678584" });

        Assert.Contains(queries, query => query.Text == "geometry dash level 133678584");
        Assert.Contains(queries, query => query.Text == "\"133678584\" geometry dash");
        Assert.Contains(queries, query => query.Text == "133678584 gmd");
    }

    [Fact]
    public void ResearchHasExpandedButBoundedDepthAndQueryBudget()
    {
        Assert.Equal(36, DeepResearchPlanner.MaxQueriesPerRun);
        Assert.Equal(5, DeepResearchPlanner.MaxDepth);
    }

    [Fact]
    public void FollowUpAddsExactTitleAndFileArchiveSearches()
    {
        var clue = new Clue { Title = "old mines" };

        var queries = DeepResearchPlanner.CreateFollowUpQueries(clue, 2);

        Assert.Contains(queries, query => query.Text == "old mines geometry dash");
        Assert.Contains(queries, query => query.Text == "\"old mines\" geometry dash");
        Assert.Contains(queries, query => query.Text == "old mines deleted level gmd");
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
