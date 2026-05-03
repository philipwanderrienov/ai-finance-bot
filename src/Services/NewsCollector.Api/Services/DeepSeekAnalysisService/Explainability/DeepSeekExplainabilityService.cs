using NewsCollector.Api.Models;
using NewsCollector.Api.Services.DeepSeekAnalysisService.RegimeAwareness;
using NewsCollector.Api.Services.DeepSeekAnalysisService.SourceWeighting;

namespace NewsCollector.Api.Services.DeepSeekAnalysisService.Explainability;

public sealed class DeepSeekExplainabilityService : IDeepSeekExplainabilityService
{
    public IReadOnlyList<DeepSeekExplainabilityItem> BuildExplainability(
        DeepSeekAnalysisResult result,
        IReadOnlyList<DeepSeekSourceWeight> sourceWeights,
        DeepSeekRegimeAwareness regime)
    {
        var items = new List<DeepSeekExplainabilityItem>
        {
            new("SignalDirection", 0.35m, $"Model selected {result.Signal}"),
            new("Confidence", Math.Clamp(result.Confidence, 0.05m, 0.99m), $"Confidence score {result.Confidence:0.##}"),
            new("Regime", 0.2m, $"Current regime inferred as {regime.Regime}")
        };

        items.AddRange(sourceWeights.Take(3).Select(sw => new DeepSeekExplainabilityItem(sw.Source, sw.Weight, sw.Reason)));
        return items;
    }
}
