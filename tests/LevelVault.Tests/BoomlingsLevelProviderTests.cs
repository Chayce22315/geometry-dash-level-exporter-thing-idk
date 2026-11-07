using LevelVault.Providers;
using Xunit;

namespace LevelVault.Tests;

public sealed class BoomlingsLevelProviderTests
{
    [Fact]
    public void ExactIdQueryIsRecognized()
    {
        Assert.Equal("133678584", BoomlingsLevelProvider.ExtractExactId(
            new ProviderSearchQuery(FreeText: "geometry dash level 133678584")));
    }

    [Fact]
    public void GenericGeometryDashQueryIsNotSentToBoomlings()
    {
        Assert.Null(BoomlingsLevelProvider.ExtractExactId(
            new ProviderSearchQuery(FreeText: "geometry dash deleted levels")));
    }

    [Fact]
    public void MultipleIdsAreNotGuessed()
    {
        Assert.Null(BoomlingsLevelProvider.ExtractExactId(
            new ProviderSearchQuery(FreeText: "geometry dash levels 133678584 123456789")));
    }

    [Fact]
    public void ParsesOnlyTheRequestedLevelId()
    {
        const string response = "1:133678584:2:Target Level:6:7654321:35:12345|1:222222222:2:Other Level:6:111:35:999#creator data";
        var parsed = BoomlingsLevelProvider.ParseLevelResponse(response, "133678584");

        Assert.True(parsed.Succeeded);
        Assert.NotNull(parsed.Result);
        Assert.Equal("133678584", parsed.Result.LevelId);
        Assert.Equal("Target Level", parsed.Result.Title.Replace("Boomlings exact-id match: ", ""));
        Assert.Equal("7654321", parsed.Result.Creator);
        Assert.Equal("12345", parsed.Result.SongId);
        Assert.Equal("boomlings server metadata", parsed.Result.RecordType);
    }

    [Fact]
    public void SuccessfulResponseWithoutExactIdReturnsNoMatch()
    {
        var parsed = BoomlingsLevelProvider.ParseLevelResponse(
            "1:222222222:2:Other Level:6:111:35:999#creator data", "133678584");

        Assert.True(parsed.Succeeded);
        Assert.Null(parsed.Result);
        Assert.Contains("no exact matching", parsed.Message);
    }

    [Fact]
    public void MinusOneIsNotMisreportedAsDeletion()
    {
        var parsed = BoomlingsLevelProvider.ParseLevelResponse("-1", "133678584");

        Assert.False(parsed.Succeeded);
        Assert.Null(parsed.Result);
        Assert.Contains("does not establish", parsed.Message);
    }
}
