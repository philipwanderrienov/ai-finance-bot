using NewsCollector.Api.Models;

namespace NewsCollector.Api.Services.DeepSeekAnalysisService.Persistence;

public interface IDeepSeekAnalysisPersistenceService
{
    Task PersistAsync(
        DeepSeekAnalysisResult result,
        IReadOnlyList<string> keyPoints,
        IReadOnlyList<string> riskFactors,
        IReadOnlyList<string> sourceUrls,
        string inputFingerprint,
        int inputArticleCount,
        CancellationToken cancellationToken);

    Task StoreMaturityAsync(
        DeepSeekAnalysisResult result,
        DeepSeekAnalysisMaturityLayer maturity,
        IReadOnlyList<DeepSeekAnalysisOutcome> futureOutcomes,
        CancellationToken cancellationToken);
}
