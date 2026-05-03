using System.Text.Json.Serialization;

namespace NewsCollector.Api.Models;

public sealed record DeepSeekMaturityMetadata(
    string AnalysisVersion,
    string? MarketRegime,
    DateTimeOffset GeneratedAtUtc,
    IReadOnlyList<string> BacktestWindows,
    IReadOnlyList<string> SourceWeights,
    bool SimulatorMode,
    string? ModelName);

public sealed record DeepSeekBacktestSnapshot(
    string Window,
    decimal DirectionalAccuracy,
    decimal AvgReturnBps,
    decimal MaxDrawdownBps,
    int SampleCount);

public sealed record DeepSeekRiskManagement(
    string Action,
    decimal PositionSizeMultiplier,
    decimal StopLossPct,
    decimal TakeProfitPct,
    decimal ConfidenceFloor,
    IReadOnlyList<string> Constraints);

public sealed record DeepSeekExplainabilityItem(
    string Label,
    decimal Weight,
    string Rationale);

public sealed record DeepSeekSourceWeight(
    string Source,
    decimal Weight,
    string Reason);

public sealed record DeepSeekRegimeAwareness(
    string Regime,
    decimal Confidence,
    IReadOnlyList<string> Implications);

public sealed record DeepSeekAnalysisMaturityLayer(
    DeepSeekMaturityMetadata Metadata,
    IReadOnlyList<DeepSeekBacktestSnapshot> Backtests,
    DeepSeekRiskManagement RiskManagement,
    IReadOnlyList<DeepSeekExplainabilityItem> Explainability,
    IReadOnlyList<DeepSeekSourceWeight> SourceWeights,
    DeepSeekRegimeAwareness RegimeAwareness);