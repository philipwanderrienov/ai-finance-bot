using NewsCollector.Api.Models;
using NewsCollector.Api.Services.DeepSeekAnalysisService.RegimeAwareness;
using NewsCollector.Api.Services.DeepSeekAnalysisService.SourceWeighting;

namespace NewsCollector.Api.Services.DeepSeekAnalysisService.Explainability;

public interface IDeepSeekExplainabilityService
{
    IReadOnlyList<DeepSeekExplainabilityItem> BuildExplainability(
        DeepSeekAnalysisResult result,
        IReadOnlyList<DeepSeekSourceWeight> sourceWeights,
        DeepSeekRegimeAwareness regime);
}
