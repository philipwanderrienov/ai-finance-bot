using NewsCollector.Api.Models;

namespace NewsCollector.Api.Services.DeepSeekAnalysisService.RegimeAwareness;

public sealed class DeepSeekRegimeAwarenessService : IDeepSeekRegimeAwarenessService
{
    public DeepSeekRegimeAwareness DetermineRegime(DeepSeekAnalysisResult result)
    {
        var confidence = Math.Clamp(result.Confidence, 0.05m, 0.99m);
        var regime = result.Signal switch
        {
            "LONG" or "WATCH_LONG" when confidence >= 0.7m => "RiskOn",
            "SHORT" or "WATCH_SHORT" => "RiskOff",
            _ when confidence < 0.4m => "Choppy",
            _ when result.Verdict.Equals("bullish", StringComparison.OrdinalIgnoreCase) && confidence >= 0.65m => "RiskOn",
            _ when result.Verdict.Equals("bearish", StringComparison.OrdinalIgnoreCase) => "RiskOff",
            _ => "Neutral"
        };

        var implications = regime switch
        {
            "RiskOn" => new[] { "Favor momentum confirmation", "Prefer smaller hedges" },
            "RiskOff" => new[] { "Reduce exposure", "Tighten stops" },
            "Choppy" => new[] { "Use conservative sizing", "Require stronger confirmation" },
            _ => new[] { "Maintain neutral posture", "Wait for confirmation" }
        };

        return new DeepSeekRegimeAwareness(regime, confidence, implications);
    }
}
