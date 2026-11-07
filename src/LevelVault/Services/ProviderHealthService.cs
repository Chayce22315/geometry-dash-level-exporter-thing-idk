using System.Diagnostics;
using LevelVault.Providers;

namespace LevelVault.Services;

public sealed record ProviderHealth(string ProviderId, string DisplayName, bool Available, TimeSpan Elapsed, string Message);

/// <summary>Non-invasive status report. A provider's health is determined by a small real query, not a fabricated status.</summary>
public sealed class ProviderHealthService(ProviderRegistry registry)
{
    public async Task<IReadOnlyList<ProviderHealth>> CheckAsync(CancellationToken cancellationToken = default)
    {
        var checks = new List<ProviderHealth>();
        foreach (var provider in registry.Providers)
        {
            var timer = Stopwatch.StartNew();
            try
            {
                var response = await provider.SearchAsync(new ProviderSearchQuery(FreeText: "geometry dash"), cancellationToken);
                timer.Stop();
                checks.Add(new(provider.Id, provider.DisplayName, response.Succeeded, timer.Elapsed,
                    response.Message ?? (response.Succeeded ? "provider responded." : "provider returned an error.")));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                timer.Stop();
                checks.Add(new(provider.Id, provider.DisplayName, false, timer.Elapsed, ex.Message));
            }
        }
        return checks;
    }
}
