using NewsCollector.Api.Models;
using NewsCollector.Api.Services.DeepSeekAnalysisService.Backtesting;
using NewsCollector.Api.Services.DeepSeekAnalysisService.Explainability;
using NewsCollector.Api.Services.DeepSeekAnalysisService.RegimeAwareness;
using NewsCollector.Api.Services.DeepSeekAnalysisService.RiskManagement;
using NewsCollector.Api.Services.DeepSeekAnalysisService.SourceWeighting;

namespace NewsCollector.Api.Services.DeepSeekAnalysisService;

public interface IDeepSeekAnalysisMaturityService
{
    DeepSeekAnalysisMaturityLayer BuildMaturityLayer(DeepSeekAnalysisResult result, bool simulatorMode, string? modelName = null);
}

public sealed class DeepSeekAnalysisMaturityService : IDeepSeekAnalysisMaturityService
{
    private readonly IDeepSeekRegimeAwarenessService _regimeAwarenessService;
    private readonly IDeepSeekSourceWeightingService _sourceWeightingService;
    private readonly IDeepSeekBacktestingService _backtestingService;
    private readonly IDeepSeekRiskManagementService _riskManagementService;
    private readonly IDeepSeekExplainabilityService _explainabilityService;

    public DeepSeekAnalysisMaturityService(
        IDeepSeekRegimeAwarenessService regimeAwarenessService,
        IDeepSeekSourceWeightingService sourceWeightingService,
        IDeepSeekBacktestingService backtestingService,
        IDeepSeekRiskManagementService riskManagementService,
        IDeepSeekExplainabilityService explainabilityService)
    {
        _regimeAwarenessService = regimeAwarenessService;
        _sourceWeightingService = sourceWeightingService;
        _backtestingService = backtestingService;
        _riskManagementService = riskManagementService;
        _explainabilityService = explainabilityService;
    }

    public DeepSeekAnalysisMaturityLayer BuildMaturityLayer(DeepSeekAnalysisResult result, bool simulatorMode, string? modelName = null)
    {
        var regime = _regimeAwarenessService.DetermineRegime(result);
        var sourceWeights = _sourceWeightingService.BuildSourceWeights(result);
        var backtests = _backtestingService.BuildBacktests(result, regime.Regime);
        var risk = _riskManagementService.BuildRiskManagement(result, regime);
        var explainability = _explainabilityService.BuildExplainability(result, sourceWeights, regime);

        var metadata = new DeepSeekMaturityMetadata(
            AnalysisVersion: "2.0",
            MarketRegime: regime.Regime,
            GeneratedAtUtc: DateTimeOffset.UtcNow,
            BacktestWindows: backtests.Select(x => x.Window).ToArray(),
            SourceWeights: sourceWeights.Select(x => $"{x.Source}:{x.Weight:0.##}").ToArray(),
            SimulatorMode: simulatorMode,
            ModelName: modelName);

        return new DeepSeekAnalysisMaturityLayer(
            metadata,
            backtests,
            risk,
            explainability,
            sourceWeights,
            regime);
    }
}
