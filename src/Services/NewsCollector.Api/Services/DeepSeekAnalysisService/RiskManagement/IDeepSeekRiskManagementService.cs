using NewsCollector.Api.Models;
using NewsCollector.Api.Services.DeepSeekAnalysisService.RegimeAwareness;

namespace NewsCollector.Api.Services.DeepSeekAnalysisService.RiskManagement;

public interface IDeepSeekRiskManagementService
{
    DeepSeekRiskManagement BuildRiskManagement(DeepSeekAnalysisResult result, DeepSeekRegimeAwareness regime);
}
