using NewsCollector.Api.Models;
using NewsCollector.Api.Services.DeepSeekAnalysisService.RegimeAwareness;

namespace NewsCollector.Api.Services.DeepSeekAnalysisService.RiskManagement;

public sealed class DeepSeekRiskManagementService : IDeepSeekRiskManagementService
{
    public DeepSeekRiskManagement BuildRiskManagement(DeepSeekAnalysisResult result, DeepSeekRegimeAwareness regime)
    {
        var confidence = Math.Clamp(result.Confidence, 0.05m, 0.99m);
        var action = result.Signal switch
        {
            "LONG" => "ScaleIn",
            "SHORT" => "Reduce",
            _ => "Wait"
        };

        var constraints = new List<string>
        {
            "Respect portfolio-level limits",
            "Do not trade against major regime"
        };

        if (regime.Regime == "Choppy")
        {
            constraints.Add("Require tighter entry criteria");
        }

        return new DeepSeekRiskManagement(
            action,
            PositionSizeMultiplier: Math.Round(confidence * (regime.Regime == "RiskOn" ? 1.1m : 0.8m), 2),
            StopLossPct: regime.Regime == "RiskOff" ? 1.5m : 2.2m,
            TakeProfitPct: regime.Regime == "RiskOn" ? 4.0m : 2.8m,
            ConfidenceFloor: 0.35m,
            constraints);
    }
}
