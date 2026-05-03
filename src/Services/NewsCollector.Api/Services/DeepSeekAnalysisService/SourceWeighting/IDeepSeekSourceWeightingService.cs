using NewsCollector.Api.Models;

namespace NewsCollector.Api.Services.DeepSeekAnalysisService.SourceWeighting;

public interface IDeepSeekSourceWeightingService
{
    IReadOnlyList<DeepSeekSourceWeight> BuildSourceWeights(DeepSeekAnalysisResult result);
}
