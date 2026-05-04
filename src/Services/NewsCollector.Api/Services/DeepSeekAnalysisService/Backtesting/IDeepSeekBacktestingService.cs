using NewsCollector.Api.Models;

namespace NewsCollector.Api.Services.DeepSeekAnalysisService.Backtesting;

public interface IDeepSeekBacktestingService
{
    IReadOnlyList<DeepSeekBacktestSnapshot> BuildBacktests(DeepSeekAnalysisResult result, string regime);
    IReadOnlyList<DeepSeekAnalysisOutcome> BuildFutureOutcomes(DeepSeekAnalysisResult result, string regime);
}
