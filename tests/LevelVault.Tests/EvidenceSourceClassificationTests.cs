using LevelVault.Models;
using LevelVault.Services;
using Xunit;

namespace LevelVault.Tests;

public sealed class EvidenceSourceClassificationTests
{
    [Theory]
    [InlineData("https://www.tiktok.com/@creator/video/123456789", "video")]
    [InlineData("https://vm.tiktok.com/abc123/", "video")]
    [InlineData("https://discord.com/channels/123/456/789", "discussion")]
    [InlineData("https://discord.gg/example", "discussion")]
    [InlineData("https://www.reddit.com/r/geometrydash/comments/example/post/", "discussion")]
    [InlineData("https://redd.it/example", "discussion")]
    public void SocialAndVideoLinksBecomeTypedEvidence(string url, string expectedType)
    {
        var investigation = new Investigation
        {
            Query = "geometry dash level 133678584"
        };
        investigation.Clues.Add(new Clue
        {
            Title = "shared level reference",
            SourceUrl = url,
            Detail = "user supplied public link"
        });

        new EvidenceAnalysisService().Analyze(investigation);

        Assert.Contains(investigation.EvidenceNodes,
            node => node.Type == expectedType && node.SourceUrl == url);
    }

    [Fact]
    public void UnrecognizedHostsRemainGenericWebReferences()
    {
        const string url = "https://example.com/level-clue";
        var investigation = new Investigation { Query = "geometry dash" };
        investigation.Clues.Add(new Clue { Title = "reference", SourceUrl = url });

        new EvidenceAnalysisService().Analyze(investigation);

        Assert.Contains(investigation.EvidenceNodes,
            node => node.Type == "web-reference" && node.SourceUrl == url);
    }
}
