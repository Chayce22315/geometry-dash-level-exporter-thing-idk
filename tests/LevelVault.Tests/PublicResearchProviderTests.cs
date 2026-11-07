using LevelVault.Providers;
using Xunit;

namespace LevelVault.Tests;

public sealed class PublicResearchProviderTests
{
    [Fact]
    public async Task RedditProviderRejectsEmptySearchWithoutNetworkRequest()
    {
        var provider = new RedditSearchProvider();

        var response = await provider.SearchAsync(new ProviderSearchQuery(), CancellationToken.None);

        Assert.False(response.Succeeded);
        Assert.Empty(response.Results);
        Assert.Contains("search clue", response.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task YouTubeProviderRejectsEmptySearchWithoutNetworkRequest()
    {
        var provider = new YouTubeSearchProvider();

        var response = await provider.SearchAsync(new ProviderSearchQuery(), CancellationToken.None);

        Assert.False(response.Succeeded);
        Assert.Empty(response.Results);
        Assert.Contains("search clue", response.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PublicProvidersExposeMetadataSearchCapabilities()
    {
        foreach (var provider in new IArchiveProvider[]
                 {
                     new RedditSearchProvider(),
                     new YouTubeSearchProvider()
                 })
        {
            Assert.NotEqual(ProviderCapabilities.None, provider.Capabilities);
            Assert.True(provider.Capabilities.HasFlag(ProviderCapabilities.RetrieveMetadata));
            Assert.False(provider.Capabilities.HasFlag(ProviderCapabilities.RetrieveLevelData));
        }
    }
}
