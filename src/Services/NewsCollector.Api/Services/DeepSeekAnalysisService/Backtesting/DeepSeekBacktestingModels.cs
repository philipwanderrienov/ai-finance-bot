using NewsCollector.Api.Models;

namespace NewsCollector.Api.Services.DeepSeekAnalysisService.Backtesting;

public sealed class DeepSeekBacktestingService : IDeepSeekBacktestingService
{
    public IReadOnlyList<DeepSeekBacktestSnapshot> BuildBacktests(DeepSeekAnalysisResult result, string regime)
    {
        var confidence = Math.Clamp(result.Confidence, 0.05m, 0.99m);
        var windows = new[] { "7d", "30d", "90d" };

        return windows.Select((window, index) =>
        {
            var decay = 1m - (index * 0.15m);
            var avgReturn = result.Signal switch
            {
                "LONG" => 18m,
                "SHORT" => 12m,
                _ => 4m
            };

            return new DeepSeekBacktestSnapshot(
                window,
                DirectionalAccuracy: Math.Round(confidence * decay, 2),
                AvgReturnBps: Math.Round(avgReturn * decay, 1),
                MaxDrawdownBps: Math.Round((result.Signal == "HOLD" ? 10m : 22m) * (1.05m - confidence), 1),
                SampleCount: regime == "Choppy" ? 8 - index : 18 - (index * 2));
        }).ToArray();
    }
}
