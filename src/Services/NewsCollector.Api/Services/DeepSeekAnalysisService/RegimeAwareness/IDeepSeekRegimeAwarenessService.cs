using NewsCollector.Api.Models;

namespace NewsCollector.Api.Services.DeepSeekAnalysisService.RegimeAwareness;

public interface IDeepSeekRegimeAwarenessService
{
    DeepSeekRegimeAwareness DetermineRegime(DeepSeekAnalysisResult result);
}
