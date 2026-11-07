using LevelVault.Models;
using LevelVault.Providers;
using LevelVault.Services;
using Xunit;

namespace LevelVault.Tests;

public sealed class InvestigationResearchServiceTests
{
    [Fact]
    public async Task OneProviderFailureDoesNotStopOtherProvidersOrInvestigation()
    {
        var registry = new ProviderRegistry();
        var failing = new StubProvider("unavailable", _ =>
            throw new HttpRequestException("simulated connection failure"));
        var healthy = new StubProvider("healthy", _ =>
            Task.FromResult(new ProviderSearchResponse(
                "healthy", true, Array.Empty<ProviderSearchResult>(), "no public matches")));

        registry.Register(failing);
        registry.Register(healthy);

        var investigation = new Investigation { Query = "133678584" };
        var service = new InvestigationResearchService(registry, new JsonResponseCache());

        var summary = await service.ResearchAsync(investigation);

        Assert.True(summary.QueriesExecuted > 0);
        Assert.True(summary.ProvidersFailed > 0);
        Assert.True(summary.ProvidersSucceeded > 0);
        Assert.Equal("completed", investigation.ResearchStatus);
        Assert.Empty(investigation.PendingResearchQueries);
        Assert.Contains(summary.Messages, message =>
            message.Contains("provider failed: HttpRequestException", StringComparison.Ordinal));
        Assert.True(failing.Calls > 0);
        Assert.True(healthy.Calls > 0);
    }

    private sealed class StubProvider(
        string id,
        Func<ProviderSearchQuery, Task<ProviderSearchResponse>> search) : IArchiveProvider
    {
        public string Id { get; } = id;
        public string DisplayName => Id;
        public ProviderCapabilities Capabilities => ProviderCapabilities.SearchById;
        public int Calls { get; private set; }

        public Task<ProviderSearchResponse> SearchAsync(
            ProviderSearchQuery query, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            return search(query);
        }
    }
}
