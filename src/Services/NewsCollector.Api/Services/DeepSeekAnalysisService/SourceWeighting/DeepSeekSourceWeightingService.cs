using NewsCollector.Api.Models;

namespace NewsCollector.Api.Services.DeepSeekAnalysisService.SourceWeighting;

public sealed class DeepSeekSourceWeightingService : IDeepSeekSourceWeightingService
{
    public IReadOnlyList<DeepSeekSourceWeight> BuildSourceWeights(DeepSeekAnalysisResult result)
    {
        var sources = result.SourceUrls.Take(5).ToArray();
        if (sources.Length == 0)
        {
            return new[]
            {
                new DeepSeekSourceWeight("news_articles", 0.5m, "Default evidence pool"),
                new DeepSeekSourceWeight("news_sources", 0.3m, "Source credibility baseline"),
                new DeepSeekSourceWeight("market_context", 0.2m, "Regime/context adjustment")
            };
        }

        var baseWeight = Math.Round(1m / sources.Length, 2);
        return sources.Select((s, i) => new DeepSeekSourceWeight(s, baseWeight, i == 0 ? "Primary evidence source" : "Supporting evidence")).ToArray();
    }
}
